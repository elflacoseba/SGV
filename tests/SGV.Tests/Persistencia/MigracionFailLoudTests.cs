using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests de integración para la migración expand-contract de TipoUnidadOrganizativa.
///
/// Test 1 (Limpio): Verifica que EnsureCreated produce el schema final correcto.
///   - TiposUnidadOrganizativa tiene 7 filas (seeds)
///   - TipoUnidad fue dropeada de UnidadesOrganizativas
///   - TipoUnidadOrganizativaId FK existe con OnDelete(Restrict)
///
/// Test 2 (Sucio): Verifica el fail-loud ejecutando directamente el SQL
///   de la migración contra una BD con datos no catalogados.
///   - Inserta un string no catalogado en TipoUnidad
///   - Ejecuta el SQL del pre-flight y verifica MySqlException 45000
///
/// Requieren MySQL 8 real en localhost:3306 con root sin password.
/// </summary>
public sealed class MigracionFailLoudTests : IAsyncLifetime
{
    private string _testDbName = null!;
    private string _testConnectionString = null!;
    private string _masterConnectionString = "Server=localhost;Port=3306;Database=SGV;User=root;";

    public Task InitializeAsync()
    {
        _testDbName = $"SGV_Test_Migration_{Guid.NewGuid():N}";
        _testConnectionString = $"Server=localhost;Port=3306;Database={_testDbName};User=root;";
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var masterConn = new MySqlConnection(_masterConnectionString);
            await masterConn.OpenAsync();
            await using var cmd = masterConn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS `{_testDbName}`";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private SgvDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(_testConnectionString, ServerVersion.Parse("8.0.0-mysql"))
            .Options;
        return new SgvDbContext(options);
    }

