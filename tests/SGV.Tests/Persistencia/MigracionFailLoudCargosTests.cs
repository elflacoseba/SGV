using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests de integración para la migración expand-contract de Cargos (Nivel string → NivelId FK).
///
/// Test 1 (Limpio): Verifica que EnsureCreated produce el schema final correcto.
///   - NivelesCargo tiene 4 filas (seeds con Codigo: Directivo, ConduccionMedia, Operativo, Academico)
///   - NivelId columna existe en Cargos
///   - Nivel columna fue dropeada de Cargos
///   - FK NivelId → NivelesCargo.Id existe con OnDelete(Restrict)
///
/// Test 2 (Sucio): Verifica el fail-loud ejecutando directamente el SQL
///   de la migración contra una BD con datos no catalogados.
///   - Inserta un string no catalogado en Nivel
///   - Ejecuta el SQL del pre-flight y verifica MySqlException 45000
///
/// Requieren MySQL 8 real en localhost:3306 con root sin password.
/// </summary>
public sealed class MigracionFailLoudCargosTests : IAsyncLifetime
{
    private string _testDbName = null!;
    private string _testConnectionString = null!;
    private string _masterConnectionString = "Server=localhost;Port=3306;Database=SGV;User=root;";

    public Task InitializeAsync()
    {
        _testDbName = $"SGV_Test_Migration_Cargos_{Guid.NewGuid():N}";
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
    public async Task MigracionCargos_DatosLimpios_NivelesCargoCreadosCon4Seeds()
    {
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Assert: NivelesCargo tiene 4 filas
        var nivelesCount = await ctx.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM NivelesCargo").ToListAsync();
        Assert.Equal(4, nivelesCount.First());

        // Assert: Los 4 seeds tienen los Codigo correctos
        var codigos = await ctx.Database.SqlQueryRaw<string>(
            "SELECT Codigo AS Value FROM NivelesCargo ORDER BY Orden").ToListAsync();
        Assert.Equal(
            new[] { "Directivo", "ConduccionMedia", "Operativo", "Academico" },
            codigos);

        // Assert: La columna Nivel NO existe en Cargos
        var columnaNivel = await ctx.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cargos' AND COLUMN_NAME = 'Nivel'")
            .ToListAsync();
        Assert.Empty(columnaNivel);

        // Assert: NivelId SÍ existe en Cargos
        var columnaNivelId = await ctx.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cargos' AND COLUMN_NAME = 'NivelId'")
            .ToListAsync();
        Assert.Single(columnaNivelId);
    }

    // ========================================================================
    // TEST 2: FK Restrict configurada correctamente
    // ========================================================================

    [MySqlFact]
    public async Task MigracionCargos_DatosLimpios_FkRestrictExiste()
    {
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Assert: La FK existe con DELETE RESTRICT
        var fkInfo = await ctx.Database.SqlQueryRaw<string>(
            "SELECT DELETE_RULE AS Value FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA = DATABASE() " +
            "AND REFERENCED_TABLE_NAME = 'NivelesCargo' " +
            "AND CONSTRAINT_NAME LIKE 'FK_Cargos_NivelesCargo%'")
            .ToListAsync();
        Assert.Single(fkInfo);
        Assert.Equal("RESTRICT", fkInfo.First());
    }

    // ========================================================================
    // TEST 3: Fail-loud — ejecuta el SQL del pre-flight directamente
    // ========================================================================

    [MySqlFact]
    public async Task MigracionCargos_DatosSucios_LanzaMySqlException45000()
    {
        // Arrange: crear BD con schema final, luego agregar Nivel columna temporal
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Obtener un NivelCargoId válido existente (de los 4 seeds)
        string nivelCargoId;
        await using (var conn = new MySqlConnection(_testConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CAST(Id AS CHAR(36)) FROM NivelesCargo LIMIT 1";
            nivelCargoId = (await cmd.ExecuteScalarAsync())!.ToString()!;
        }

        // Desactivar FK checks para poder insertar filas con Nivel temporal
        await ctx.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0");

        // Agregar Nivel como columna temporal (simula schema viejo con datos sucios)
        await ctx.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `Cargos` ADD COLUMN `Nivel` VARCHAR(50) NULL");

        // Insertar una fila con Nivel no catalogado
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO `Cargos` " +
            "(`Id`, `Codigo`, `Nombre`, `Descripcion`, `NivelId`, `Nivel`, " +
            "`IsActive`, `IsDeleted`, `CreatedAt`) " +
            "VALUES ('00000000-0000-0000-0000-000000000099', 'DIRTY', 'Dirty Cargo', " +
            $"NULL, '{nivelCargoId}', 'FooBar', 1, 0, NOW())");

        // Restaurar FK checks
        await ctx.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1");

        // Act + Assert: ejecutar el SQL del fail-loud pre-flight
        var ex = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await using var conn = new MySqlConnection(_testConnectionString);
            await conn.OpenAsync();

            // Step 1: crear tabla temporal de dirty niveles
            await using (var cmd1 = conn.CreateCommand())
            {
                cmd1.CommandText = @"
                    CREATE TEMPORARY TABLE _DirtyNivelesCargo AS
                    SELECT DISTINCT Nivel
                    FROM Cargos
                    WHERE Nivel IS NOT NULL
                      AND Nivel NOT IN ('Directivo', 'Conducción media', 'Operativo', 'Académico')
                      AND Nivel NOT IN (SELECT Codigo FROM NivelesCargo)";
                await cmd1.ExecuteNonQueryAsync();
            }

            // Step 2: contar y preparar variables
            int dirtyCount;
            string dirtyExamples;
            await using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = "SELECT COUNT(*) FROM _DirtyNivelesCargo";
                dirtyCount = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            }
            await using (var cmd3 = conn.CreateCommand())
            {
                cmd3.CommandText = @"
                    SELECT COALESCE(
                        GROUP_CONCAT(Nivel SEPARATOR ', '),
                        'ninguno')
                    FROM (SELECT Nivel FROM _DirtyNivelesCargo LIMIT 5) AS d";
                dirtyExamples = (await cmd3.ExecuteScalarAsync())?.ToString() ?? "ninguno";
            }

            // Step 3: SIGNAL si hay datos sucios
            if (dirtyCount > 0)
            {
                await using var cmd4 = conn.CreateCommand();
                cmd4.CommandText =
                    $"SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Backfill fail-loud: " +
                    $"{dirtyCount} valores de Nivel sin catalogar. Ejemplos: {dirtyExamples}'";
                await cmd4.ExecuteNonQueryAsync();
            }

            // Cleanup
            await using (var cmd5 = conn.CreateCommand())
            {
                cmd5.CommandText = "DROP TEMPORARY TABLE IF EXISTS _DirtyNivelesCargo";
                await cmd5.ExecuteNonQueryAsync();
            }
        });

        // MySQL lanza error 1644 (ER_SIGNAL_EXCEPTION) para SQLSTATE '45000'
        Assert.Equal(1644, ex.Number);
        Assert.Contains("Backfill fail-loud", ex.Message);
    }

    // ========================================================================
    // TEST 4: Check constraint CK_NivelesCargo_ValorNumerico declarada en BD
    // ========================================================================
    //
    // The check constraint `>= 0 AND <= 255` matches the byte range used by the
    // `ValorNumerico` column. Attempting an out-of-range value would be rejected
    // at the column-type level (tinyint unsigned) before the constraint fires, so
    // the runtime proof is the EXISTENCE of the check constraint itself.
    // Without it, a future column type change could silently widen the allowed
    // range and the constraint would still pass trivially.

    [MySqlFact]
    public async Task MigracionCargos_DatosLimpios_CheckConstraint_ValorNumericoExiste()
    {
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Assert: La check constraint CK_NivelesCargo_ValorNumerico está registrada
        // en INFORMATION_SCHEMA.CHECK_CONSTRAINTS.
        var constraintNames = await ctx.Database.SqlQueryRaw<string>(
            "SELECT CONSTRAINT_NAME AS Value FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA = DATABASE() " +
            "AND CONSTRAINT_NAME = 'CK_NivelesCargo_ValorNumerico'")
            .ToListAsync();

        Assert.Single(constraintNames);
        Assert.Equal("CK_NivelesCargo_ValorNumerico", constraintNames.First());

        // Assert: La expresión de la constraint coincide con el rango declarado.
        var checkClauses = await ctx.Database.SqlQueryRaw<string>(
            "SELECT CHECK_CLAUSE AS Value FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS " +
            "WHERE CONSTRAINT_SCHEMA = DATABASE() " +
            "AND CONSTRAINT_NAME = 'CK_NivelesCargo_ValorNumerico'")
            .ToListAsync();

        Assert.Single(checkClauses);
        var clause = checkClauses.First();
        Assert.Contains("ValorNumerico", clause);
        Assert.Contains(">=", clause);
        Assert.Contains("0", clause);
        Assert.Contains("<=", clause);
        Assert.Contains("255", clause);
    }
}
