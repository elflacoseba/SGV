using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests [MySqlFact] de la migración
/// <c>20260723203015_AddCategoriaHabilidadCatalog</c> (issue
/// migrar-campo-categoria-habilidades-a-tabla). Cubre:
///   - Estructura de la tabla <c>CategoriasHabilidad</c>.
///   - Seed de 4 filas con IDs del bloque 72000000-…
///   - FK Restrict entre Habilidades.CategoriaId y CategoriasHabilidad.Id.
///   - Backfill case-insensitive de <c>LOWER(Categoria)</c>.
///   - Variante opt-in relajada: valores sucios quedan en NULL con
///     auditoría para remediación post-deploy.
///   - DROP COLUMN de la columna legacy Categoria.
///   - DROP INDEX IX_Habilidades_Categoria + CREATE INDEX
///     IX_Habilidades_CategoriaId.
/// </summary>
public sealed class CategoriaHabilidadMigracionTests : IAsyncLifetime
{
    private string _testDbName = null!;
    private string _testConnectionString = null!;

    public Task InitializeAsync()
    {
        _testDbName = $"sgv_ch_{Guid.NewGuid():N}"[..12];
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

    private async Task<SgvDbContext> CreatePreMigracionContextAsync()
    {
        // Aplica todas las migraciones previas hasta justo antes de la nuestra.
        // La pre-migración tiene Habilidades.Categoria (string) sin FK.
        await using var ctx = CreateTestContext();
        await ctx.Database.MigrateAsync("20260720230343_TipoDocumentoCatalogoYPersonaFk");
        return ctx;
    }

    private static async Task SeedHabilidadLegacyAsync(
        string connectionString,
        Guid id,
        string codigo,
        string categoriaLegacy,
        string nombre = "X")
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Habilidades (Id, Codigo, Nombre, Categoria, IsActive, IsDeleted, CreatedAt)
            VALUES (@id, @codigo, @nombre, @categoria, 1, 0, NOW(6))";
        cmd.Parameters.AddWithValue("@id", id.ToString("D"));
        cmd.Parameters.AddWithValue("@codigo", codigo);
        cmd.Parameters.AddWithValue("@nombre", nombre);
        cmd.Parameters.AddWithValue("@categoria", categoriaLegacy);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountRowsAsync(string connectionString, string sql)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static async Task<string?> ScalarStringAsync(string connectionString, string sql)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    private static async Task<bool> ColumnExistsAsync(string connectionString, string table, string column)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = @t AND column_name = @c";
        cmd.Parameters.AddWithValue("@t", table);
        cmd.Parameters.AddWithValue("@c", column);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<bool> IndexExistsAsync(string connectionString, string table, string index)
    {
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.statistics
            WHERE table_schema = DATABASE() AND table_name = @t AND index_name = @i";
        cmd.Parameters.AddWithValue("@t", table);
        cmd.Parameters.AddWithValue("@i", index);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    // ── Estructura y seed post-migración ─────────────────────────

    [MySqlFact]
    public async Task Migracion_PostEstructura_TablaCategoriasHabilidadExiste()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        var count = await CountRowsAsync(_testConnectionString,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'CategoriasHabilidad'");
        Assert.True(count > 0, "Tabla CategoriasHabilidad debe existir post-migración.");
    }

    [MySqlFact]
    public async Task Migracion_PostSeed_Contiene4Filas()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        var count = await CountRowsAsync(_testConnectionString, "SELECT COUNT(*) FROM CategoriasHabilidad");
        Assert.Equal(4, count);
    }

    [MySqlFact]
    public async Task Migracion_PostSeed_IdsCorrespondenACteHabilidadConstantes()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        var conduccion = await ScalarStringAsync(_testConnectionString,
            $"SELECT CAST(Id AS CHAR(36)) FROM CategoriasHabilidad WHERE Codigo = 'Conduccion'");
        Assert.Equal(CategoriaHabilidadConstantes.ConduccionId.ToString("D"), conduccion);

        var tecnica = await ScalarStringAsync(_testConnectionString,
            $"SELECT CAST(Id AS CHAR(36)) FROM CategoriasHabilidad WHERE Codigo = 'Tecnica'");
        Assert.Equal(CategoriaHabilidadConstantes.TecnicaId.ToString("D"), tecnica);

        var dominio = await ScalarStringAsync(_testConnectionString,
            $"SELECT CAST(Id AS CHAR(36)) FROM CategoriasHabilidad WHERE Codigo = 'Dominio'");
        Assert.Equal(CategoriaHabilidadConstantes.DominioId.ToString("D"), dominio);

        var academica = await ScalarStringAsync(_testConnectionString,
            $"SELECT CAST(Id AS CHAR(36)) FROM CategoriasHabilidad WHERE Codigo = 'Academica'");
        Assert.Equal(CategoriaHabilidadConstantes.AcademicaId.ToString("D"), academica);
    }

    // ── Backfill ───────────────────────────────────────────────

