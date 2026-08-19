using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SGV.Infraestructura.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Schema assertions for the <c>AddRefreshTokens</c> migration introduced by
/// PR1b (change <c>implementa-refresh-tokens</c>). PR1b ships the
/// <see cref="RefreshTokenEntity"/> POCO + fluent config but the actual DDL
/// lands in this migration. These tests assert the runtime schema matches the
/// shape documented in design §4 (DDL explícito MySQL) and the
/// idempotent script in <c>docs/migracion-add-refresh-tokens.sql</c>:
///
/// <list type="bullet">
///   <item>Table <c>RefreshTokens</c> with charset <c>utf8mb4</c>.</item>
///   <item><c>Id</c> as <c>char(36)</c> PK, <c>ascii_general_ci</c>.</item>
///   <item><c>UserId</c> <c>varchar(450)</c> FK → <c>AspNetUsers.Id</c>, <c>ON DELETE CASCADE</c>.</item>
///   <item><c>FamilyId</c> <c>char(36)</c> <c>ascii_general_ci</c>.</item>
///   <item><c>TokenHash</c> <c>varchar(64)</c> <c>utf8mb4</c>.</item>
///   <item><c>ReplacedById</c> nullable <c>char(36)</c> — PR1b design
///         deviation: el self-FK <c>FK_RefreshTokens_RefreshTokens_ReplacedById</c>
///         con <c>ON DELETE RESTRICT</c> del design §4 fue removido.
///         Motivo: MySQL no soporta FKs diferidas y la rotación atómica
///         (UPDATE del viejo + INSERT del nuevo) necesita escribir
///         <c>ReplacedById = newId</c> antes de que la fila con
///         <c>Id = newId</c> exista. La integridad de la cadena la
///         mantiene <c>FamilyId</c> + <c>IX_RefreshTokens_ReplacedById</c>.</item>
///   <item>Datetime columns <c>datetime(6)</c>.</item>
///   <item>Indexes: UNIQUE <c>IX_RefreshTokens_TokenHash</c>, <c>IX_RefreshTokens_UserId</c>,
///         <c>IX_RefreshTokens_FamilyId</c>, <c>IX_RefreshTokens_ReplacedById</c>.</item>
/// </list>
///
/// Tagged <c>[MySqlFact]</c> so the bootstrap's <c>Database.Migrate()</c>
/// applies the pending migration automatically — the test then reads
/// <c>information_schema</c> directly.
/// </summary>
public sealed class RefreshTokensMigrationTests
{
    private const string TableName = "RefreshTokens";

    [MySqlFact]
    public async Task Tabla_ExisteDespuesDeMigrar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var existe = await TableExistsAsync(context, TableName);

