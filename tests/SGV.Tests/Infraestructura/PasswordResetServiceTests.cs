using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Infraestructura;

/// <summary>
/// Behavior tests for <see cref="PasswordResetService"/>. Covers the
/// contract laid down by
/// <c>openspec/changes/2026-07-21-password-reset-181/specs/password-reset-flow/spec.md</c>:
/// <list type="bullet">
///   <item><c>FindByNameAsync</c>/<c>FindByEmailAsync</c> both fall through
///   to a single recovery flow.</item>
///   <item>Anti-enumeration: unknown identifiers MUST NOT send email.</item>
///   <item>Token issuance and verification flow through Identity.</item>
///   <item>The reset link is URL-encoded with the Web's
///   <see cref="SmtpOptions.WebBaseUrl"/>.</item>
/// </list>
/// </summary>
public sealed class PasswordResetServiceTests
{
    private const string SeedUserId = "user-1";
    private const string SeedUserName = "admin";
    private const string SeedEmail = "admin@example.com";
    private const string WebBaseUrl = "https://sgv.example.com";

    private static (PasswordResetService Sut, InMemoryUserStore Store, TestableUserManager UserManager, FakeEmailSender Email)
        BuildSut(SgvIdentityUser? seedUser = null, string? seedPasswordHash = null, string? seedSecurityStamp = null)
    {
        var store = new InMemoryUserStore();
        if (seedUser is not null)
        {
            store.Seed(seedUser, passwordHash: seedPasswordHash, securityStamp: seedSecurityStamp);
        }

        var userManager = new TestableUserManager(store);
        var email = new FakeEmailSender();
        var smtp = Options.Create(new SmtpOptions
        {
            Mode = SmtpDeliveryMode.Logger,
            Host = "unused",
            Port = 25,
            FromAddress = "no-reply@sgv.local",
            FromName = "SGV",
            WebBaseUrl = WebBaseUrl
        });
        var logger = NullLogger<PasswordResetService>.Instance;

        var sut = new PasswordResetService(userManager, email, smtp, logger);
        return (sut, store, userManager, email);
    }

    private static SgvIdentityUser NewSeedUser() => new()
    {
        Id = SeedUserId,
        UserName = SeedUserName,
        Email = SeedEmail,
        PersonaId = Guid.Parse("e0000000-0000-0000-0000-000000000001")
    };

    // ── ForgotPasswordAsync ────────────────────────────────────────────

    [Fact]
    public async Task ForgotPasswordAsync_ExistingUserByUserName_SendsEmailWithUrlEncodedLink()
    {
        var (sut, _, userManager, email) = BuildSut(seedUser: NewSeedUser());

        var outcome = await sut.ForgotPasswordAsync(new ForgotPasswordRequest(SeedUserName));

        Assert.Equal(PasswordResetOutcome.Success, outcome);
        Assert.Single(email.Calls);
        var body = email.Calls[0].HtmlBody;
        var expectedLink =
            $"{WebBaseUrl}/auth/reset-password?userId={SeedUserId}&token={Uri.EscapeDataString(userManager.NextPasswordResetToken)}";
        Assert.Contains(expectedLink, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ExistingUserByEmail_SendsEmail()
    {
        var (sut, _, _, email) = BuildSut(seedUser: NewSeedUser());

        var outcome = await sut.ForgotPasswordAsync(new ForgotPasswordRequest(SeedEmail));

        Assert.Equal(PasswordResetOutcome.Success, outcome);
        Assert.Single(email.Calls);
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnknownUser_DoesNotSendEmail_AndReturnsSuccess()
    {
        // Anti-enumeration: the outcome shape MUST be equivalent to
        // the existing-user path so an attacker cannot enumerate
        // which identifiers are registered.
        var (sut, _, _, email) = BuildSut(); // no seed user

        var outcome = await sut.ForgotPasswordAsync(new ForgotPasswordRequest("ghost@example.com"));

        Assert.Equal(PasswordResetOutcome.Success, outcome);
        Assert.Empty(email.Calls);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ExistingAndUnknownUsers_ProduceByteEquivalentSuccessOutcome()
    {
        var (withUser, _, _, emailWith) = BuildSut(seedUser: NewSeedUser());
        var (empty, _, _, emptyEmail) = BuildSut(); // no seed

        var real = await withUser.ForgotPasswordAsync(new ForgotPasswordRequest(SeedUserName));
        var ghost = await empty.ForgotPasswordAsync(new ForgotPasswordRequest(SeedUserName));

        Assert.Equal(PasswordResetOutcome.Success, real);
        Assert.Equal(PasswordResetOutcome.Success, ghost);
        Assert.Single(emailWith.Calls);
        Assert.Empty(emptyEmail.Calls);
    }

    // ── ResetPasswordAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_RotatesPassword_ReturnsSuccess()
    {
        const string token = "valid-token";
        const string newPassword = "Password1!";

        var (sut, _, userManager, _) = BuildSut(
            seedUser: NewSeedUser(),
            seedPasswordHash: "old-hash");
        userManager.ValidPasswordResetTokens.Add(token);

        var outcome = await sut.ResetPasswordAsync(
            new ResetPasswordRequest(SeedUserId, token, newPassword));

        Assert.Equal(PasswordResetOutcome.Success, outcome);
        Assert.True(userManager.ResetPasswordCalled);
        Assert.Equal(newPassword, userManager.LastResetNewPassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ReturnsInvalidTokenAndDoesNotRotatePassword()
    {
        var (sut, _, userManager, _) = BuildSut(
            seedUser: NewSeedUser(),
            seedPasswordHash: "old-hash");

        var outcome = await sut.ResetPasswordAsync(
            new ResetPasswordRequest(SeedUserId, Token: "bogus", NewPassword: "Password1!"));

        Assert.Equal(PasswordResetOutcome.InvalidToken, outcome);
        Assert.False(userManager.ResetPasswordCalled);
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownUserId_ReturnsInvalidToken()
    {
        var (sut, _, _, _) = BuildSut(seedUser: NewSeedUser());

        var outcome = await sut.ResetPasswordAsync(
            new ResetPasswordRequest(UserId: "missing-id", Token: "token", NewPassword: "Password1!"));

        Assert.Equal(PasswordResetOutcome.InvalidToken, outcome);
    }
}
