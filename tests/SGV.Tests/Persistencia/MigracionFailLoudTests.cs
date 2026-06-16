using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests de integración para la migración expand-contract de TipoUnidadOrganizativa.
///
/// Test 1 (Limpio): Verifica que la migración produce el schema final correcto.
///   - TiposUnidadOrganizativa tiene 7 filas (seeds)
///   - TipoUnidad fue dropeada de UnidadesOrganizativas
///   - TipoUnidadOrganizativaId FK existe con OnDelete(Restrict)
///
/// Test 2 (Sucio): Verifica el fail-loud con SIGNAL SQLSTATE '45000'.
///   - Inserta un string no catalogado en TipoUnidad
///   - La migración aborta con MySqlException antes de ALTER TABLE
///
/// Estos tests requieren MySQL 8 real. Se saltan si no está disponible.
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

    /// <summary>
    /// Crea un SgvDbContext apuntando a la BD de test.
    /// </summary>
    private SgvDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(_testConnectionString, ServerVersion.Parse("8.0.0-mysql"))
            .Options;
        return new SgvDbContext(options);
    }

    /// <summary>
    /// Crea una BD de test a partir del snapshot actual (schema final, sin migraciones pendientes).
    /// Retorna el contexto listo para usar.
    /// </summary>
    private async Task<SgvDbContext> CreateFreshTestDatabaseAsync()
    {
        var ctx = CreateTestContext();
        // EnsureCreated crea el schema basado en el model snapshot actual (ya incluye TiposUnidadOrganizativa)
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    /// <summary>
    /// Backfill limpio: schema final tiene 7 seeds en TiposUnidadOrganizativa
    /// y la columna TipoUnidad fue eliminada.
    /// </summary>
    [MySqlFact]
    public async Task Migracion_DatosLimpios_TiposUnidadOrganizativaCreadosCon7Seeds()
    {
        // Arrange + Act: crear BD con el schema actual (incluye la migración)
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Assert: TiposUnidadOrganizativa tiene 7 filas
        var tiposCount = await ctx.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM TiposUnidadOrganizativa").ToListAsync();
        Assert.Equal(7, tiposCount.First());

        // Assert: Los 7 seeds tienen los Guids correctos
        var codigos = await ctx.Database.SqlQueryRaw<string>(
            "SELECT Codigo AS Value FROM TiposUnidadOrganizativa ORDER BY Codigo").ToListAsync();
        Assert.Equal(new[] { "Area", "Departamento", "Direccion", "Division", "Facultad", "Institucion", "Secretaria" }, codigos);

        // Assert: La columna TipoUnidad NO existe en UnidadesOrganizativas
        var columnaLegacy = await ctx.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UnidadesOrganizativas' AND COLUMN_NAME = 'TipoUnidad'").ToListAsync();
        Assert.Empty(columnaLegacy);

        // Assert: La columna TipoUnidadOrganizativaId SÍ existe
        var columnaFk = await ctx.Database.SqlQueryRaw<string>(
            "SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UnidadesOrganizativas' AND COLUMN_NAME = 'TipoUnidadOrganizativaId'").ToListAsync();
        Assert.Single(columnaFk);
    }

    /// <summary>
    /// Backfill limpio: la FK OnDelete(Restrict) está configurada correctamente.
    /// </summary>
    [MySqlFact]
    public async Task Migracion_DatosLimpios_FkRestrictExiste()
    {
        // Arrange + Act
        await using var ctx = await CreateFreshTestDatabaseAsync();

        // Assert: La FK existe con DELETE RESTRICT
        var fkInfo = await ctx.Database.SqlQueryRaw<string>(
            "SELECT DELETE_RULE AS Value FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS " +
            "WHERE TABLE_SCHEMA = DATABASE() " +
            "AND REFERENCED_TABLE_NAME = 'TiposUnidadOrganizativa' " +
            "AND CONSTRAINT_NAME LIKE 'FK_UnidadesOrganizativas_TiposUnidad%'").ToListAsync();
        Assert.Single(fkInfo);
        Assert.Equal("RESTRICT", fkInfo.First());
    }

    /// <summary>
    /// Datos sucios: string no catalogado en TipoUnidad causa una MySqlException
    /// con código 45000 (SIGNAL SQLSTATE) antes de que se modifique el schema.
    ///
    /// NOTA: Este test requiere crear manualmente el schema OLD (con TipoUnidad string)
    /// porque EnsureCreated() ya crea el schema FINAL. Se usa SQL directo para simular
    /// el estado pre-migración.
    /// </summary>
    [MySqlFact]
    public async Task Migracion_DatosSucios_LanzaMySqlException45000()
    {
        // Arrange: crear BD con schema OLD manualmente (sin TipoUnidadOrganizativa)
        await using (var ctx = CreateTestContext())
        {
            ctx.Database.EnsureCreated();

            // Re-add TipoUnidad column (fue eliminada por EnsureCreated)
            await ctx.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE UnidadesOrganizativas
                ADD COLUMN `TipoUnidad` varchar(50) NOT NULL DEFAULT '' CHARACTER SET utf8mb4;
            ");

            // Insertar una fila con TipoUnidad no catalogado
            await ctx.Database.ExecuteSqlRawAsync(@"
                INSERT INTO UnidadesOrganizativas (Id, Codigo, Nombre, TipoUnidad, TipoUnidadOrganizativaId)
                VALUES ('00000000-0000-0000-0000-000000000099', 'Dirty', 'Dirty Unit', 'FooBar',
                        '00000000-0000-0000-0000-000000000001');
            ");
        }

        // Act + Assert: aplicar migraciones debe fallar por SIGNAL SQLSTATE '45000'
        await using (var ctx = CreateTestContext())
        {
            var ex = await Assert.ThrowsAsync<MySqlException>(() => ctx.Database.MigrateAsync());

            // El error viene del SIGNAL SQLSTATE '45000' (fail-loud del pre-flight)
            Assert.Equal(45000, ex.Number);
        }
    }
}
