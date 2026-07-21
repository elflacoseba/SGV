using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Aplicacion.Seguridad.PasswordReset;

/// <summary>
/// Application-layer port that orchestrates the password recovery flow.
/// Implemented by <c>SGV.Infraestructura.Seguridad.PasswordResetService</c>
/// which depends on Identity (<see cref="Microsoft.AspNetCore.Identity.UserManager{TUser}"/>)
/// plus <see cref="Microsoft.AspNetCore.Identity.IEmailSender{TUser}"/>.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="SGV.Aplicacion.Seguridad.Usuarios.IAuthServicio"/>
/// because authentication and credential recovery are different
/// concerns (SRP): the former validates credentials and emits tokens,
/// the latter manages the out-of-band recovery handshake via email.
/// </remarks>
public interface IPasswordResetService
{
    /// <summary>
    /// Handles a <see cref="ForgotPasswordRequest"/> by trying to find
    /// a matching user (<c>UserName</c> or <c>Email</c>) and emailing a
    /// recovery link when found. The caller MUST treat every outcome
    /// the same way (silent success) so that unknown identifiers cannot
    /// be enumerated.
    /// </summary>
    Task<PasswordResetOutcome> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the supplied token and, on success, rotates the user's
    /// password via Identity. On invalid or expired tokens the result is
    /// <see cref="PasswordResetOutcome.InvalidToken"/>; the controller
    /// maps that to <c>400 Bad Request</c>.
    /// </summary>
    Task<PasswordResetOutcome> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a password reset token without consuming it or
    /// changing the password. Used by the Web shell on GET to decide
    /// whether to show the password form or an error page. Returns
    /// <see cref="PasswordResetOutcome.InvalidToken"/> when the token
    /// is invalid, expired, or the user no longer exists.
    /// </summary>
    Task<PasswordResetOutcome> ValidateResetTokenAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Discriminated result type for the recovery flow. The values are
/// explicit on purpose so the controller does not need to look at HTTP
/// status codes inside Identity to know what to do.
/// </summary>
public enum PasswordResetOutcome
{
    /// <summary>
    /// Operation succeeded. For
    /// <see cref="IPasswordResetService.ForgotPasswordAsync"/> this
    /// also covers the silent path where no user matched (the caller
    /// MUST collapse it into the same response).
    /// </summary>
    Success = 0,

    /// <summary>
    /// <see cref="IPasswordResetService.ResetPasswordAsync"/> only:
    /// the supplied token is invalid, expired, or already consumed.
    /// Maps to <c>400 Bad Request</c>.
    /// </summary>
    InvalidToken = 1,

    /// <summary>
    /// <see cref="IPasswordResetService.ForgotPasswordAsync"/> only:
    /// no user matched the supplied identifier. The caller MUST
    /// silently collapse this into <see cref="Success"/>.
    /// </summary>
    UserNotFound = 2,

    /// <summary>
    /// The request tripped the rate limiter before the service was
    /// invoked. The middleware maps this to <c>429</c>; the service
    /// never produces it on its own.
    /// </summary>
    RateLimited = 3,

    /// <summary>
    /// Reserved for the FluentValidation pipeline when the request body
    /// fails input validation.
    /// </summary>
    ValidationError = 4
}
