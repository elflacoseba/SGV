using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Integration tests against MySQL for <see cref="RefreshTokenRepository"/>.
/// PR1b (change <c>implementa-refresh-tokens</c>) introduces the
/// persistence-layer repository that backs the refresh-token rotation flow.
/// REQs covered (spec block B):
/// <list type="bullet">
///   <item>REQ-RTM-STORE-1 — persistencia MySQL con migración versionada.</item>
///   <item>REQ-RTM-ROTATION-1 — rotación single-use por familia.</item>
///   <item>REQ-RTM-REPLAY-1 — detección de replay.</item>
///   <item>REQ-RTM-FAMILY-1 — familia identifica la cadena de rotaciones.</item>
/// </list>
/// </summary>
public sealed class RefreshTokenRepositoryTests
{
    [MySqlFact]
    public async Task AddAsync_ThenGetByHashAsync_DevuelveSnapshotConTodosLosCampos()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var familyId = fixture.TrackFamilyId(Guid.NewGuid());
        var snapshot = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: familyId);

        var repo = new RefreshTokenRepository(context);
        await repo.AddAsync(snapshot, default);
        await context.SaveChangesAsync();

        var leido = await repo.GetByHashAsync(snapshot.TokenHash, default);

        Assert.NotNull(leido);
        Assert.Equal(snapshot.Id, leido!.Id);
        Assert.Equal(snapshot.UserId, leido.UserId);
        Assert.Equal(snapshot.FamilyId, leido.FamilyId);
        Assert.Equal(snapshot.TokenHash, leido.TokenHash);
        Assert.Equal(snapshot.CreatedAt, leido.CreatedAt);
        Assert.Equal(snapshot.ExpiresAt, leido.ExpiresAt);
        Assert.Null(leido.RevokedAt);
        Assert.Null(leido.ReplacedById);
        Assert.Equal(snapshot.LastUsedAt, leido.LastUsedAt);
    }

    [MySqlFact]
    public async Task GetByHashAsync_CuandoElHashNoExiste_DevuelveNull()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new RefreshTokenRepository(context);

        var resultado = await repo.GetByHashAsync(new string('z', 64), default);

        Assert.Null(resultado);
    }

    [MySqlFact]
    public async Task TryConsumeAsync_TokenActivo_MarcaRevokedAtYReplacedById()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var familyId = fixture.TrackFamilyId(Guid.NewGuid());
        var original = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: familyId);
        context.RefreshTokens.Add(RefreshTokenEntityAdapter.FromSnapshot(original));
        await context.SaveChangesAsync();

        var repo = new RefreshTokenRepository(context);
        var nowUtc = original.LastUsedAt.AddMinutes(1);
        var nuevoId = Guid.NewGuid();
        var consumido = await repo.TryConsumeAsync(original.TokenHash, nuevoId, nowUtc, default);

        Assert.True(consumido);

        var filaActualizada = await repo.GetByHashAsync(original.TokenHash, default);
        Assert.NotNull(filaActualizada);
        Assert.Equal(nowUtc, filaActualizada!.RevokedAt);
        Assert.Equal(nuevoId, filaActualizada.ReplacedById);
        Assert.Equal(nowUtc, filaActualizada.LastUsedAt);
    }

    [MySqlFact]
    public async Task TryConsumeAsync_TokenYaRevocado_DevuelveFalse()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var familyId = fixture.TrackFamilyId(Guid.NewGuid());
        var original = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: familyId);
        context.RefreshTokens.Add(RefreshTokenEntityAdapter.FromSnapshot(original));
        await context.SaveChangesAsync();

        var repo = new RefreshTokenRepository(context);
        var t0 = original.LastUsedAt.AddMinutes(1);
        var primerIntento = await repo.TryConsumeAsync(original.TokenHash, Guid.NewGuid(), t0, default);
        Assert.True(primerIntento);

        var segundoIntento = await repo.TryConsumeAsync(
            original.TokenHash, Guid.NewGuid(), t0.AddMinutes(1), default);

        Assert.False(segundoIntento);
    }

    [MySqlFact]
    public async Task TryConsumeAsync_TokenExpirado_DevuelveFalse_YNoMutaLaFila()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var familyId = fixture.TrackFamilyId(Guid.NewGuid());
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expirado = RefreshTokenTestFixture.CrearSnapshotValido(
            userId: fixture.UserId,
            familyId: familyId,
            createdAt: t0,
            expiresAt: t0.AddMinutes(5),
            lastUsedAt: t0);
        context.RefreshTokens.Add(RefreshTokenEntityAdapter.FromSnapshot(expirado));
        await context.SaveChangesAsync();

        var repo = new RefreshTokenRepository(context);
        var nowUtc = t0.AddDays(1);
        var consumido = await repo.TryConsumeAsync(expirado.TokenHash, Guid.NewGuid(), nowUtc, default);

        Assert.False(consumido);

        // La fila NO se mutó: el WHERE ExpiresAt > @now la excluyó, así que
        // RevokedAt y ReplacedById permanecen null (REQ-AUTH-REFRESH-2).
        var filaIntacta = await repo.GetByHashAsync(expirado.TokenHash, default);
        Assert.NotNull(filaIntacta);
        Assert.Null(filaIntacta!.RevokedAt);
        Assert.Null(filaIntacta.ReplacedById);
    }

    [MySqlFact]
    public async Task RevokeFamilyAsync_RevocaTodosLosActivosDeLaFamilia()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var familiaObjetivo = fixture.TrackFamilyId(Guid.NewGuid());
        var familiaAjena = fixture.TrackFamilyId(Guid.NewGuid());

        var t1 = RefreshTokenTestFixture.CrearSnapshotValido(
            fixture.UserId, familiaObjetivo,
            createdAt: new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc),
            tokenHash: new string('1', 64));
        var t2 = RefreshTokenTestFixture.CrearSnapshotValido(
            fixture.UserId, familiaObjetivo,
            createdAt: new DateTime(2026, 8, 19, 11, 0, 0, DateTimeKind.Utc),
            tokenHash: new string('2', 64));
        var t3 = RefreshTokenTestFixture.CrearSnapshotValido(
            fixture.UserId, familiaObjetivo,
            createdAt: new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc),
            tokenHash: new string('3', 64));
        var otraFamilia = RefreshTokenTestFixture.CrearSnapshotValido(
            fixture.UserId, familiaAjena,
            createdAt: new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc),
            tokenHash: new string('4', 64));
        var revocadoPreviamente = RefreshTokenTestFixture.CrearSnapshotValido(
            fixture.UserId, familiaObjetivo,
            createdAt: new DateTime(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc),
            tokenHash: new string('5', 64));
        revocadoPreviamente = revocadoPreviamente with { RevokedAt = new DateTime(2026, 8, 19, 9, 30, 0, DateTimeKind.Utc) };

        await InsertarAsync(context, t1, t2, t3, otraFamilia, revocadoPreviamente);

        var repo = new RefreshTokenRepository(context);
        var when = new DateTime(2026, 8, 19, 13, 0, 0, DateTimeKind.Utc);
        var affected = await repo.RevokeFamilyAsync(familiaObjetivo, when, default);

        Assert.Equal(3, affected);

        // Las 3 filas activas de la familia objetivo están revocadas.
        Assert.Equal(when, (await repo.GetByHashAsync(t1.TokenHash, default))!.RevokedAt);
        Assert.Equal(when, (await repo.GetByHashAsync(t2.TokenHash, default))!.RevokedAt);
        Assert.Equal(when, (await repo.GetByHashAsync(t3.TokenHash, default))!.RevokedAt);

        // La fila de la otra familia NO fue tocada.
        Assert.Null((await repo.GetByHashAsync(otraFamilia.TokenHash, default))!.RevokedAt);

        // La fila ya revocada mantiene su RevokedAt original (el WHERE
        // RevokedAt IS NULL la excluye del UPDATE).
        Assert.Equal(
            new DateTime(2026, 8, 19, 9, 30, 0, DateTimeKind.Utc),
            (await repo.GetByHashAsync(revocadoPreviamente.TokenHash, default))!.RevokedAt);
    }

    [MySqlFact]
    public async Task RevokeAllForUserAsync_RevocaTodasLasFamiliasDelUsuario()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var fixture = await RefreshTokenTestFixture.CreateAsync(context);

        var fam1 = fixture.TrackFamilyId(Guid.NewGuid());
        var fam2 = fixture.TrackFamilyId(Guid.NewGuid());

        var t1 = RefreshTokenTestFixture.CrearSnapshotValido(
            fixture.UserId, fam1,
            createdAt: new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc),
            tokenHash: new string('6', 64));
        var t2 = RefreshTokenTestFixture.CrearSnapshotValido(
            fixture.UserId, fam2,
            createdAt: new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc),
            tokenHash: new string('7', 64));

        await InsertarAsync(context, t1, t2);

        var repo = new RefreshTokenRepository(context);
        var when = new DateTime(2026, 8, 19, 13, 0, 0, DateTimeKind.Utc);
        var affected = await repo.RevokeAllForUserAsync(fixture.UserId, when, default);

        Assert.Equal(2, affected);
        Assert.Equal(when, (await repo.GetByHashAsync(t1.TokenHash, default))!.RevokedAt);
        Assert.Equal(when, (await repo.GetByHashAsync(t2.TokenHash, default))!.RevokedAt);
    }

    private static async Task InsertarAsync(SgvDbContext context, params RefreshTokenSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            context.RefreshTokens.Add(RefreshTokenEntityAdapter.FromSnapshot(snapshot));
        }
        await context.SaveChangesAsync();
    }
}