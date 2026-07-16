using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGV.Api.Seguridad;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Seguridad;

/// <summary>
/// Behavior tests for <see cref="RevalidatorCredenciales"/>: a missing user,
/// a locked-out user and an active user must yield false, false and true
/// respectively. Covers the security-critical decision logic of
/// <c>SigueVigenteAsync</c>.
/// </summary>
public sealed class RevalidatorCredencialesTests
{
    [Fact]
    public async Task SigueVigenteAsync_UserNotFound_ReturnsFalse()
    {
        var revalidator = BuildRevalidator(new InMemoryUserLockoutStore());

        var result = await revalidator.SigueVigenteAsync("missing-id");

        Assert.False(result);
    }

    [Fact]
    public async Task SigueVigenteAsync_UserLockedOut_ReturnsFalse()
    {
        var store = new InMemoryUserLockoutStore();
        const string lockedUserId = "locked-id";
        store.Add(new SgvIdentityUser
        {
            Id = lockedUserId,
            UserName = "locked",
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddYears(10)
        });
        var revalidator = BuildRevalidator(store);

        var result = await revalidator.SigueVigenteAsync(lockedUserId);

        Assert.False(result);
    }

    [Fact]
    public async Task SigueVigenteAsync_ActiveUser_ReturnsTrue()
    {
        var store = new InMemoryUserLockoutStore();
        const string activeUserId = "active-id";
        store.Add(new SgvIdentityUser
        {
            Id = activeUserId,
            UserName = "active",
            LockoutEnabled = true,
            LockoutEnd = null
        });
        var revalidator = BuildRevalidator(store);

        var result = await revalidator.SigueVigenteAsync(activeUserId);

        Assert.True(result);
    }

    private static RevalidatorCredenciales BuildRevalidator(IUserStore<SgvIdentityUser> store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserStore<SgvIdentityUser>>(store);
        services.AddSingleton<UserManager<SgvIdentityUser>>(sp => new UserManager<SgvIdentityUser>(
            sp.GetRequiredService<IUserStore<SgvIdentityUser>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<SgvIdentityUser>(),
            Array.Empty<IUserValidator<SgvIdentityUser>>(),
            Array.Empty<IPasswordValidator<SgvIdentityUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            sp,
            NullLogger<UserManager<SgvIdentityUser>>.Instance));
        var provider = services.BuildServiceProvider();

        return new RevalidatorCredenciales(
            new FixedScopeFactory(provider),
            NullLogger<RevalidatorCredenciales>.Instance);
    }

    /// <summary>
    /// Minimal <see cref="IUserLockoutStore{SgvIdentityUser}"/> backing the
    /// revalidator: only <c>FindByIdAsync</c>, <c>GetLockoutEnabledAsync</c>
    /// and <c>GetLockoutEndDateAsync</c> return real values; the rest throw
    /// so any future call surfaces a regression in the production store.
    /// </summary>
    private sealed class InMemoryUserLockoutStore : IUserLockoutStore<SgvIdentityUser>
    {
        private readonly Dictionary<string, SgvIdentityUser> _users = new(StringComparer.Ordinal);

        public void Add(SgvIdentityUser user) => _users[user.Id] = user;

        public Task<string> GetUserIdAsync(SgvIdentityUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(SgvIdentityUser user, CancellationToken ct) => Task.FromResult<string?>(user.UserName);
        public Task SetUserNameAsync(SgvIdentityUser user, string? userName, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(SgvIdentityUser user, CancellationToken ct) => Task.FromResult<string?>(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(SgvIdentityUser user, string? normalizedName, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(SgvIdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(SgvIdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(SgvIdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<SgvIdentityUser?> FindByIdAsync(string userId, CancellationToken ct)
            => Task.FromResult(_users.TryGetValue(userId, out var user) ? user : null);
        public Task<SgvIdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) => throw new NotSupportedException();
        public Task SetLockoutEndDateAsync(SgvIdentityUser user, DateTimeOffset? lockoutEnd, CancellationToken ct) => throw new NotSupportedException();
        public Task<DateTimeOffset?> GetLockoutEndDateAsync(SgvIdentityUser user, CancellationToken ct) => Task.FromResult(user.LockoutEnd);
        public Task<int> IncrementAccessFailedCountAsync(SgvIdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task ResetAccessFailedCountAsync(SgvIdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> GetAccessFailedCountAsync(SgvIdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> GetLockoutEnabledAsync(SgvIdentityUser user, CancellationToken ct) => Task.FromResult(user.LockoutEnabled);
        public Task SetLockoutEnabledAsync(SgvIdentityUser user, bool enabled, CancellationToken ct) => throw new NotSupportedException();
        public void Dispose() { }
    }

    /// <summary>
    /// Test double for <see cref="IServiceScopeFactory"/> that always returns
    /// a scope over a fixed provider. The revalidator only reads
    /// <c>UserManager&lt;SgvIdentityUser&gt;</c> from the scope, so the
    /// production composition root's scoping semantics are not relevant.
    /// </summary>
    private sealed class FixedScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _provider;
        public FixedScopeFactory(IServiceProvider provider) => _provider = provider;
        public IServiceScope CreateScope() => new FixedScope(_provider);

        private sealed class FixedScope : IServiceScope
        {
            public FixedScope(IServiceProvider provider) => ServiceProvider = provider;
            public IServiceProvider ServiceProvider { get; }
            public void Dispose() { }
        }
    }
}
