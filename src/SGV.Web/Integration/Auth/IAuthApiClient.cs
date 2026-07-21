using SGV.Contracts.Seguridad.Usuarios;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// Authentication client abstraction used by SGV.Web.
/// </summary>
public interface IAuthApiClient
{
    /// <summary>
    /// Attempts to authenticate a user against SGV.Api.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The login response when authentication succeeds; otherwise null.</returns>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a password recovery email without requiring an authenticated session.
    /// </summary>
    /// <param name="request">Password recovery identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The password recovery outcome.</returns>
    Task<PasswordResetOutcome> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a new password using a recovery token without requiring an authenticated session.
    /// </summary>
    /// <param name="request">Password reset payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The password reset outcome.</returns>
    Task<PasswordResetOutcome> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
