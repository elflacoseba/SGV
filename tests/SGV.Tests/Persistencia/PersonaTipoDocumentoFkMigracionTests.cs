using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests [MySqlFact] de la migración issue #147 (catálogo <c>TipoDocumento</c>
/// + FK en <c>Persona</c> + recreación de <c>ActiveDocumentoUnique</c>).
///
/// Cubre:
///   - Backfill: valores legacy matcheados a GUID del seed.
///   - Backfill sucio: valores no catalogados quedan con la FK en NULL y
///     NumeroDocumento preservado (variante opt-in relajada de
///     REQ-SPA-EVOLUTION-001 condición #3).
///   - Índice único activo: la nueva fórmula CONCAT preserva unicidad.
///   - FK OnDelete(Restrict): el delete de un catalogado referenciado falla.
/// </summary>
public sealed class PersonaTipoDocumentoFkMigracionTests : IAsyncLifetime
{
    private string _testDbName = null!;
    private string _testConnectionString = null!;

    public Task InitializeAsync()
    {
        // MySQL limita el nombre de los user-level locks a 64 chars; el
        // prefijo "__<dbname>_EFMigrationsLock" se completa solo. Usar un
        // nombre corto para evitar truncado.
        _testDbName = $"sgv_td_{Guid.NewGuid():N}"[..12];
        _testConnectionString = TestSgvDbContextFactory.BuildConnectionStringForDatabase(_testDbName);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var masterConn = new MySqlConnection(TestSgvDbContextFactory.ResolveConnectionString());
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
        // Crear el schema pre-migración (todo lo previo a #147) sin aplicar
        // la migración bajo prueba. MigrateAsync upTo <previa> deja la DB en
        // el estado donde TipoDocumento es string.
        await ctx.Database.MigrateAsync("20260719180541_AddPersonasNumeroDocumentoIndex");
        return ctx;
    }

