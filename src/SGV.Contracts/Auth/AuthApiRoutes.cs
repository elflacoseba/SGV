namespace SGV.Contracts.Auth;

/// <summary>
/// Centralized authentication routes shared by SGV.Api and SGV.Web.
/// </summary>
public static class AuthApiRoutes
{
    /// <summary>
    /// Base route for authentication endpoints.
    /// </summary>
    public const string Base = "api/v1/auth";

    /// <summary>
    /// Relative login route.
    /// </summary>
    public const string LoginRelative = "login";

    /// <summary>
    /// Absolute login route path.
    /// </summary>
    public const string Login = "/" + Base + "/" + LoginRelative;

    /// <summary>
    /// Relative route for the password recovery request. Marked
    /// <c>[AllowAnonymous]</c>; see <c>AuthController</c> in
    /// <c>SGV.Api</c>.
    /// </summary>
    public const string ForgotPasswordRelative = "forgot-password";

    /// <summary>
    /// Absolute route for the password recovery request.
    /// </summary>
    public const string ForgotPassword = "/" + Base + "/" + ForgotPasswordRelative;

    /// <summary>
    /// Relative route for the password reset execution. Marked
    /// <c>[AllowAnonymous]</c>; see <c>AuthController</c> in
    /// <c>SGV.Api</c>.
    /// </summary>
    public const string ResetPasswordRelative = "reset-password";

    /// <summary>
    /// Absolute route for the password reset execution.
    /// </summary>
    public const string ResetPassword = "/" + Base + "/" + ResetPasswordRelative;

    /// <summary>
    /// Relative route for lightweight token validation (no password change).
    /// </summary>
    public const string ValidateResetTokenRelative = "validate-reset-token";

    /// <summary>
    /// Absolute route for the token-validation endpoint.
    /// </summary>
    public const string ValidateResetToken = "/" + Base + "/" + ValidateResetTokenRelative;

    /// <summary>
    /// Relative route for the authenticated password-change endpoint.
    /// </summary>
    public const string ChangePasswordRelative = "change-password";

    /// <summary>
    /// Absolute route for the authenticated password-change endpoint.
    /// </summary>
    public const string ChangePassword = "/" + Base + "/" + ChangePasswordRelative;

    /// <summary>
    /// Rate-limit policy name for the change-password endpoint.
    /// </summary>
    public const string ChangePasswordPolicyName = "ChangePassword";

    /// See <c>AuthController.ForgotPassword</c> in <c>SGV.Api</c>.
    /// </summary>
    public const string ForgotPasswordPolicyName = "ForgotPassword";

    /// <summary>
    /// Rate-limit policy name for the reset-password endpoint.
    /// See <c>AuthController.ResetPassword</c> in <c>SGV.Api</c>.
    /// </summary>
    public const string ResetPasswordPolicyName = "ResetPassword";

    /// <summary>
    /// PR1a (change <c>implementa-refresh-tokens</c>): relative route
    /// for the refresh endpoint. Marked <c>[AllowAnonymous]</c>; the
    /// refresh token travels in the request body, not a cookie —
    /// see <see cref="RefreshRequest"/>. Defined here in PR1a so the
    /// PR2 wiring can rely on the constant without re-litigating the
    /// string.
    /// </summary>
    public const string RefreshRelative = "refresh";

    /// <summary>
    /// PR1a: absolute route for the refresh endpoint.
    /// </summary>
    public const string Refresh = "/" + Base + "/" + RefreshRelative;

    /// <summary>
    /// PR1a: relative route for the logout endpoint. PR2 wires this as
    /// <c>[Authorize]</c>.
    /// </summary>
    public const string LogoutRelative = "logout";

    /// <summary>
    /// PR1a: absolute route for the logout endpoint.
    /// </summary>
    public const string Logout = "/" + Base + "/" + LogoutRelative;

    /// <summary>
    /// PR1a: name of the rate-limit policy applied to the refresh
    /// endpoint. PR4 wires the actual <c>AddPolicy</c> entry — PR1a
    /// only reserves the name so the constants stay locked early.
    /// </summary>
    public const string RefreshPolicyName = "Refresh";
}
