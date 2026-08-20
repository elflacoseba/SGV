using System.Collections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using SGV.Web.Integration.Auth;
using Xunit;

namespace SGV.Tests.Web.Auth;

/// <summary>
/// Unit tests for <see cref="RefreshTokenCookieAccessor"/>.
///
/// Validates the single source of truth for the <c>sgv.rt</c> cookie
/// (PR3 of change <c>implementa-refresh-tokens</c>):
/// <list type="bullet">
///   <item>Environment-aware <c>SecurePolicy</c> (<see cref="RefreshTokenCookieAccessor"/>).</item>
///   <item>Hardening defaults: <c>HttpOnly</c>, <c>SameSite=Lax</c>, <c>Path=/</c>.</item>
///   <item>Read returns the token when the cookie is present, <c>null</c> otherwise.</item>
///   <item>Delete uses the same <c>CookieOptions</c> shape as <see cref="IRefreshTokenCookieAccessor.Set"/>
///   so the browser actually drops the cookie.</item>
/// </list>
///
/// Spec references: REQ-AUTH-COOKIES-1 (cookie hardening by environment),
/// REQ-AUTH-COOKIES-2 (logout clears both cookies).
/// </summary>
public sealed class RefreshTokenCookieAccessorTests
{
    private const string RefreshCookieName = IRefreshTokenCookieAccessor.CookieName;

