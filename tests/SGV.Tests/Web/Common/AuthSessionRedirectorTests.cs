using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGV.Web.Integration.Common;
using Xunit;

namespace SGV.Tests.Web.Common;

/// <summary>
/// Tests del helper <see cref="IAuthSessionRedirector"/> introducido en
/// #125 (Slice 3). Cubren los seis casos del design §11.3 incluyendo el
/// guard anti open-redirect (URLs absolutas externas y protocol-relative).
/// <para>
/// El helper vive en <c>SGV.Web.Integration.Common</c> y queda expuesto
/// al assembly de tests vía <c>InternalsVisibleTo</c> (ver
/// <c>src/SGV.Web/Program.cs</c>); los tests crean un
/// <see cref="DefaultHttpContext"/> con <c>Request.Host = "localhost"</c>
/// y un <see cref="HttpContextAccessor"/>, suficiente para ejercitar las
/// ramas del switch sin necesidad del harness web completo.
/// </para>
/// </summary>
public sealed class AuthSessionRedirectorTests
{
    /// <summary>
    /// Construye un <see cref="IAuthSessionRedirector"/> con un HttpContext
    /// pre-poblado (Request.Host = "localhost") listo para invocar el
    /// helper sin tener que bootear el harness web.
    /// </summary>
    private static IAuthSessionRedirector BuildRedirector(out DefaultHttpContext context, string? requestPath = "/Organizacion/Cargos")
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("localhost");
        http.Request.Path = requestPath;
        http.Request.Scheme = "https";
        context = http;

        var accessor = new HttpContextAccessor { HttpContext = http };
        return new AuthSessionRedirector(accessor);
    }

    // ─────────────────────────────────────────────────────────────────
    // Casos del design §11.3
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TryRedirectToLogin_NoHttpContext_ReturnsNull()
    {
        // Arrange: accessor con HttpContext=null (caso típico de tests sin host)
        var accessor = new HttpContextAccessor { HttpContext = null };
        var redirector = new AuthSessionRedirector(accessor);

        // Act
        var result = redirector.TryRedirectToLogin("/organizacion/cargos");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryRedirectToLogin_WithLocalPath_PreservesReturnUrl()
    {
        // Arrange: returnUrl local absoluto (empieza con "/")
        var redirector = BuildRedirector(out _);

        // Act
        var result = redirector.TryRedirectToLogin("/organizacion/cargos");

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/auth/sign-in?returnUrl=%2Forganizacion%2Fcargos", redirect.Url);
    }

    [Fact]
    public void TryRedirectToLogin_WithAbsoluteExternalUrl_DropsReturnUrl_RedirectsToLogin()
    {
        // Arrange: URL absoluta con host externo (atacante)
        var redirector = BuildRedirector(out _);

        // Act
        var result = redirector.TryRedirectToLogin("https://evil.example.com/oauth");

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/auth/sign-in", redirect.Url);
    }

    [Fact]
    public void TryRedirectToLogin_WithProtocolRelativeUrl_DropsReturnUrl_RedirectsToLogin()
    {
        // Arrange: URL protocol-relative (//host/path) — vector clásico de open-redirect
        var redirector = BuildRedirector(out _);

        // Act
        var result = redirector.TryRedirectToLogin("//evil.example.com/oauth");

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/auth/sign-in", redirect.Url);
    }

    [Fact]
    public void TryRedirectToLogin_WithLoopbackAbsoluteUrl_PreservesReturnUrl()
    {
        // Arrange: URL absoluta con IsLoopback=true (127.0.0.1) sigue siendo local
        var redirector = BuildRedirector(out _);

        // Act
        var result = redirector.TryRedirectToLogin("http://127.0.0.1:5500/organizacion/cargos");

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("/auth/sign-in?returnUrl=", redirect.Url);
        Assert.Contains("127.0.0.1", redirect.Url);
    }

    [Fact]
    public void TryRedirectToLogin_EmptyPath_OmitsReturnUrl()
    {
        // Arrange: returnUrl vacío o whitespace
        var redirector = BuildRedirector(out _);

        // Act
        var result = redirector.TryRedirectToLogin("   ");

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/auth/sign-in", redirect.Url);
    }

    /// <summary>
    /// Vectores clásicos de open-redirect que el guard anti-F9 debe
    /// rechazar descartando el <c>returnUrl</c> y redirigiendo al login
    /// sin él. La lista cubre tanto URLs absolutas externas como
    /// variantes que confunden al parser de URI (credenciales embebidas,
    /// backslashes, control chars y esquemas no-http).
    /// </summary>
    [Theory]
    [InlineData("https://evil.example.com/oauth")]
    [InlineData("//evil.example.com/oauth")]
    [InlineData("https://user:pass@evil.example.com/")]
    [InlineData("https://safe.example.com@evil.example.com/")]
    [InlineData("/\\evil.example.com/oauth")]      // algunos browsers interpretan /\\ como //
    [InlineData("\\\\evil.example.com\\oauth")]    // UNC-style
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData(" https://evil.example.com")]       // leading whitespace
    [InlineData("https://evil.example.com\n/oauth")] // embedded newline
    [InlineData("HTTP://EVIL.EXAMPLE.COM/oauth")]  // case-fold
    [InlineData("HtTpS://EvIl.ExAmPlE.cOm/oAuth")]  // mixed case
    public void TryRedirectToLogin_WithMaliciousReturnUrl_DropsReturnUrl(string maliciousReturnUrl)
    {
        // Arrange
        var redirector = BuildRedirector(out _);

        // Act
        var result = redirector.TryRedirectToLogin(maliciousReturnUrl);

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/auth/sign-in", redirect.Url);
    }

    /// <summary>
    /// URLs que NO son vectores open-redirect y deben preservarse como
    /// <c>returnUrl</c> local válido. Complementa el test anterior para
    /// garantizar que el guard no rechaza en exceso (falso positivo).
    /// </summary>
    [Theory]
    [InlineData("/organizacion/cargos")]
    [InlineData("/auth/sign-in")]                  // mismo path, válido
    [InlineData("/api/v1/usuarios?page=1")]        // query string
    [InlineData("/path/with#fragment")]             // fragment
    [InlineData("/path-with-dashes_and_underscores")] // caracteres seguros
    public void TryRedirectToLogin_WithLocalReturnUrl_PreservesReturnUrl(string localReturnUrl)
    {
        // Arrange
        var redirector = BuildRedirector(out _);

        // Act
        var result = redirector.TryRedirectToLogin(localReturnUrl);

        // Assert
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("/auth/sign-in?returnUrl=", redirect.Url);
        Assert.NotEqual("/auth/sign-in", redirect.Url);
    }
}