namespace SGV.Web.Integration.Auth;

/// <summary>
/// Single source of truth for the <c>sgv.rt</c> refresh-cookie in SGV.Web.
///
/// Centralises the environment-aware hardening so no PageModel touches
/// <see cref="IRequestCookieCollection"/> or <see cref="IResponseCookies"/>
/// directly. <see cref="Set"/> and <see cref="Delete"/> MUST share the same
/// <see cref="CookieOptions"/> shape (path, SameSite, SecurePolicy) — the
/// browser only drops a cookie when the delete instruction matches the
/// options used at emission time.
/// </summary>
/// <remarks>
/// Change <c>implementa-refresh-tokens</c> PR3. Spec: REQ-AUTH-COOKIES-1
/// (cookie hardening by environment), REQ-AUTH-COOKIES-2 (logout clears
/// both cookies). Design: <c>sdd/implementa-refresh-tokens/design</c> §2.6.
///
/// SGV.Web is the only emitter of <c>sgv.rt</c>: the API is body-based
/// because <see cref="System.Net.Http.HttpClient"/> calls between SGV.Web
/// and SGV.Api are server-to-server and the <c>Set-Cookie</c> header never
/// reaches the browser.
/// </remarks>
public interface IRefreshTokenCookieAccessor
{
    /// <summary>
    /// Cookie name on the browser. Reserved for the refresh token emitted
    /// by <c>POST /api/v1/auth/login</c> and rotated by
    /// <c>POST /api/v1/auth/refresh</c>.
    /// </summary>
    public const string CookieName = "sgv.rt";

    /// <summary>
    /// Writes the refresh cookie on the current HTTP response.
    /// </summary>
    /// <param name="refreshToken">Plain refresh token returned by the API.</param>
    /// <param name="expiresAt">Absolute expiration from the API response.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <see cref="HttpContext"/> is available (background work).
    /// </exception>
    void Set(string refreshToken, DateTimeOffset expiresAt);

    /// <summary>
    /// Reads the refresh cookie from the current HTTP request. Returns
    /// <c>null</c> when the cookie is absent or carries a blank value.
    /// </summary>
    string? Get();

    /// <summary>
    /// Deletes the refresh cookie on the current HTTP response. Uses the
    /// same <see cref="CookieOptions"/> shape as <see cref="Set"/> so the
    /// browser honours the deletion.
    /// </summary>
    void Delete();
}
