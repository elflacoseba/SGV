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

    /// <summary>
    /// Validates a password reset token without consuming it. Used on GET
    /// to decide whether to show the reset form or an error.
    /// </summary>
    /// <param name="request">Token validation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see cref="PasswordResetOutcome.Success"/> when the token is valid;
    /// <see cref="PasswordResetOutcome.InvalidToken"/> otherwise.</returns>
    Task<PasswordResetOutcome> ValidateResetTokenAsync(
        ValidateResetTokenRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the password of the currently authenticated user. Requires a
    /// valid bearer token; calling this from an anonymous context yields a
    /// <c>401 Unauthorized</c> from the API which propagates as
    /// <see cref="HttpRequestException"/> with <c>StatusCode = 401</c>.
    /// </summary>
    /// <param name="request">Authenticated password-change payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discriminated outcome of the change-password operation.</returns>
    Task<ChangePasswordOutcome> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the refresh token against <c>POST /api/v1/auth/refresh</c>.
    /// The caller is responsible for persisting the returned
    /// <see cref="RefreshResponse.RefreshToken"/> in the <c>sgv.rt</c> cookie
    /// via <see cref="IRefreshTokenCookieAccessor.Set"/>.
    /// </summary>
    /// <param name="request">Anonymous refresh payload (the refresh token
    /// travels in the body — the API is body-based, <c>SGV.Web</c> is the
    /// only <c>sgv.rt</c> emitter).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new pair on success; <c>null</c> on
    /// <c>401 Unauthorized</c> or <c>429 Too Many Requests</c>. Other failures
    /// propagate as <see cref="HttpRequestException"/>.</returns>
    Task<RefreshResponse?> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /api/v1/auth/logout</c> to revoke the refresh-token
    /// family server-side. Uses the authenticated pipeline so the bearer
    /// token travels in the <c>Authorization</c> header.
    /// </summary>
    /// <param name="request">Logout payload. <see cref="LogoutRequest.RefreshToken"/>
    /// may be null for legacy sessions that never received a refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> on <c>200 OK</c>; <c>false</c> on
    /// <c>401 Unauthorized</c> (the session expired server-side). Other
    /// failures propagate as <see cref="HttpRequestException"/> so the caller
    /// can decide whether to fail-open and still clean local cookies.</returns>
    Task<bool> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);
}
