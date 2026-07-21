using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGV.Aplicacion.Seguridad.PasswordReset;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Infraestructura.Email;
using SGV.Infraestructura.Seguridad;

namespace SGV.Infraestructura.Seguridad;

/// <summary>
/// Application port implementation that drives the password recovery
/// flow on top of ASP.NET Core Identity. <see cref="ForgotPasswordAsync"/>
/// looks the user up by either <c>UserName</c> or <c>Email</c> and, when
/// found, emails a recovery link built from <see cref="SmtpOptions.WebBaseUrl"/>;
/// when the user is unknown it returns the same outcome (silent path)
/// to prevent enumeration. <see cref="ResetPasswordAsync"/> verifies the
/// token with <see cref="UserManager{TUser}.VerifyUserTokenAsync"/> and
/// delegates rotation to <see cref="UserManager{TUser}.ResetPasswordAsync"/>.
/// </summary>
/// <remarks>
/// Splitting the link composition into the application layer (and not
/// inside <see cref="SmtpEmailSender"/>) keeps the responsibility on
/// the recovery flow: the sender stays a dumb transport and the URL
/// shape remains testable against <see cref="SmtpOptions.WebBaseUrl"/>.
/// </remarks>
public sealed class PasswordResetService(
    UserManager<SgvIdentityUser> userManager,
    IEmailSender<SgvIdentityUser> emailSender,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private readonly IOptions<SmtpOptions> _smtpOptions = smtpOptions;

    public async Task<PasswordResetOutcome> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager
            .FindByNameAsync(request.UserNameOrEmail)
            .ConfigureAwait(false);

        if (user is null)
        {
            user = await userManager
                .FindByEmailAsync(request.UserNameOrEmail)
                .ConfigureAwait(false);
        }

        if (user is null)
        {
            // Anti-enumeration: log without leaking whether the
            // identifier was present, and return Success so the
            // controller collapses both branches into the same 200.
            logger.LogInformation(
                "Password recovery requested for unknown identifier (response masked).");
            return PasswordResetOutcome.Success;
        }

        var token = await userManager
            .GeneratePasswordResetTokenAsync(user)
            .ConfigureAwait(false);

        var resetLink = BuildRecoveryLink(user.Id, token);
        var body = BuildRecoveryBody(resetLink);

        await emailSender
            .SendPasswordResetLinkAsync(user, resetLink, body)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Password recovery email dispatched for userId={UserId}.",
            user.Id);

        return PasswordResetOutcome.Success;
    }

    public async Task<PasswordResetOutcome> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager
            .FindByIdAsync(request.UserId)
            .ConfigureAwait(false);

        if (user is null)
        {
            return PasswordResetOutcome.InvalidToken;
        }

        var isValid = await userManager
            .VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "ResetPassword", request.Token)
            .ConfigureAwait(false);

        if (!isValid)
        {
            logger.LogWarning(
                "Password reset rejected: invalid or expired token for userId={UserId}.",
                user.Id);
            return PasswordResetOutcome.InvalidToken;
        }

        var result = await userManager
            .ResetPasswordAsync(user, request.Token, request.NewPassword)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            // The token verified but the password itself did not
            // pass Identity's policy at runtime. Surface as
            // InvalidToken so the controller responds 400 — the
            // FluentValidation validator already enforces the same
            // shape up front, so this branch is reserved for policy
            // drift between startup and request time.
            logger.LogWarning(
                "Password reset failed for userId={UserId}: {Errors}",
                user.Id,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return PasswordResetOutcome.InvalidToken;
        }

        logger.LogInformation(
            "Password reset succeeded for userId={UserId} (SecurityStamp rotated by Identity).",
            user.Id);

        return PasswordResetOutcome.Success;
    }

    private string BuildRecoveryLink(string userId, string token)
    {
        var baseUrl = _smtpOptions.Value.WebBaseUrl.TrimEnd('/');
        var encodedUserId = Uri.EscapeDataString(userId);
        var encodedToken = Uri.EscapeDataString(token);
        return $"{baseUrl}/auth/reset-password?userId={encodedUserId}&token={encodedToken}";
    }

    private static string BuildRecoveryBody(string link) =>
        "<p>Recibimos un pedido para restablecer tu contraseña.</p>" +
        "<p>Si fuiste vos, hacé clic en el siguiente enlace:</p>" +
        $"<p><a href=\"{link}\">Restablecer contraseña</a></p>" +
        "<p>Si no realizaste esta solicitud, podés ignorar este mensaje.</p>" +
        "<p>El enlace caduca en una hora.</p>";
}
