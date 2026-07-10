using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Verifies that the cookie authentication options registered by <c>SGV.Web/Program.cs</c>
/// expose environment-aware security attributes.
///
/// The cookie carries the JWT that <see cref="SGV.Web.Integration.Auth.ApiBearerTokenHandler"/>
/// forwards to <c>SGV.Api</c>, so its attributes are the only line of defence against cookie
/// theft in production. The expected matrix is fixed by spec
/// <c>sgv-web-authentication</c>:
/// <list type="bullet">
/// <item><c>HttpOnly</c> always <c>true</c>;</item>
/// <item><c>SameSite</c> always <c>Lax</c>;</item>
/// <item><c>SecurePolicy</c> is <c>Always</c> outside <c>Development</c> and
/// <c>SameAsRequest</c> in <c>Development</c>.</item>
/// </list>
/// </summary>
public sealed class WebCookieAuthenticationOptionsTests
{
    private const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    private const string WebBaseUrlConfigKey = "SgvApi:BaseUrl";
    private const string DevWebBaseUrl = "https://api.example.com";

    [Fact]
    public void WebCookieAuthOptions_Production_SecurePolicyAlways()
    {
        // Arrange — Production env with a valid SgvApi:BaseUrl so the
        // ValidateOnStart on SgvApiOptions does not block host construction.
        using var factory = new WebApplicationFactory<SGV.Web.Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Production")
                .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [WebBaseUrlConfigKey] = DevWebBaseUrl
                    })));

        using var client = factory.CreateClient();

        // Act — read the cookie options registered for the cookie scheme.
        var cookie = ResolveCookieOptions(factory);

        // Assert — production matrix applies.
        Assert.True(cookie.HttpOnly, "Cookie.HttpOnly must be true in production.");
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
    }

    [Fact]
    public void WebCookieAuthOptions_Development_SecurePolicySameAsRequest()
    {
        // Arrange — Development env with a valid SgvApi:BaseUrl override.
        using var factory = new WebApplicationFactory<SGV.Web.Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [WebBaseUrlConfigKey] = DevWebBaseUrl
                    })));

        using var client = factory.CreateClient();

        // Act
        var cookie = ResolveCookieOptions(factory);

        // Assert — development matrix applies.
        Assert.True(cookie.HttpOnly, "Cookie.HttpOnly must be true in development.");
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, cookie.SecurePolicy);
    }

    private static CookieBuilder ResolveCookieOptions(WebApplicationFactory<SGV.Web.Program> factory)
    {
        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var options = monitor.Get(CookieScheme);
        return options.Cookie;
    }
}