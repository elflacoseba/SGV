using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Concurrency regression test for the atomic consume primitive of
/// <see cref="RefreshTokenRepository"/>.
/// REQ-RTM-CONCURRENCY-1 (spec block B): ante dos requests concurrentes
/// con el mismo <c>TokenHash</c>, a lo sumo uno retorna
/// <c>affected == 1</c>. El otro obtiene <c>affected == 0</c> porque el
/// <c>WHERE RevokedAt IS NULL AND ExpiresAt &gt; @now</c> ya no
/// matchea — esto es la primitiva de replay detection.
/// </summary>
/// <remarks>
/// Para reproducir la carrera de verdad necesitamos DOS
/// <see cref="SgvDbContext"/> independientes: con un solo DbContext,
/// EF Core serializa las operaciones internamente y el test sería
/// falsamente verde. Por eso cada task construye su propio DbContext a
/// partir de la misma connection string.
/// </remarks>
public sealed class RefreshTokenRepositoryConcurrentTests
{
    [MySqlFact]
    public async Task TryConsumeAsync_DosTasksConElMismoToken_SoloUnaGana()
    {
        // El bootstrap de MySqlFact corre Database.Migrate() antes del primer
        // test, así que la tabla RefreshTokens ya existe.
        var connectionString = TestSgvDbContextFactory.ResolveConnectionString();

        // Sembrar el token y la fixture en un DbContext dedicado al seed;
        // el fixture NO se debe disposear hasta el final del test para que
        // las dos tasks siguientes puedan leer el token sembrado.
        var familyId = Guid.NewGuid();
        await using var seedContext = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(seedContext);
        var userId = fixture.UserId;

        var snapshot = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: familyId,
            tokenHash: new string('c', 64));
        seedContext.RefreshTokens.Add(RefreshTokenEntityAdapter.FromSnapshot(snapshot));
        await seedContext.SaveChangesAsync();
        seedContext.ChangeTracker.Clear();

        // Dos DbContexts independientes, dos tasks paralelas que llaman
        // TryConsumeAsync con el mismo TokenHash y ReplacedById distintos.
        var task1 = TryConsumeEnContextoIndependiente(
            connectionString, snapshot.TokenHash, replacedById: Guid.NewGuid());

        var task2 = TryConsumeEnContextoIndependiente(
            connectionString, snapshot.TokenHash, replacedById: Guid.NewGuid());

        var resultados = await Task.WhenAll(task1, task2);

        // Exactamente una de las dos tareas debe haber consumido.
        Assert.Equal(1, resultados.Count(r => r));

        // La fila T1 ahora tiene RevokedAt != null y ReplacedById apunta
        // al ganador de la carrera (REQ-RTM-CONCURRENCY-1).
        var fila = await seedContext.RefreshTokens
            .AsNoTracking()
            .SingleAsync(r => r.TokenHash == snapshot.TokenHash);

        Assert.NotNull(fila.RevokedAt);
        Assert.NotNull(fila.ReplacedById);
    }

    private static async Task<bool> TryConsumeEnContextoIndependiente(
        string connectionString,
        string tokenHash,
        Guid replacedById)
    {
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        await using var context = new SgvDbContext(options);
        var repo = new RefreshTokenRepository(context);

        // El nowUtc debe estar dentro de la ventana de validez del token
        // sembrado (Creado=2026-08-19T12:00:00Z, Expira=14d después).
        return await repo.TryConsumeAsync(
            tokenHash,
            replacedById,
            nowUtc: new DateTime(2026, 8, 19, 12, 1, 0, DateTimeKind.Utc),
            default);
    }
}