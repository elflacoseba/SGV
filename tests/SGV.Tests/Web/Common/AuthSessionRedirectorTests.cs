using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
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
/// y un <see cref="IUrlHelperFactory"/> falso que devuelve paths
/// predecibles, suficiente para ejercitar las ramas del switch sin
/// necesidad del harness web completo.
/// </para>
/// </summary>
public sealed class AuthSessionRedirectorTests
{
    /// <summary>
    /// Fábrica de <see cref="IUrlHelper"/> falsa. Devuelve un
    /// <see cref="DummyUrlHelper"/> con la ruta esperada si la página
    /// solicitada coincide; <c>null</c> en caso contrario, espejando el
    /// comportamiento real de <see cref="UrlHelperFactory"/>.
    /// </summary>
    private sealed class FakeUrlHelperFactory : IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context) => new DummyUrlHelper(context);
    }

    /// <summary>
    /// <see cref="IUrlHelper"/> que devuelve paths predecibles:
    /// <c>"/auth/sign-in"</c> sin argumentos, o
    /// <c>"/auth/sign-in?returnUrl=…"</c> cuando se pasa
    /// <c>new { returnUrl = … }</c>. Permite assertar el redirect
    /// emitido sin un motor de routing real.
    /// </summary>
    private sealed class DummyUrlHelper : IUrlHelper
    {
        private readonly ActionContext _context;

        public DummyUrlHelper(ActionContext context)
        {
            _context = context;
            ActionContext = context;
        }

        public ActionContext ActionContext { get; }

        public string? Action(UrlActionContext urlActionContext) => null;

        public string? Content(string? contentPath) => null;

        public bool IsLocalUrl(string? url) => url is not null
            && url.StartsWith("/", System.StringComparison.Ordinal)
            && !url.StartsWith("//", System.StringComparison.Ordinal)
            && !url.StartsWith("/\\", System.StringComparison.Ordinal);

        public string? Link(string? routeName, object? values) => null;

        public string? RouteUrl(UrlRouteContext routeContext) => null;

        public string? Page(string pageName, object? values) => BuildPage(pageName, values);

        private static string? BuildPage(string pageName, object? values)
        {
            if (!string.Equals(pageName, "/Auth/SignIn", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var returnUrl = TryGetReturnUrl(values);
            return string.IsNullOrWhiteSpace(returnUrl)
                ? "/auth/sign-in"
                : $"/auth/sign-in?returnUrl={System.Uri.EscapeDataString(returnUrl)}";
        }

        private static string? TryGetReturnUrl(object? values)
        {
            if (values is null) return null;

            var prop = values.GetType().GetProperty("returnUrl");
            if (prop is null) return null;

            var raw = prop.GetValue(values) as string;
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }
    }

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
        var factory = new FakeUrlHelperFactory();
        return new AuthSessionRedirector(accessor, factory);
    }

    // ─────────────────────────────────────────────────────────────────
    // Casos del design §11.3
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TryRedirectToLogin_NoHttpContext_ReturnsNull()
    {
        // Arrange: accessor con HttpContext=null (caso típico de tests sin host)
        var accessor = new HttpContextAccessor { HttpContext = null };
        var factory = new FakeUrlHelperFactory();
        var redirector = new AuthSessionRedirector(accessor, factory);

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
}