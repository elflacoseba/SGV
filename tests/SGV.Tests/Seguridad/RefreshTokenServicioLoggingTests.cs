using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
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
/// Unit tests for the structured logging hooks in
/// <see cref="RefreshTokenServicio"/> (PR4 of change
/// <c>implementa-refresh-tokens</c>). Verifies the log events emitted on
/// refresh success, refresh failure (invalid / expired), replay detection
/// and family revocation. The payload MUST never contain the plain token
/// nor its hash (privacy + audit hygiene).
/// </summary>
public sealed class RefreshTokenServicioLoggingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Refresh_Success_LogsInformationWithExpectedProperties()
    {
        var (servicio, _, logger, _) = Build();
        var issued = await servicio.IssueAsync("user-1");

        var result = await servicio.RefreshAsync(issued.Token);

        Assert.Equal(RefreshOutcome.Success, result.Outcome);
        var entry = Assert.Single(logger.Records, r => r.Level == LogLevel.Information);
        Assert.Contains("RefreshSuccess", entry.Message, StringComparison.Ordinal);
        Assert.Contains("user-1", entry.Message, StringComparison.Ordinal);
        Assert.Contains(issued.FamilyId.ToString(), entry.Message, StringComparison.Ordinal);
        // Privacy: never log the plain token or its hash.
        Assert.DoesNotContain(issued.Token, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RefreshTokenHashing.ComputeSha256Hex(issued.Token), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_InvalidToken_LogsWarning()
    {
        var (servicio, _, logger, _) = Build();

        var result = await servicio.RefreshAsync("never-issued");

        Assert.Equal(RefreshOutcome.Invalid, result.Outcome);
        var entry = Assert.Single(logger.Records, r => r.Level == LogLevel.Warning);
        Assert.Contains("RefreshFailure", entry.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidToken", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_LogsWarning()
    {
        var clock = new FakeTimeProvider(Now);
        var (servicio, _, logger, _) = Build(clock);
        var issued = await servicio.IssueAsync("user-1");

        clock.Advance(TimeSpan.FromDays(15));
        var result = await servicio.RefreshAsync(issued.Token);

        Assert.Equal(RefreshOutcome.Expired, result.Outcome);
        var entry = Assert.Single(logger.Records, r => r.Level == LogLevel.Warning);
        Assert.Contains("RefreshFailure", entry.Message, StringComparison.Ordinal);
        Assert.Contains("ExpiredToken", entry.Message, StringComparison.Ordinal);
        Assert.Contains("user-1", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_ReplayDetected_LogsError_WithFamilyId()
    {
        var (servicio, _, logger, _) = Build();
        var issued = await servicio.IssueAsync("user-1");
        var rotated = await servicio.RefreshAsync(issued.Token);
        Assert.Equal(RefreshOutcome.Success, rotated.Outcome);

        // Reset the log sink so we only observe the replay event, not the
        // successful rotation that preceded it.
        logger.Clear();

        var replay = await servicio.RefreshAsync(issued.Token);

        Assert.Equal(RefreshOutcome.ReplayDetected, replay.Outcome);
        var entry = Assert.Single(logger.Records, r => r.Level == LogLevel.Error);
        Assert.Contains("RefreshReplayDetected", entry.Message, StringComparison.Ordinal);
        Assert.Contains(issued.FamilyId.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains("user-1", entry.Message, StringComparison.Ordinal);
        // Privacidad: el log NO debe contener el token ni su hash.
        Assert.DoesNotContain(issued.Token, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RefreshTokenHashing.ComputeSha256Hex(issued.Token), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeAllForUser_LogsInformation_WithAffectedCounts()
    {
        var (servicio, _, logger, _) = Build();
        var first = await servicio.IssueAsync("user-1");
        var second = await servicio.IssueAsync("user-1");
        var otherUser = await servicio.IssueAsync("user-2");

        logger.Clear();

        await servicio.RevokeAsync("user-1", first.Token);

        var entry = Assert.Single(logger.Records, r => r.Level == LogLevel.Information);
        Assert.Contains("FamilyRevocation", entry.Message, StringComparison.Ordinal);
        Assert.Contains("user-1", entry.Message, StringComparison.Ordinal);
        // Two active families/tokens for user-1 (first + second) are revoked.
        Assert.Contains("2", entry.Message, StringComparison.Ordinal);
        // The other user's token must not be counted or mentioned.
        Assert.DoesNotContain(otherUser.FamilyId.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(otherUser.Token, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_NeverContainPlainTokenOrHash()
    {
        // Privacy regression: scan every emitted log in the full flow.
        var (servicio, _, logger, clock) = Build();
        var issued = await servicio.IssueAsync("user-1");
        var plain = issued.Token;
        var hash = RefreshTokenHashing.ComputeSha256Hex(plain);

        _ = await servicio.RefreshAsync(plain);
        _ = await servicio.RefreshAsync(plain); // Replay
        _ = await servicio.RefreshAsync("never-issued"); // Invalid
        var issued2 = await servicio.IssueAsync("user-1");
        clock.Advance(TimeSpan.FromDays(20));
        _ = await servicio.RefreshAsync(issued2.Token); // Expired
        await servicio.RevokeAsync("user-1", plain);

        Assert.NotEmpty(logger.Records);
        foreach (var record in logger.Records)
        {
            Assert.DoesNotContain(plain, record.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(hash, record.Message, StringComparison.Ordinal);
        }
    }

    private static (RefreshTokenServicio Servicio,
        FakeRefreshTokenRepository Repository,
        ListLogSink<RefreshTokenServicio> Logger,
        FakeTimeProvider Clock) Build(FakeTimeProvider? clock = null)
    {
        clock ??= new FakeTimeProvider(Now);
        var repository = new FakeRefreshTokenRepository();
        var auditoria = new FakeAuditoria();
        var logger = new ListLogSink<RefreshTokenServicio>();
        var servicio = new RefreshTokenServicio(
            repository,
            new NoOpUnitOfWork(),
            new FakeAccessTokenIssuer(),
            auditoria,
            clock,
            logger,
            Options.Create(new RefreshTokenOptions()));

        return (servicio, repository, logger, clock);
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

    private sealed class FakeAuditoria : IAuditoriaServicio
    {
        public Task RegistrarAsync(
            string entidad,
            string entityId,
            string accion,
            string? usuarioOperadorId,
            IReadOnlyDictionary<string, object?> valoresAnteriores,
            IReadOnlyDictionary<string, object?> valoresNuevos,
            CancellationToken cancellationToken = default)
        {
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

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset current;

        public FakeTimeProvider(DateTimeOffset start) => current = start;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan delta) => current = current.Add(delta);
    }

    private sealed class ListLogSink<T> : ILogger<T>
    {
        private readonly List<LogRecord> records = new();
        private readonly object gate = new();

        public IReadOnlyList<LogRecord> Records
        {
            get { lock (gate) { return records.ToArray(); } }
        }

        public void Clear()
        {
            lock (gate) { records.Clear(); }
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            lock (gate)
            {
                records.Add(new LogRecord(logLevel, eventId, message, exception));
            }
        }

        public sealed record LogRecord(LogLevel Level, EventId EventId, string Message, Exception? Exception);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
