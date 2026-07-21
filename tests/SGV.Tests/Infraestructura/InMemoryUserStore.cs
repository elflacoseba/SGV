using Microsoft.AspNetCore.Identity;
using SGV.Infraestructura.Seguridad;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Minimal in-memory <see cref="IUserStore{TUser}"/> backing the
/// <see cref="PasswordResetServiceTests"/>. The store implements only
/// the surfaces <c>PasswordResetService</c> touches — basic identity,
/// email lookup, password hash and security stamp — and throws on
/// any other call so future regressions surface as loud test
/// failures instead of silently passing.
/// </summary>
internal sealed class InMemoryUserStore :
    IUserStore<SgvIdentityUser>,
    IUserEmailStore<SgvIdentityUser>,
    IUserPasswordStore<SgvIdentityUser>,
    IUserSecurityStampStore<SgvIdentityUser>
{
    private readonly Dictionary<string, SgvIdentityUser> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SgvIdentityUser> _byNormalizedName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SgvIdentityUser> _byNormalizedEmail = new(StringComparer.Ordinal);
    private readonly Dictionary<SgvIdentityUser, string> _passwordHashes = new();
    private readonly Dictionary<SgvIdentityUser, string> _securityStamps = new();

    public IdentityErrorDescriber ErrorDescriber { get; } = new();

    public void Seed(SgvIdentityUser user, string? passwordHash = null, string? securityStamp = null)
    {
        user.NormalizedUserName = (user.UserName ?? user.Id).ToUpperInvariant();
        user.NormalizedEmail = (user.Email ?? string.Empty).ToUpperInvariant();

        _byId[user.Id] = user;
        _byNormalizedName[user.NormalizedUserName] = user;
        _byNormalizedEmail[user.NormalizedEmail] = user;

        if (passwordHash is not null)
        {
            _passwordHashes[user] = passwordHash;
        }

        // Match UserManager's default: a fresh SecurityStamp on creation.
        _securityStamps[user] = securityStamp ?? Guid.NewGuid().ToString("N");
    }

    /// <summary>Last security stamp observed during the test run.</summary>
    public string? GetSecurityStamp(SgvIdentityUser user) =>
        _securityStamps.TryGetValue(user, out var stamp) ? stamp : null;

    // ── IUserStore<SgvIdentityUser> ─────────────────────────────────

    public Task<string> GetUserIdAsync(SgvIdentityUser user, CancellationToken ct) => Task.FromResult(user.Id);

    public Task<string?> GetUserNameAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.UserName);

    public Task SetUserNameAsync(SgvIdentityUser user, string? userName, CancellationToken ct)
    {
        user.UserName = userName;
        user.NormalizedUserName = (userName ?? string.Empty).ToUpperInvariant();
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(SgvIdentityUser user, string? normalizedName, CancellationToken ct)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task<IdentityResult> CreateAsync(SgvIdentityUser user, CancellationToken ct)
    {
        Seed(user);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult(IdentityResult.Success);

    public Task<IdentityResult> DeleteAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult(IdentityResult.Success);

    public Task<SgvIdentityUser?> FindByIdAsync(string userId, CancellationToken ct) =>
        Task.FromResult(_byId.TryGetValue(userId, out var user) ? user : null);

    public Task<SgvIdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
        Task.FromResult(_byNormalizedName.TryGetValue(normalizedUserName, out var user) ? user : null);

    // ── IUserEmailStore<SgvIdentityUser> ─────────────────────────────

    public Task SetEmailAsync(SgvIdentityUser user, string? email, CancellationToken ct)
    {
        user.Email = email;
        user.NormalizedEmail = (email ?? string.Empty).ToUpperInvariant();
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.Email);

    public Task<bool> GetEmailConfirmedAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(SgvIdentityUser user, bool confirmed, CancellationToken ct)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedEmailAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult<string?>(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(SgvIdentityUser user, string? normalizedEmail, CancellationToken ct)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task<SgvIdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        Task.FromResult(_byNormalizedEmail.TryGetValue(normalizedEmail, out var user) ? user : null);

    // ── IUserPasswordStore<SgvIdentityUser> ──────────────────────────

    public Task SetPasswordHashAsync(SgvIdentityUser user, string? passwordHash, CancellationToken ct)
    {
        if (passwordHash is null)
        {
            _passwordHashes.Remove(user);
        }
        else
        {
            _passwordHashes[user] = passwordHash;
        }

        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult(_passwordHashes.TryGetValue(user, out var hash) ? hash : null);

    public Task<bool> HasPasswordAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult(_passwordHashes.ContainsKey(user));

    // ── IUserSecurityStampStore<SgvIdentityUser> ─────────────────────

    public Task SetSecurityStampAsync(SgvIdentityUser user, string stamp, CancellationToken ct)
    {
        _securityStamps[user] = stamp;
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(SgvIdentityUser user, CancellationToken ct) =>
        Task.FromResult(_securityStamps.TryGetValue(user, out var stamp) ? stamp : null);

    // ── IDispose ─────────────────────────────────────────────────────

    public void Dispose() { }
}
