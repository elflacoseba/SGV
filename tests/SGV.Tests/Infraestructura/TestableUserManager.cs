using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGV.Infraestructura.Seguridad;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Test-only <see cref="UserManager{TUser}"/> that lets the
/// <see cref="PasswordResetServiceTests"/> pin down token issuance
/// and verification without booting Identity's full token pipeline.
/// All other operations delegate to the underlying <see cref="IUserStore{TUser}"/>
/// verbatim, so the seed data still goes through the real
/// <c>UserManager</c> constraints (PasswordHasher, IdentityErrorDescriber).
/// </summary>
internal sealed class TestableUserManager : UserManager<SgvIdentityUser>
{
    private readonly InMemoryUserStore _store;

    public TestableUserManager(InMemoryUserStore store)
        : base(
              store,
              Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
              new PasswordHasher<SgvIdentityUser>(),
              Array.Empty<IUserValidator<SgvIdentityUser>>(),
              Array.Empty<IPasswordValidator<SgvIdentityUser>>(),
              new UpperInvariantLookupNormalizer(),
              new IdentityErrorDescriber(),
              new ServiceCollection().BuildServiceProvider(),
              NullLogger<UserManager<SgvIdentityUser>>.Instance)
    {
        _store = store;
    }

    /// <summary>
    /// Token returned by <see cref="GeneratePasswordResetTokenAsync"/>
    /// for the next <see cref="ForgotPasswordAsync"/> call.
    /// </summary>
    public string NextPasswordResetToken { get; set; } = "raw-token-CfDJ8abc";

    public override Task<string> GeneratePasswordResetTokenAsync(SgvIdentityUser user)
        => Task.FromResult(NextPasswordResetToken);

    /// <summary>
    /// Tokens that <see cref="VerifyUserTokenAsync"/> accepts for the
    /// current <c>ResetPassword</c> purpose. Each issued token is
    /// added here, mimicking a real DataProtection round-trip.
    /// </summary>
    public HashSet<string> ValidPasswordResetTokens { get; } = new(StringComparer.Ordinal);

    public override Task<bool> VerifyUserTokenAsync(
        SgvIdentityUser user,
        string tokenProvider,
        string purpose,
        string token)
    {
        if (!string.Equals(purpose, "ResetPassword", StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(ValidPasswordResetTokens.Contains(token));
    }

    /// <summary>True when <see cref="ResetPasswordAsync"/> was called for the seed user.</summary>
    public bool ResetPasswordCalled { get; private set; }

    public string? LastResetNewPassword { get; private set; }

    public override async Task<IdentityResult> ResetPasswordAsync(
        SgvIdentityUser user,
        string token,
        string? newPassword)
    {
        if (!ValidPasswordResetTokens.Contains(token))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidToken",
                Description = "El token de restablecimiento no es válido o ya fue utilizado."
            });
        }

        ResetPasswordCalled = true;
        LastResetNewPassword = newPassword;

        ValidPasswordResetTokens.Remove(token);
        var hasher = new PasswordHasher<SgvIdentityUser>();
        var hash = hasher.HashPassword(user, newPassword ?? string.Empty);
        // Mirror Identity's contract: rotate the password hash on the store.
        // We bypass the protected setter to avoid leaking the IUserStore<T>
        // accessor pattern into tests.
        var store = (IUserPasswordStore<SgvIdentityUser>)_store;
        await store.SetPasswordHashAsync(user, hash, CancellationToken.None).ConfigureAwait(false);
        return IdentityResult.Success;
    }
}
