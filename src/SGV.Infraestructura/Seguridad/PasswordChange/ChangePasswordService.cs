using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Seguridad.PasswordChange;
using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Infraestructura.Seguridad.PasswordChange;

/// <summary>
/// Changes an authenticated user's password through ASP.NET Core Identity.
/// </summary>
public sealed class ChangePasswordService(
    UserManager<SgvIdentityUser> userManager,
    ILogger<ChangePasswordService> logger) : IChangePasswordService
{
    /// <inheritdoc />
    public async Task<ChangePasswordOutcome> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return ChangePasswordOutcome.InvalidCurrentPassword;
        }

        var result = await userManager
            .ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return result.Errors.Any(error => error.Code == "PasswordMismatch")
                ? ChangePasswordOutcome.InvalidCurrentPassword
                : ChangePasswordOutcome.ValidationError;
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            logger.LogWarning(
                "Password changed but SecurityStamp rotation failed for userId={UserId}: {Errors}",
                user.Id,
                string.Join("; ", stampResult.Errors.Select(error => error.Description)));
        }

        logger.LogInformation("Password change succeeded for userId={UserId}.", user.Id);
        return ChangePasswordOutcome.Success;
    }
}
