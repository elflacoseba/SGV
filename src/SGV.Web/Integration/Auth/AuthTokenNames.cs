namespace SGV.Web.Integration.Auth;

/// <summary>
/// Single source of truth for the authentication token names exchanged between
/// the cookie-auth handler and the typed <see cref="System.Net.Http.HttpClient"/>
/// pipeline that talks to SGV.Api. Keeping these names here prevents the
/// sign-in flow and the bearer-propagation handler from drifting apart.
/// </summary>
internal static class AuthTokenNames
{
    /// <summary>
    /// JWT bearer token issued by SGV.Api at login and consumed by
    /// <see cref="ApiBearerTokenHandler"/> on every outbound API call.
    /// </summary>
    public const string AccessToken = "access_token";

    /// <summary>
    /// ISO-8601 UTC timestamp at which the access token expires.
    /// </summary>
    public const string ExpiresAt = "expires_at";
}