    [MySqlFact]
    public async Task Migracion_BackfillCategoriaMatch_AsignaGuid()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await SeedHabilidadLegacyAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000001"), "HAB-001", "Conducción", "Habilidad 1");
        await SeedHabilidadLegacyAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000002"), "HAB-002", "Técnica", "Habilidad 2");
        await SeedHabilidadLegacyAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000003"), "HAB-003", "Dominio", "Habilidad 3");

        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        var id1 = await ScalarStringAsync(_testConnectionString,
            "SELECT CAST(CategoriaId AS CHAR(36)) FROM Habilidades WHERE Id = '00000000-0000-0000-0000-000000000001'");
        Assert.Equal(CategoriaHabilidadConstantes.ConduccionId.ToString("D"), id1);

        var id2 = await ScalarStringAsync(_testConnectionString,
            "SELECT CAST(CategoriaId AS CHAR(36)) FROM Habilidades WHERE Id = '00000000-0000-0000-0000-000000000002'");
        Assert.Equal(CategoriaHabilidadConstantes.TecnicaId.ToString("D"), id2);

        var id3 = await ScalarStringAsync(_testConnectionString,
            "SELECT CAST(CategoriaId AS CHAR(36)) FROM Habilidades WHERE Id = '00000000-0000-0000-0000-000000000003'");
        Assert.Equal(CategoriaHabilidadConstantes.DominioId.ToString("D"), id3);
    }

    [MySqlFact]
    public async Task Migracion_BackfillCategoriaSucia_QuedaNullYAuditoria()
    {
        // Variante opt-in relajada: valores legacy no catalogados caen a NULL
        // con auditoría en Auditorias (Origen, CategoriaOriginal).
        await using var ctx = await CreatePreMigracionContextAsync();
        await SeedHabilidadLegacyAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000010"), "HAB-DIRTY", "Otra Cosa");
        await SeedHabilidadLegacyAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000011"), "HAB-NULL", null);

        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        var idDirty = await ScalarStringAsync(_testConnectionString,
            "SELECT CAST(CategoriaId AS CHAR(36)) FROM Habilidades WHERE Id = '00000000-0000-0000-0000-000000000010'");
        Assert.Null(idDirty);

        var idNull = await ScalarStringAsync(_testConnectionString,
            "SELECT CAST(CategoriaId AS CHAR(36)) FROM Habilidades WHERE Id = '00000000-0000-0000-0000-000000000011'");
        Assert.Null(idNull);

        var auditCount = await CountRowsAsync(_testConnectionString,
            @"SELECT COUNT(*) FROM Auditorias
              WHERE EntityName = 'Habilidad'
                AND Operation = 'BackfillLegacyCategoriaToNull'
                AND JSON_EXTRACT(NewValuesJson, '$.Origen') = 'Migracion.AddCategoriaHabilidadCatalog'");
        Assert.True(auditCount >= 1, "Debe haber al menos 1 fila de auditoría para el backfill.");
    }

    // ── FK Restrict ────────────────────────────────────────────

    [MySqlFact]
    public async Task Migracion_PostFK_DeleteCategoriaEnUsoFalla()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await SeedHabilidadLegacyAsync(_testConnectionString,
            new Guid("00000000-0000-0000-0000-000000000020"), "HAB-LINK", "Conducción");

        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        await using var conn = new MySqlConnection(_testConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM CategoriasHabilidad WHERE Id = '{CategoriaHabilidadConstantes.ConduccionId:D}'";
        var ex = await Assert.ThrowsAsync<MySqlException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("FK_Habilidades_CategoriasHabilidad_CategoriaId", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── Drop de legacy + índices ───────────────────────────────

    [MySqlFact]
    public async Task Migracion_PostMigracion_ColumnaCategoriaEliminada()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        Assert.False(await ColumnExistsAsync(_testConnectionString, "Habilidades", "Categoria"),
            "La columna legacy Habilidades.Categoria debe haberse eliminado.");
    }

    [MySqlFact]
    public async Task Migracion_PostMigracion_IndiceCategoriaIdExiste()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        Assert.True(await IndexExistsAsync(_testConnectionString, "Habilidades", "IX_Habilidades_CategoriaId"));
    }

    [MySqlFact]
    public async Task Migracion_PostMigracion_IndiceLegacyCategoriaEliminado()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        Assert.False(await IndexExistsAsync(_testConnectionString, "Habilidades", "IX_Habilidades_Categoria"),
            "El índice legacy IX_Habilidades_Categoria debe haberse eliminado.");
    }

    // ── Idempotencia ───────────────────────────────────────────

    [MySqlFact]
    public async Task Migracion_SegundaCorrida_NoFalla()
    {
        // Aplicar la migración dos veces seguidas: la segunda no debe fallar.
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator1 = CreateTestContext();
        await migrator1.Database.MigrateAsync();
        await using var migrator2 = CreateTestContext();
        await migrator2.Database.MigrateAsync();

        var count = await CountRowsAsync(_testConnectionString, "SELECT COUNT(*) FROM CategoriasHabilidad");
        Assert.Equal(4, count);
    }

    // ── Down forward-only ──────────────────────────────────────

    [MySqlFact]
    public async Task Migracion_Down_LanzaNotSupportedException()
    {
        await using var ctx = await CreatePreMigracionContextAsync();
        await using var migrator = CreateTestContext();
        await migrator.Database.MigrateAsync();

        // Intentar revertir la migración. Down() lanza NotSupportedException
        // (forward-only por diseño; precedente FixActivePuestoIdUniqueType).
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => migrator.Database.MigrateAsync("0"));
        Assert.Contains("forward-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}