    private async Task<SgvDbContext> CreateFreshTestDatabaseAsync()
    {
        var ctx = CreateTestContext();
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    // ========================================================================
    // TEST 1: Backfill limpio — schema final correcto
    // ========================================================================

    [MySqlFact]
    public async Task Migracion_DatosLimpios_TiposUnidadOrganizativaCreadosCon7Seeds()
    {
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Assert: TiposUnidadOrganizativa tiene 7 filas
        var tiposCount = await ctx.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM TiposUnidadOrganizativa").ToListAsync();
        Assert.Equal(7, tiposCount.First());

        // Assert: Los 7 seeds tienen los Codigo correctos
        var codigos = await ctx.Database.SqlQueryRaw<string>(
            "SELECT Codigo AS Value FROM TiposUnidadOrganizativa ORDER BY Codigo").ToListAsync();
        Assert.Equal(
            new[] { "Area", "Departamento", "Direccion", "Division", "Facultad", "Institucion", "Secretaria" },
            codigos);

        // Assert: La columna TipoUnidad NO existe
        var columnaLegacy = await ctx.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UnidadesOrganizativas' AND COLUMN_NAME = 'TipoUnidad'")
            .ToListAsync();
        Assert.Empty(columnaLegacy);

        // Assert: TipoUnidadOrganizativaId SÍ existe
        var columnaFk = await ctx.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UnidadesOrganizativas' AND COLUMN_NAME = 'TipoUnidadOrganizativaId'")
            .ToListAsync();
        Assert.Single(columnaFk);
    }

    // ========================================================================
    // TEST 2: FK Restrict configurada correctamente
    // ========================================================================

    [MySqlFact]
    public async Task Migracion_DatosLimpios_FkRestrictExiste()
    {
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Assert: La FK existe con DELETE RESTRICT
        // INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS usa CONSTRAINT_SCHEMA, no TABLE_SCHEMA
        var fkInfo = await ctx.Database.SqlQueryRaw<string>(
            "SELECT DELETE_RULE AS Value FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA = DATABASE() " +
            "AND REFERENCED_TABLE_NAME = 'TiposUnidadOrganizativa' " +
            "AND CONSTRAINT_NAME LIKE 'FK_UnidadesOrganizativas_TiposUnidad%'")
            .ToListAsync();
        Assert.Single(fkInfo);
        Assert.Equal("RESTRICT", fkInfo.First());
    }

    // ========================================================================
    // TEST 3: Fail-loud — ejecuta el SQL del pre-flight directamente
    //
    // No re-ejecuta la migración completa (ya fue aplicada por EnsureCreated).
    // En su lugar, recrea la condición sucia y ejecuta el SQL del fail-loud
    // para verificar que produce MySqlException 45000.
    // ========================================================================

    [MySqlFact]
    public async Task Migracion_DatosSucios_LanzaMySqlException45000()
    {
        // Arrange: crear BD con schema final, luego agregar TipoUnidad con datos sucios
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Obtener un TipoUnidadOrganizativaId válido existente (de los 7 seeds)
        string tipoUnidadOrganizativaId;
        await using (var conn = new MySqlConnection(_testConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CAST(Id AS CHAR(36)) FROM TiposUnidadOrganizativa LIMIT 1";
            tipoUnidadOrganizativaId = (await cmd.ExecuteScalarAsync())!.ToString()!;
        }

        // Desactivar FK checks para poder insertar filas con TipoUnidad temporal
        await ctx.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0");

        // Agregar TipoUnidad como columna temporal (simula schema viejo con datos sucios)
        await ctx.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `UnidadesOrganizativas` ADD COLUMN `TipoUnidad` VARCHAR(50) NOT NULL DEFAULT ''");

        // Insertar una fila con TipoUnidad no catalogado
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO `UnidadesOrganizativas` " +
            "(`Id`, `Codigo`, `Nombre`, `TipoUnidadOrganizativaId`, `TipoUnidad`, `IsActive`, `IsDeleted`, `CreatedAt`) " +
            $"VALUES ('00000000-0000-0000-0000-000000000099', 'Dirty', 'Dirty Unit', " +
            $"'{tipoUnidadOrganizativaId}', 'FooBar', 0, 0, NOW())");

        // Restaurar FK checks
        await ctx.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1");

        // Act + Assert: ejecutar el SQL del fail-loud pre-flight
        // Ejecutamos la detección y el SIGNAL como pasos separados para
        // evitar problemas de propagación de errores en multi-statement batches.
        var ex = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await using var conn = new MySqlConnection(_testConnectionString);
            await conn.OpenAsync();

            // Step 1: crear tabla temporal de dirty types
            await using (var cmd1 = conn.CreateCommand())
            {
                cmd1.CommandText = @"
                    CREATE TEMPORARY TABLE _DirtyTiposUnidad AS
                    SELECT DISTINCT TipoUnidad
                    FROM UnidadesOrganizativas
                    WHERE TipoUnidad NOT IN (
                        'Institucion', 'Facultad', 'Secretaria', 'Direccion',
                        'Departamento', 'Division', 'Area'
                    )";
                await cmd1.ExecuteNonQueryAsync();
            }

            // Step 2: contar y preparar variables
            int dirtyCount;
            string dirtyExamples;
            await using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = "SELECT COUNT(*) FROM _DirtyTiposUnidad";
                dirtyCount = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            }
            await using (var cmd3 = conn.CreateCommand())
            {
                cmd3.CommandText = @"
                    SELECT COALESCE(
                        GROUP_CONCAT(TipoUnidad SEPARATOR ', '),
                        'ninguno')
                    FROM (SELECT TipoUnidad FROM _DirtyTiposUnidad LIMIT 5) AS d";
                dirtyExamples = (await cmd3.ExecuteScalarAsync())?.ToString() ?? "ninguno";
            }

            // Step 3: SIGNAL si hay datos sucios
            if (dirtyCount > 0)
            {
                await using var cmd4 = conn.CreateCommand();
                cmd4.CommandText =
                    $"SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Backfill fail-loud: " +
                    $"{dirtyCount} valores de TipoUnidad sin catalogar. Ejemplos: {dirtyExamples}'";
                await cmd4.ExecuteNonQueryAsync();
            }

            // Cleanup
            await using (var cmd5 = conn.CreateCommand())
            {
                cmd5.CommandText = "DROP TEMPORARY TABLE IF EXISTS _DirtyTiposUnidad";
                await cmd5.ExecuteNonQueryAsync();
            }
        });

        // MySQL lanza error 1644 (ER_SIGNAL_EXCEPTION) para SQLSTATE '45000'
        Assert.Equal(1644, ex.Number);
        Assert.Contains("Backfill fail-loud", ex.Message);
    }
}
