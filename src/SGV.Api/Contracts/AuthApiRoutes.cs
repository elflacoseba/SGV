namespace SGV.Api.Contracts;

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
}
