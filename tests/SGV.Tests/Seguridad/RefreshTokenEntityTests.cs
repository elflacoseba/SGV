using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// Behaviour lock-down for <see cref="RefreshTokenEntity.Reconstitute"/>
/// and <see cref="RefreshTokenEntity.IsValid"/>.
///
/// PR1a (change <c>implementa-refresh-tokens</c>) introduces the
/// refresh-token rotation flow. The entity must expose a typed
/// factory so the persistence layer can hydrate rows without reflection
/// (REQ-124-1 spirit), and a pure validity predicate that the
/// refresh-service (PR2) and the row-lock tests (PR1b) can reuse.
/// </summary>
public sealed class RefreshTokenEntityTests
{
    [Fact]
    public void Reconstitute_AsignaTodasLasPropiedades()
    {
        var id = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var expiresAt = now.AddDays(14);
        var lastUsedAt = now.AddMinutes(5);
        var replacedById = Guid.NewGuid();

        var entity = RefreshTokenEntity.Reconstitute(
            id: id,
            userId: "user-id-123",
            familyId: familyId,
            tokenHash: new string('a', 64),
            createdAt: now,
            expiresAt: expiresAt,
            revokedAt: null,
            replacedById: replacedById,
            lastUsedAt: lastUsedAt);

        Assert.Equal(id, entity.Id);
        Assert.Equal("user-id-123", entity.UserId);
        Assert.Equal(familyId, entity.FamilyId);
        Assert.Equal(new string('a', 64), entity.TokenHash);
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(expiresAt, entity.ExpiresAt);
        Assert.Null(entity.RevokedAt);
        Assert.Equal(replacedById, entity.ReplacedById);
        Assert.Equal(lastUsedAt, entity.LastUsedAt);
    }

    [Fact]
    public void IsValid_NoRevocadoYExpiracionFutura_RetornaTrue()
    {
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        var entity = RefreshTokenEntity.Reconstitute(
            id: Guid.NewGuid(),
            userId: "user-1",
            familyId: Guid.NewGuid(),
            tokenHash: new string('b', 64),
            createdAt: now.AddDays(-1),
            expiresAt: now.AddDays(1),
            revokedAt: null,
            replacedById: null,
            lastUsedAt: now.AddHours(-2));

        Assert.True(entity.IsValid(now));
    }

    [Fact]
    public void IsValid_TokenRevocado_RetornaFalse()
    {
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        var entity = RefreshTokenEntity.Reconstitute(
            id: Guid.NewGuid(),
            userId: "user-1",
            familyId: Guid.NewGuid(),
            tokenHash: new string('c', 64),
            createdAt: now.AddDays(-1),
            expiresAt: now.AddDays(1),
            revokedAt: now.AddMinutes(-5),
            replacedById: null,
            lastUsedAt: now.AddHours(-2));

        Assert.False(entity.IsValid(now));
    }

    [Fact]
    public void IsValid_TokenExpirado_RetornaFalse()
    {
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        var entity = RefreshTokenEntity.Reconstitute(
            id: Guid.NewGuid(),
            userId: "user-1",
            familyId: Guid.NewGuid(),
            tokenHash: new string('d', 64),
            createdAt: now.AddDays(-30),
            expiresAt: now.AddSeconds(-1),
            revokedAt: null,
            replacedById: null,
            lastUsedAt: now.AddDays(-29));

        Assert.False(entity.IsValid(now));
    }

    [Fact]
    public void IsValid_ExpiracionExactamenteIgualANow_RetornaFalse()
    {
        // Boundary: ExpiresAt <= now => invalid. The strict-less-than
        // check keeps validation in lock-step with the row-lock UPDATE
        // (ExpiresAt > @now) the repository uses in PR1b.
        var now = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        var entity = RefreshTokenEntity.Reconstitute(
            id: Guid.NewGuid(),
            userId: "user-1",
            familyId: Guid.NewGuid(),
            tokenHash: new string('e', 64),
            createdAt: now.AddDays(-1),
            expiresAt: now,
            revokedAt: null,
            replacedById: null,
            lastUsedAt: now);

        Assert.False(entity.IsValid(now));
    }
}
