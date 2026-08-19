using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Seguridad;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Test fixture for <c>RefreshTokenRepository</c> integration tests. Each
/// instance creates a fresh <see cref="PersonaEntity"/> + <see cref="SgvIdentityUser"/>
/// pair (to satisfy the FK from <c>RefreshTokens.UserId</c>) and tracks all
/// data it inserted so <see cref="DisposeAsync"/> can clean it up without
/// touching unrelated rows in the shared <c>sgv_test</c> database.
/// </summary>
/// <remarks>
/// PR1b scope: only refresh-token-specific tables are cleaned. We do NOT
/// wipe <c>Personas</c> or <c>AspNetUsers</c> globally because the
/// <c>SgvTestDatabaseCleaner</c> only runs in dedicated setups and the
/// persona/user we create here is disposable enough that targeted cleanup
/// is sufficient.
/// </remarks>
internal sealed class RefreshTokenTestFixture : IAsyncDisposable
{
    private readonly List<Guid> _familyIds = new();
    private bool _disposed;

    private RefreshTokenTestFixture(SgvDbContext context, string userId, Guid personaId)
    {
        Context = context;
        UserId = userId;
        PersonaId = personaId;
    }

    public SgvDbContext Context { get; }

    public string UserId { get; }

    public Guid PersonaId { get; }

    public Guid TrackFamilyId(Guid familyId)
    {
        _familyIds.Add(familyId);
        return familyId;
    }

    public static async Task<RefreshTokenTestFixture> CreateAsync(SgvDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var persona = new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Legajo = $"RT-LEG-{suffix}",
            Nombres = "Refresh",
            Apellidos = $"Test-{suffix}",
            Email = $"rt-{suffix}@test.local",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        context.Personas.Add(persona);

        var user = new SgvIdentityUser
        {
            Id = $"rt-user-{Guid.NewGuid():N}"[..30],
            PersonaId = persona.Id,
            UserName = $"rt-{suffix}",
            NormalizedUserName = $"RT-{suffix}".ToUpperInvariant(),
            Email = persona.Email,
            NormalizedEmail = persona.Email!.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        context.Users.Add(user);

        await context.SaveChangesAsync();

        return new RefreshTokenTestFixture(context, user.Id, persona.Id);
    }

    public static RefreshTokenSnapshot CrearSnapshotValido(
        string userId,
        Guid familyId,
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        DateTime? lastUsedAt = null,
        string? tokenHash = null)
    {
        var now = createdAt ?? new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
        var expires = expiresAt ?? now.AddDays(14);
        var used = lastUsedAt ?? now;
        var hash = tokenHash ?? new string('a', 64);

        return new RefreshTokenSnapshot(
            Id: Guid.NewGuid(),
            UserId: userId,
            FamilyId: familyId,
            TokenHash: hash,
            CreatedAt: now,
            ExpiresAt: expires,
            RevokedAt: null,
            ReplacedById: null,
            LastUsedAt: used);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Delete refresh tokens owned by our test user. The FK from
            // RefreshTokens to AspNetUsers is CASCADE, so deleting the user
            // would also clean up its tokens — but we explicitly delete the
            // refresh tokens first to keep test isolation precise.
            if (_familyIds.Count > 0)
            {
                var ids = string.Join(",", _familyIds.Select(id => $"'{id}'"));
                await Context.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM `RefreshTokens` WHERE `FamilyId` IN ({ids})");
            }

            await Context.Database.ExecuteSqlRawAsync(
                "DELETE FROM `RefreshTokens` WHERE `UserId` = {0}", UserId);

            await Context.Database.ExecuteSqlRawAsync(
                "DELETE FROM `AspNetUsers` WHERE `Id` = {0}", UserId);
            await Context.Database.ExecuteSqlRawAsync(
                "DELETE FROM `Personas` WHERE `Id` = {0}", PersonaId.ToString("D"));
        }
        finally
        {
            Context.ChangeTracker.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Bridges the application-layer <see cref="RefreshTokenSnapshot"/> with the
/// persistence-layer <see cref="RefreshTokenEntity"/> for tests that need to
/// seed rows directly through <c>DbSet&lt;RefreshTokenEntity&gt;</c> (for
/// example, to exercise schema-level constraints like the UNIQUE index on
/// <c>TokenHash</c>).
/// </summary>
internal static class RefreshTokenEntityAdapter
{
    public static RefreshTokenEntity FromSnapshot(RefreshTokenSnapshot snapshot)
    {
        return RefreshTokenEntity.Reconstitute(
            id: snapshot.Id,
            userId: snapshot.UserId,
            familyId: snapshot.FamilyId,
            tokenHash: snapshot.TokenHash,
            createdAt: snapshot.CreatedAt,
            expiresAt: snapshot.ExpiresAt,
            revokedAt: snapshot.RevokedAt,
            replacedById: snapshot.ReplacedById,
            lastUsedAt: snapshot.LastUsedAt);
    }
}