        Assert.True(
            existe,
            $"La tabla {TableName} debe existir tras aplicar AddRefreshTokens.");
    }

    [MySqlFact]
    public async Task Tabla_TieneLasNueveColumnasEsperadas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var columnas = await GetColumnsAsync(context, TableName);

        // 9 columnas: Id, UserId, FamilyId, TokenHash, CreatedAt, ExpiresAt,
        // RevokedAt, ReplacedById, LastUsedAt.
        Assert.Equal(9, columnas.Count);

        AssertColumn(columnas, "Id", "char", isNullable: false);
        AssertColumn(columnas, "UserId", "varchar", isNullable: false);
        AssertColumn(columnas, "FamilyId", "char", isNullable: false);
        AssertColumn(columnas, "TokenHash", "varchar", isNullable: false);
        AssertColumn(columnas, "CreatedAt", "datetime", isNullable: false);
        AssertColumn(columnas, "ExpiresAt", "datetime", isNullable: false);
        AssertColumn(columnas, "RevokedAt", "datetime", isNullable: true);
        AssertColumn(columnas, "ReplacedById", "char", isNullable: true);
        AssertColumn(columnas, "LastUsedAt", "datetime", isNullable: false);
    }

    [MySqlFact]
    public async Task Tabla_TieneCharSetUtf8mb4()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var createTable = await GetCreateTableSqlAsync(context, TableName);

        // MySQL expresa el charset de tabla como `DEFAULT CHARSET=utf8mb4`
        // en el bloque final del SHOW CREATE TABLE; Pomelo lo emite así
        // cuando no se setea explícitamente el charset de columna a utf8mb4.
        // Aceptamos ambas formas para tolerar el formato exacto de MySQL 8.
        Assert.Matches(
            new Regex("(DEFAULT )?CHARSET=utf8mb4", RegexOptions.IgnoreCase),
            createTable);
    }

    [MySqlFact]
    public async Task Tabla_TieneUnIndiceUnicoSobreTokenHash()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var indices = await GetIndexesAsync(context, TableName);
        var uniqueTokenHash = indices
            .SingleOrDefault(i => i.IndexName == "IX_RefreshTokens_TokenHash");

        Assert.NotNull(uniqueTokenHash);
        Assert.True(uniqueTokenHash!.IsUnique);
        Assert.Equal("TokenHash", uniqueTokenHash.ColumnName);
    }

    [MySqlFact]
    public async Task Tabla_TieneIndicesSobreUserIdFamilyIdYReplacedById()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var indices = await GetIndexesAsync(context, TableName);
        var nombres = indices.Select(i => i.IndexName).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("IX_RefreshTokens_UserId", nombres);
        Assert.Contains("IX_RefreshTokens_FamilyId", nombres);
        // Índice declarativo sobre ReplacedById: aunque ya no hay FK, el
        // índice sigue siendo útil para las queries "¿qué token
        // reemplazó a este?" O(log n).
        Assert.Contains("IX_RefreshTokens_ReplacedById", nombres);
    }

    [MySqlFact]
    public async Task Tabla_TieneForeignKeyACascadeSobreAspNetUsers()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var fks = await GetForeignKeysAsync(context, TableName);
        var fkUser = fks.SingleOrDefault(fk =>
            fk.ConstraintName == "FK_RefreshTokens_AspNetUsers_UserId");

        Assert.NotNull(fkUser);
        Assert.Equal("AspNetUsers", fkUser!.ReferencedTable);
        Assert.Equal("Id", fkUser.ReferencedColumn);
        Assert.Equal("CASCADE", fkUser.OnDelete);
    }

    [MySqlFact]
    public async Task Tabla_NoTieneSelfForeignKeySobreReplacedById()
    {
        // PR1b design deviation: el self-FK sobre ReplacedById fue removido
        // porque MySQL no soporta FKs diferidas y la rotación atómica
        // necesita escribir ReplacedById antes del INSERT de la fila que lo
        // referencia. Ver <see cref="RefreshTokenConfiguracion"/> para la
        // justificación completa.
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var fks = await GetForeignKeysAsync(context, TableName);
        var fkSelf = fks.SingleOrDefault(fk =>
            fk.ConstraintName == "FK_RefreshTokens_RefreshTokens_ReplacedById");

        Assert.Null(fkSelf);
    }

    [MySqlFact]
    public async Task Tabla_CuandoSeInsertaUnTokenHashDuplicado_LanzaError1062DeMySql()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var primer = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: Guid.NewGuid());

        fixture.Context.RefreshTokens.Add(RefreshTokenEntityAdapter.FromSnapshot(primer));
        await fixture.Context.SaveChangesAsync();

        var duplicado = primer with { Id = Guid.NewGuid() };
        fixture.Context.RefreshTokens.Add(RefreshTokenEntityAdapter.FromSnapshot(duplicado));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            async () => await fixture.Context.SaveChangesAsync());

        Assert.NotNull(ex.InnerException);
        Assert.Contains("Duplicate entry", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> TableExistsAsync(SgvDbContext context, string table)
    {
        var conn = (MySqlConnection)context.Database.GetDbConnection();
        await EnsureOpenAsync(conn);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = DATABASE() AND table_name = @t";
        cmd.Parameters.AddWithValue("@t", table);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task<List<ColumnInfo>> GetColumnsAsync(SgvDbContext context, string table)
    {
        var conn = (MySqlConnection)context.Database.GetDbConnection();
        await EnsureOpenAsync(conn);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = @t
            ORDER BY ordinal_position";
        cmd.Parameters.AddWithValue("@t", table);

        var columns = new List<ColumnInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo(
                ColumnName: reader.GetString(0),
                DataType: reader.GetString(1),
                IsNullable: reader.GetString(2) == "YES"));
        }
        return columns;
    }

    private static async Task<string> GetCreateTableSqlAsync(SgvDbContext context, string table)
    {
        var conn = (MySqlConnection)context.Database.GetDbConnection();
        await EnsureOpenAsync(conn);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SHOW CREATE TABLE `{table}`";
        // SHOW CREATE TABLE retorna dos columnas: Table (nombre) y Create
        // Table (DDL). ExecuteScalarAsync sólo toma la primera — usamos
        // ExecuteReaderAsync para llegar al DDL.
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var createTable = reader.IsDBNull(1) ? null : reader.GetString(1);
        Assert.False(string.IsNullOrWhiteSpace(createTable));
        return createTable!;
    }

    private static async Task<List<IndexInfo>> GetIndexesAsync(SgvDbContext context, string table)
    {
        var conn = (MySqlConnection)context.Database.GetDbConnection();
        await EnsureOpenAsync(conn);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SHOW INDEX FROM `{table}`";

        var indexes = new Dictionary<string, IndexInfo>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(2);
            var column = reader.GetString(4);
            var isUnique = reader.GetInt64(1) == 0;
            if (!indexes.TryGetValue(name, out var info))
            {
                info = new IndexInfo(name, isUnique, column);
                indexes[name] = info;
            }
        }
        return indexes.Values.ToList();
    }

    private static async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(SgvDbContext context, string table)
    {
        var conn = (MySqlConnection)context.Database.GetDbConnection();
        await EnsureOpenAsync(conn);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT rc.constraint_name,
                   rc.referenced_table_name,
                   kcu.referenced_column_name,
                   rc.delete_rule
            FROM information_schema.referential_constraints rc
            JOIN information_schema.key_column_usage kcu
              ON kcu.constraint_schema = rc.constraint_schema
             AND kcu.constraint_name = rc.constraint_name
            WHERE rc.constraint_schema = DATABASE()
              AND rc.table_name = @t
            ORDER BY rc.constraint_name, kcu.ordinal_position";
        cmd.Parameters.AddWithValue("@t", table);

        var fks = new List<ForeignKeyInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fks.Add(new ForeignKeyInfo(
                ConstraintName: reader.GetString(0),
                ReferencedTable: reader.GetString(1),
                ReferencedColumn: reader.GetString(2),
                OnDelete: reader.GetString(3)));
        }
        return fks;
    }

    private static async Task EnsureOpenAsync(MySqlConnection conn)
    {
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }
    }

    private static void AssertColumn(
        List<ColumnInfo> columns,
        string columnName,
        string expectedDataType,
        bool isNullable)
    {
        var col = columns.SingleOrDefault(c => c.ColumnName == columnName);
        Assert.NotNull(col);
        Assert.Equal(isNullable, col!.IsNullable);
        Assert.Equal(
            expectedDataType,
            col.DataType,
            ignoreCase: true);
    }

    private sealed record ColumnInfo(string ColumnName, string DataType, bool IsNullable);

    private sealed record IndexInfo(string IndexName, bool IsUnique, string ColumnName);

    private sealed record ForeignKeyInfo(
        string ConstraintName,
        string ReferencedTable,
        string ReferencedColumn,
        string OnDelete);
}