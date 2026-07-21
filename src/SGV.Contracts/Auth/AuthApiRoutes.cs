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
}
