using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Seguridad.Contratos;
using SGV.Aplicacion.Seguridad.Servicios;
using SGV.Contracts.Seguridad;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// Unit tests for <see cref="RefreshTokenServicio"/> (PR2a of change
/// <c>implementa-refresh-tokens</c>). No database: the repository is a
/// hand-written in-memory fake, matching the project convention of
/// avoiding mocking frameworks.
/// </summary>
public sealed class RefreshTokenServicioTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssueAsync_PersistsHashedTokenInNewFamily()
    {
        var (servicio, repository, _, _) = Build();

        var emitido = await servicio.IssueAsync("user-1");

        Assert.False(string.IsNullOrWhiteSpace(emitido.Token));
        Assert.NotEqual(Guid.Empty, emitido.FamilyId);
        Assert.Equal(Now.UtcDateTime.AddDays(14), emitido.ExpiresAt.UtcDateTime);

        var stored = Assert.Single(repository.Tokens.Values);
        Assert.Equal("user-1", stored.UserId);
        Assert.Equal(emitido.FamilyId, stored.FamilyId);
        Assert.Equal(RefreshTokenHashing.ComputeSha256Hex(emitido.Token), stored.TokenHash);
        Assert.NotEqual(emitido.Token, stored.TokenHash);
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task IssueAsync_TwiceForSameUser_CreatesDistinctFamilies()
    {
        var (servicio, _, _, _) = Build();

        var first = await servicio.IssueAsync("user-1");
        var second = await servicio.IssueAsync("user-1");

        Assert.NotEqual(first.FamilyId, second.FamilyId);
        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_RotatesWithinSameFamily()
    {
        var (servicio, repository, _, _) = Build();
        var issued = await servicio.IssueAsync("user-1");
        var originalHash = RefreshTokenHashing.ComputeSha256Hex(issued.Token);

        var result = await servicio.RefreshAsync(issued.Token);

        Assert.Equal(RefreshOutcome.Success, result.Outcome);
        Assert.Equal("jwt-for-user-1", result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.NotEqual(issued.Token, result.RefreshToken);
        Assert.Equal(Now.UtcDateTime.AddDays(14), result.RefreshTokenExpiresAt!.Value.UtcDateTime);

        var consumed = repository.Tokens[originalHash];
        Assert.NotNull(consumed.RevokedAt);
        var rotated = repository.Tokens[RefreshTokenHashing.ComputeSha256Hex(result.RefreshToken!)];
        Assert.Equal(issued.FamilyId, rotated.FamilyId);
        Assert.Equal(rotated.Id, consumed.ReplacedById);
        Assert.Null(rotated.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_WithUnknownToken_ReturnsInvalid()
    {
        var (servicio, _, auditoria, _) = Build();

        var result = await servicio.RefreshAsync("never-issued");

        Assert.Equal(RefreshOutcome.Invalid, result.Outcome);
        Assert.Null(result.AccessToken);
        Assert.Empty(auditoria.Entries);
    }

    [Fact]
    public async Task RefreshAsync_WithBlankToken_ReturnsInvalid()
    {
        var (servicio, _, _, _) = Build();

        Assert.Equal(RefreshOutcome.Invalid, (await servicio.RefreshAsync(null)).Outcome);
        Assert.Equal(RefreshOutcome.Invalid, (await servicio.RefreshAsync("   ")).Outcome);
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredToken_ReturnsExpiredWithoutRevokingFamily()
    {
        var clock = new FakeTimeProvider(Now);
        var (servicio, repository, auditoria, _) = Build(clock);
        var issued = await servicio.IssueAsync("user-1");
        var hash = RefreshTokenHashing.ComputeSha256Hex(issued.Token);

        clock.Advance(TimeSpan.FromDays(15));
        var result = await servicio.RefreshAsync(issued.Token);

        Assert.Equal(RefreshOutcome.Expired, result.Outcome);
        // REQ-AUTH-REFRESH-2: the row must not be mutated and the family
        // must not be revoked — expiry is not evidence of compromise.
        Assert.Null(repository.Tokens[hash].RevokedAt);
        Assert.Equal(0, repository.RevokeFamilyCalls);
        Assert.Empty(auditoria.Entries);
    }

    [Fact]
    public async Task RefreshAsync_WithAlreadyConsumedToken_DetectsReplayAndRevokesFamily()
    {
        var (servicio, repository, auditoria, _) = Build();
        var issued = await servicio.IssueAsync("user-1");
        var rotated = await servicio.RefreshAsync(issued.Token);
        Assert.Equal(RefreshOutcome.Success, rotated.Outcome);

        var replay = await servicio.RefreshAsync(issued.Token);

        Assert.Equal(RefreshOutcome.ReplayDetected, replay.Outcome);
        Assert.Null(replay.RefreshToken);
        Assert.Equal(1, repository.RevokeFamilyCalls);
        Assert.All(
            repository.Tokens.Values.Where(t => t.FamilyId == issued.FamilyId),
            token => Assert.NotNull(token.RevokedAt));

        var entry = Assert.Single(auditoria.Entries);
        Assert.Equal("RefreshToken", entry.Entidad);
        Assert.Equal("RevocarFamilia", entry.Accion);
        Assert.Equal(issued.FamilyId.ToString(), entry.EntityId);
        Assert.DoesNotContain("TokenHash", string.Join(",", entry.ValoresNuevos.Keys));
    }

    [Fact]
    public async Task RefreshAsync_WhenAnotherFamilyExists_ReplayOnlyRevokesItsOwnFamily()
    {
        var (servicio, repository, _, _) = Build();
        var compromised = await servicio.IssueAsync("user-1");
        var untouched = await servicio.IssueAsync("user-1");
        _ = await servicio.RefreshAsync(compromised.Token);

        var replay = await servicio.RefreshAsync(compromised.Token);

        Assert.Equal(RefreshOutcome.ReplayDetected, replay.Outcome);
        Assert.All(
            repository.Tokens.Values.Where(t => t.FamilyId == untouched.FamilyId),
            token => Assert.Null(token.RevokedAt));
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCallsWithSameToken_OnlyOneWins()
    {
        // REQ-RTM-CONCURRENCY-1: TryConsumeAsync is the atomic primitive;
        // exactly one caller may observe Success, the loser must be treated
        // as a replay (the token is no longer current).
        var (servicio, _, _, _) = Build();
        var issued = await servicio.IssueAsync("user-1");

        var results = await Task.WhenAll(
            servicio.RefreshAsync(issued.Token),
            servicio.RefreshAsync(issued.Token));

        Assert.Single(results, r => r.Outcome == RefreshOutcome.Success);
        Assert.Single(results, r => r.Outcome == RefreshOutcome.ReplayDetected);
    }

    [Fact]
    public async Task RevokeAsync_RevokesEveryActiveFamilyOfTheUser()
    {
        var (servicio, repository, auditoria, _) = Build();
        var first = await servicio.IssueAsync("user-1");
        var second = await servicio.IssueAsync("user-1");
        var otherUser = await servicio.IssueAsync("user-2");

        await servicio.RevokeAsync("user-1", first.Token);

        Assert.All(
            repository.Tokens.Values.Where(t => t.UserId == "user-1"),
            token => Assert.NotNull(token.RevokedAt));
        Assert.Null(repository.Tokens[RefreshTokenHashing.ComputeSha256Hex(otherUser.Token)].RevokedAt);
        Assert.NotEqual(first.FamilyId, second.FamilyId);

        var entry = Assert.Single(auditoria.Entries);
        Assert.Equal("Logout", entry.Accion);
        Assert.Equal("user-1", entry.EntityId);
    }

    [Fact]
    public async Task RevokeAsync_WithoutToken_StillRevokesAndDoesNotThrow()
    {
        var (servicio, repository, _, _) = Build();
        var issued = await servicio.IssueAsync("user-1");

        await servicio.RevokeAsync("user-1");

        Assert.NotNull(repository.Tokens[RefreshTokenHashing.ComputeSha256Hex(issued.Token)].RevokedAt);
    }

    [Fact]
    public async Task RevokeAsync_WithoutActiveTokens_IsGracefulNoOp()
    {
        var (servicio, _, auditoria, _) = Build();

        await servicio.RevokeAsync("user-without-tokens");

        // Nothing to revoke is a legitimate legacy-session case, not an error,
        // and it must not pollute the audit trail.
        Assert.Empty(auditoria.Entries);
    }

    private static (RefreshTokenServicio Servicio,
        FakeRefreshTokenRepository Repository,
        FakeAuditoria Auditoria,
        FakeTimeProvider Clock) Build(FakeTimeProvider? clock = null)
    {
        clock ??= new FakeTimeProvider(Now);
        var repository = new FakeRefreshTokenRepository();
        var auditoria = new FakeAuditoria();
        var servicio = new RefreshTokenServicio(
            repository,
            new NoOpUnitOfWork(),
            new FakeAccessTokenIssuer(),
            auditoria,
            clock,
            NullLogger<RefreshTokenServicio>.Instance,
            Options.Create(new RefreshTokenOptions()));

        return (servicio, repository, auditoria, clock);
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly object gate = new();

        public ConcurrentDictionary<string, RefreshTokenSnapshot> Tokens { get; } = new(StringComparer.Ordinal);

        public int RevokeFamilyCalls { get; private set; }

        public Task AddAsync(RefreshTokenSnapshot token, CancellationToken cancellationToken = default)
        {
            Tokens[token.TokenHash] = token;
            return Task.CompletedTask;
        }

        public Task<RefreshTokenSnapshot?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
            => Task.FromResult(Tokens.TryGetValue(tokenHash ?? string.Empty, out var token) ? token : null);

        public Task<bool> TryConsumeAsync(
            string tokenHash,
            Guid replacedById,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                if (!Tokens.TryGetValue(tokenHash ?? string.Empty, out var token)
                    || !token.IsActive(nowUtc))
                {
                    return Task.FromResult(false);
                }

                Tokens[token.TokenHash] = token with
                {
                    RevokedAt = nowUtc,
                    ReplacedById = replacedById,
                    LastUsedAt = nowUtc
                };
                return Task.FromResult(true);
            }
        }

        public Task<int> RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                RevokeFamilyCalls++;
                return Task.FromResult(RevokeWhere(token => token.FamilyId == familyId, nowUtc));
            }
        }

        public Task<int> RevokeAllForUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                return Task.FromResult(RevokeWhere(
                    token => string.Equals(token.UserId, userId, StringComparison.Ordinal),
                    nowUtc));
            }
        }

        private int RevokeWhere(Func<RefreshTokenSnapshot, bool> predicate, DateTime nowUtc)
        {
            var affected = 0;
            foreach (var token in Tokens.Values.Where(t => predicate(t) && t.RevokedAt is null).ToArray())
            {
                Tokens[token.TokenHash] = token with { RevokedAt = nowUtc };
                affected++;
            }
            return affected;
        }
    }

    private sealed record AuditEntry(
        string Entidad,
        string EntityId,
        string Accion,
        IReadOnlyDictionary<string, object?> ValoresNuevos);

    private sealed class FakeAuditoria : IAuditoriaServicio
    {
        private readonly List<AuditEntry> entries = new();

        public IReadOnlyList<AuditEntry> Entries => entries;

        public Task RegistrarAsync(
            string entidad,
            string entityId,
            string accion,
            string? usuarioOperadorId,
            IReadOnlyDictionary<string, object?> valoresAnteriores,
            IReadOnlyDictionary<string, object?> valoresNuevos,
            CancellationToken cancellationToken = default)
        {
            lock (entries)
            {
                entries.Add(new AuditEntry(entidad, entityId, accion, valoresNuevos));
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
    {
        public Task<AccessTokenEmitido?> EmitirAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<AccessTokenEmitido?>(new AccessTokenEmitido($"jwt-for-{userId}", Now.AddHours(1)));
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset current = start;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan delta) => current = current.Add(delta);
    }
}
