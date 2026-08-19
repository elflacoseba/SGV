using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace SGV.Web.Integration.Auth;

/// <summary>
/// DI-backed implementation of <see cref="IRefreshTokenCookieAccessor"/>.
/// Resolves the current HTTP context via <see cref="IHttpContextAccessor"/>
/// and derives the <see cref="CookieSecurePolicy"/> from
/// <see cref="IWebHostEnvironment"/> — exactly the same expression that
/// Program.cs uses for the <c>sgv.auth</c> cookie (issue #101).
/// </summary>
/// <remarks>
/// Change <c>implementa-refresh-tokens</c> PR3. Spec: REQ-AUTH-COOKIES-1.
/// Design: <c>sdd/implementa-refresh-tokens/design</c> §2.6.
///
/// <para>
/// <b>Environment policy:</b>
/// <list type="table">
///   <item><term>Development</term><description><see cref="CookieSecurePolicy.SameAsRequest"/> — the <c>Secure</c> flag is emitted only when the request is HTTPS.</description></item>
///   <item><term>Staging / Production / other</term><description><see cref="CookieSecurePolicy.Always"/> — the <c>Secure</c> flag is always emitted.</description></item>
/// </list>
/// </para>
/// <para>
/// Singleton: no mutable state, only reads <see cref="IWebHostEnvironment"/>
/// at invocation time. The accessor must resolve the current
/// <see cref="HttpContext"/> through the scoped
/// <see cref="IHttpContextAccessor"/>; if the context is null (background
/// work), <see cref="Set"/> throws and <see cref="Get"/>/<see cref="Delete"/>
/// are no-ops.
/// </para>
/// </remarks>
public sealed class RefreshTokenCookieAccessor : IRefreshTokenCookieAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Creates the cookie accessor.
    /// </summary>
    /// <param name="httpContextAccessor">Resolves the current HTTP context.</param>
    /// <param name="environment">Active web host environment.</param>
    public RefreshTokenCookieAccessor(
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <inheritdoc />
    public void Set(string refreshToken, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var context = RequireHttpContext();
        var options = BuildOptions(expiresAt, context.Request.IsHttps);
        context.Response.Cookies.Append(
            IRefreshTokenCookieAccessor.CookieName,
            refreshToken,
            options);
    }

    /// <inheritdoc />
    public string? Get()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        return context.Request.Cookies.TryGetValue(IRefreshTokenCookieAccessor.CookieName, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    /// <inheritdoc />
    public void Delete()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        // BuildOptions(null, isHttps) keeps the same Path / SameSite / Secure shape
        // as Set(...) so the browser honours the deletion. Expires is intentionally
        // left null so IResponseCookies.Delete emits the canonical past-date.
        var options = BuildOptions(expiresAt: null, context.Request.IsHttps);
        context.Response.Cookies.Delete(
            IRefreshTokenCookieAccessor.CookieName,
            options);
    }

    /// <summary>
    /// Builds the canonical <see cref="CookieOptions"/> shape used by both
    /// emission and deletion. Path, SameSite and Secure MUST match the shape
    /// used at <see cref="Set"/> time — the browser only drops a cookie when the
    /// delete instruction carries the same path and security flags.
    /// </summary>
    /// <param name="expiresAt">Absolute expiration for emission; <c>null</c> for deletion.</param>
    /// <param name="isHttps">Whether the inbound request is HTTPS. Drives the
    /// <c>Secure</c> flag in Development (same shape as
    /// <see cref="CookieSecurePolicy.SameAsRequest"/>).</param>
    private CookieOptions BuildOptions(DateTimeOffset? expiresAt, bool isHttps) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        // .NET 10 removed SecurePolicy from CookieOptions; we compute the
        // equivalent flag directly. Same expression as Program.cs:54 for the
        // sgv.auth cookie (issue #101): Development -> SameAsRequest, others
        // -> Always.
        Secure = _environment.IsDevelopment() ? isHttps : true,
        Path = "/",
        Expires = expiresAt
    };

    /// <summary>
    /// Returns the current <see cref="HttpContext"/> or throws when none is
    /// available. <see cref="Set"/> cannot proceed without a response.
    /// </summary>
    private HttpContext RequireHttpContext()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            throw new InvalidOperationException(
                "RefreshTokenCookieAccessor.Set requires an active HttpContext. "
                + "Refresh cookies cannot be emitted outside of a request pipeline.");
        }

        return context;
    }
}