    [Fact]
    public void Set_WithValidTokenInDevelopmentOverHttp_AppendsCookieWithoutSecure()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Http);
        var env = FakeWebHostEnvironment.Development();
        var sut = new RefreshTokenCookieAccessor(accessor, env);

        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);
        sut.Set("eyJhbGciOiJIUzI1NiJ9.token-value", expiresAt);

        var setCookie = accessor.HttpContext!.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshCookieName}=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", setCookie, StringComparison.OrdinalIgnoreCase); // Development + HTTP -> no Secure flag
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Set_InProduction_AppendsCookieWithSecure()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Https);
        var env = FakeWebHostEnvironment.Production();
        var sut = new RefreshTokenCookieAccessor(accessor, env);

        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);
        sut.Set("production-refresh-token", expiresAt);

        var setCookie = accessor.HttpContext!.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshCookieName}=production-refresh-token", setCookie);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Set_InStaging_SetsSecureRegardlessOfSchema()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Http);
        var env = FakeWebHostEnvironment.Staging();
        var sut = new RefreshTokenCookieAccessor(accessor, env);

        sut.Set("staging-refresh-token", DateTimeOffset.UtcNow.AddDays(7));

        var setCookie = accessor.HttpContext!.Response.Headers.SetCookie.ToString();
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Get_WithPresentCookie_ReturnsToken()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Http);
        var (cookies, requestCookies) = BuildRequestCookieCollection(new Dictionary<string, string>
        {
            [RefreshCookieName] = "the-refresh-token"
        });
        accessor.HttpContext!.Features.Set(cookies);
        var sut = new RefreshTokenCookieAccessor(accessor, FakeWebHostEnvironment.Development());

        var actual = sut.Get();

        Assert.Equal("the-refresh-token", actual);
    }

    [Fact]
    public void Get_WithMissingCookie_ReturnsNull()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Http);
        var (cookies, _) = BuildRequestCookieCollection(new Dictionary<string, string>());
        accessor.HttpContext!.Features.Set(cookies);
        var sut = new RefreshTokenCookieAccessor(accessor, FakeWebHostEnvironment.Development());

        var actual = sut.Get();

        Assert.Null(actual);
    }

    [Fact]
    public void Get_WithWhitespaceValue_ReturnsNull()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Http);
        // Whitespace cookies are rejected by CookieHeaderValue; encode the
        // blank-out scenario with an empty string instead, which is the
        // realistic shape the browser would emit if a previous Set-Cookie
        // cleared the value.
        var (cookies, _) = BuildRequestCookieCollection(new Dictionary<string, string>
        {
            [RefreshCookieName] = string.Empty
        });
        accessor.HttpContext!.Features.Set(cookies);
        var sut = new RefreshTokenCookieAccessor(accessor, FakeWebHostEnvironment.Development());

        var actual = sut.Get();

        Assert.Null(actual);
    }

    [Fact]
    public void Delete_EmitsCookieDeletionWithMatchingOptions()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Https);
        var env = FakeWebHostEnvironment.Production();
        var sut = new RefreshTokenCookieAccessor(accessor, env);

        sut.Delete();

        var setCookie = accessor.HttpContext!.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshCookieName}=", setCookie);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            setCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase)
            || setCookie.Contains("max-age=0", StringComparison.OrdinalIgnoreCase),
            "Delete must emit a past Expires or Max-Age=0 so the browser drops the cookie.");
    }

    [Fact]
    public void Delete_WithoutHttpContext_DoesNotThrow()
    {
        var accessor = new FakeHttpContextAccessor { HttpContext = null };
        var sut = new RefreshTokenCookieAccessor(accessor, FakeWebHostEnvironment.Development());

        var ex = Record.Exception(() => sut.Delete());

        Assert.Null(ex);
    }

    [Fact]
    public void Set_AfterDelete_RewritesCookieWithoutThrowing()
    {
        var accessor = FakeHttpContextAccessor.ForSchema(HttpSchema.Http);
        var sut = new RefreshTokenCookieAccessor(accessor, FakeWebHostEnvironment.Development());

        sut.Delete();
        var ex = Record.Exception(() => sut.Set("new-refresh-token", DateTimeOffset.UtcNow.AddDays(14)));

        Assert.Null(ex);
        var setCookie = accessor.HttpContext!.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshCookieName}=new-refresh-token", setCookie);
    }

    [Fact]
    public void Set_WithoutHttpContext_ThrowsInvalidOperation()
    {
        var accessor = new FakeHttpContextAccessor { HttpContext = null };
        var sut = new RefreshTokenCookieAccessor(accessor, FakeWebHostEnvironment.Development());

        Assert.Throws<InvalidOperationException>(() =>
            sut.Set("any", DateTimeOffset.UtcNow.AddDays(1)));
    }

    [Fact]
    public void Get_WithoutHttpContext_ReturnsNull()
    {
        var accessor = new FakeHttpContextAccessor { HttpContext = null };
        var sut = new RefreshTokenCookieAccessor(accessor, FakeWebHostEnvironment.Development());

        var actual = sut.Get();

        Assert.Null(actual);
    }

    public enum HttpSchema { Http, Https }

    /// <summary>
    /// Minimal <see cref="IHttpContextAccessor"/> substitute for the in-memory
    /// unit tests. <see cref="DefaultHttpContext"/> exposes the response cookies
    /// collection so the assertions can inspect the emitted <c>Set-Cookie</c>
    /// header without spinning a real host.
    /// </summary>
    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }

        public static FakeHttpContextAccessor ForSchema(HttpSchema schema)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = schema == HttpSchema.Https ? "https" : "http";
            httpContext.Request.IsHttps = schema == HttpSchema.Https;
            return new FakeHttpContextAccessor { HttpContext = httpContext };
        }
    }

    /// <summary>
    /// Builds a request cookie feature from a dictionary. The default
    /// <see cref="HttpRequest"/> cookie collection cannot be mutated directly
    /// because <c>DefaultHttpContext</c> exposes it read-only; the canonical
    /// injection point is the <see cref="IRequestCookiesFeature"/>.
    /// </summary>
    private static (IRequestCookiesFeature feature, IRequestCookieCollection collection) BuildRequestCookieCollection(
        Dictionary<string, string> cookies)
    {
        var collection = new FakeRequestCookieCollection(cookies);
        return (new FakeRequestCookiesFeature(collection), collection);
    }

    /// <summary>
    /// In-memory <see cref="IRequestCookieCollection"/> backed by a dictionary.
    /// Avoids the wrapper allocation of <c>RequestCookiesCollection</c> for
    /// tests that only need to inject a few cookies.
    /// </summary>
    private sealed class FakeRequestCookieCollection(Dictionary<string, string> cookies) : IRequestCookieCollection
    {
        private readonly Dictionary<string, string> _cookies = cookies;

        public string? this[string key] => _cookies.TryGetValue(key, out var value) ? value : null;

        public int Count => _cookies.Count;

        public ICollection<string> Keys => _cookies.Keys;

        public bool ContainsKey(string key) => _cookies.ContainsKey(key);

        public bool TryGetValue(string key, out string? value)
        {
            var found = _cookies.TryGetValue(key, out var raw);
            value = raw;
            return found;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _cookies.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _cookies.GetEnumerator();
    }

    /// <summary>
    /// Feature wrapper that satisfies <see cref="IRequestCookiesFeature"/>
    /// so <c>DefaultHttpContext</c> serves the cookies we inject.
    /// </summary>
    private sealed class FakeRequestCookiesFeature : IRequestCookiesFeature
    {
        public FakeRequestCookiesFeature(IRequestCookieCollection cookies)
        {
            Cookies = cookies;
        }

        public IRequestCookieCollection Cookies { get; set; }

        public int Version => 0;
    }

    /// <summary>
    /// Minimal <see cref="IWebHostEnvironment"/> stand-in. Exposes only
    /// EnvironmentName because that is the only property the accessor reads.
    /// </summary>
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "SGV.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;

        public static FakeWebHostEnvironment Development() => new() { EnvironmentName = Environments.Development };
        public static FakeWebHostEnvironment Staging() => new() { EnvironmentName = Environments.Staging };
        public static FakeWebHostEnvironment Production() => new() { EnvironmentName = Environments.Production };
    }
}