    private static async Task SeedPersonaAsync(string connectionString, Guid id, string legajo, string tipoDocumentoLegacy, string numeroDocumento)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Personas (Id, Nombres, Apellidos, Legajo, TipoDocumento, NumeroDocumento, IsActive, IsDeleted, CreatedAt)
            VALUES (@id, 'X', 'Y', @legajo, @tipo, @num, 1, 0, NOW(6))";
        cmd.Parameters.AddWithValue("@id", id.ToString("D"));
        cmd.Parameters.AddWithValue("@legajo", legajo);
        cmd.Parameters.AddWithValue("@tipo", tipoDocumentoLegacy);
        cmd.Parameters.AddWithValue("@num", numeroDocumento);
        await cmd.ExecuteNonQueryAsync();
    }

    [MySqlFact]
    public async Task Migracion_BackfillTipoDocumentoConocido_AsignaGuid()
    {
        // Arrange: arrancar con un schema pre-migración (Personas.TipoDocumento string).
        await using var ctx = await CreateFreshTestDatabaseAsync();

        await SeedPersonaAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000001"), "LEG-001", "DNI", "12345678");
        await SeedPersonaAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000002"), "LEG-002", "Pasaporte", "ABC123456");

        // Act: aplicar la migración de #147.
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        // Assert: TipoDocumentoId FK quedó mapeada a los Guids del seed.
        await using var conn = new MySqlConnection(_testConnectionString);
        await conn.OpenAsync();

        await using (var cmd1 = conn.CreateCommand())
        {
            cmd1.CommandText = "SELECT CAST(TipoDocumentoId AS CHAR(36)) FROM Personas WHERE Id = '00000000-0000-0000-0000-000000000001'";
            var dniId = (string?)await cmd1.ExecuteScalarAsync();
            Assert.Equal(TipoDocumentoConstantes.DniId.ToString("D"), dniId);
        }

        await using (var cmd2 = conn.CreateCommand())
        {
            cmd2.CommandText = "SELECT CAST(TipoDocumentoId AS CHAR(36)) FROM Personas WHERE Id = '00000000-0000-0000-0000-000000000002'";
            var pasaporteId = (string?)await cmd2.ExecuteScalarAsync();
            Assert.Equal(TipoDocumentoConstantes.PasaporteId.ToString("D"), pasaporteId);
        }
    }

    [MySqlFact]
    public async Task Migracion_BackfillTipoDocumentoSucio_TipoDocumentoIdQuedaNull()
    {
        // Arrange: insertar una persona con TipoDocumento sucio (no es seed).
        await using var ctx = await CreateFreshTestDatabaseAsync();

        await SeedPersonaAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-0000000000ff"), "LEG-SUCIO", "FooBar", "99999");

        // Act: aplicar la migración.
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        // Assert: TipoDocumentoId quedó NULL (política opt-in relajada).
        // ExecuteScalarAsync devuelve DBNull.Value para columnas NULL; usar
        // IsDBNull para distinguir.
        await using var conn = new MySqlConnection(_testConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TipoDocumentoId FROM Personas WHERE Id = '00000000-0000-0000-0000-0000000000ff'";
        var tipoDocId = await cmd.ExecuteScalarAsync();
        Assert.True(tipoDocId is null || tipoDocId is DBNull,
            $"Expected NULL or DBNull, got: '{tipoDocId}' ({tipoDocId?.GetType().FullName})");
    }

    [MySqlFact]
    public async Task Migracion_BackfillTipoDocumentoSucio_NumeroDocumentoPreservado()
    {
        // Arrange: persona con TipoDocumento sucio + NumeroDocumento.
        await using var ctx = await CreateFreshTestDatabaseAsync();

        await SeedPersonaAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-0000000000fe"), "LEG-NUM", "BarBaz", "77777");

        // Act.
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        // Assert: NumeroDocumento se preserva intacto.
        await using var conn = new MySqlConnection(_testConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT NumeroDocumento FROM Personas WHERE Id = '00000000-0000-0000-0000-0000000000fe'";
        var numDoc = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("77777", numDoc);
    }

    [MySqlFact]
    public async Task Migracion_IndiceActiveDocumentoUnique_RecreadoConNuevaFormula()
    {
        // Arrange: arrancar pre-migración e insertar una persona con DNI.
        await using var ctx = await CreateFreshTestDatabaseAsync();

        await SeedPersonaAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-0000000000a1"), "LEG-A1", "DNI", "12345678");

        // Act: aplicar la migración.
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        // Assert: el índice único activo rechaza el duplicado post-migración.
        await using var conn = new MySqlConnection(_testConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Personas (Id, Nombres, Apellidos, Legajo, TipoDocumentoId, NumeroDocumento, IsActive, IsDeleted, CreatedAt)
            VALUES ('00000000-0000-0000-0000-0000000000a2', 'B', 'Y', 'LEG-A2',
                    '71000000-0000-0000-0000-000000000001', '12345678', 1, 0, NOW(6))";
        await Assert.ThrowsAsync<MySqlException>(() => cmd.ExecuteNonQueryAsync());
    }

    [MySqlFact]
    public async Task FK_OnDeleteRestrict_RechazaEliminarCatalogado()
    {
        // Arrange: arrancar con el seed cargado y una persona que lo referencia.
        await using var ctx = await CreateFreshTestDatabaseAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        await using (var conn = new MySqlConnection(_testConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Personas (Id, Nombres, Apellidos, Legajo, TipoDocumentoId, NumeroDocumento, IsActive, IsDeleted, CreatedAt)
                VALUES ('00000000-0000-0000-0000-0000000000b1', 'C', 'Z', 'LEG-B1',
                        '71000000-0000-0000-0000-000000000001', '11111111', 1, 0, NOW(6))";
            await cmd.ExecuteNonQueryAsync();
        }

        // Act + Assert: el DELETE del TipoDocumento referenciado falla.
        await using (var conn = new MySqlConnection(_testConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM TiposDocumento WHERE Id = '71000000-0000-0000-0000-000000000001'";
            await Assert.ThrowsAsync<MySqlException>(() => cmd.ExecuteNonQueryAsync());
        }
    }
}
