using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Common;

/// <summary>
/// Implementación por defecto de <see cref="IAuthSessionRedirector"/>
/// basada en <see cref="IHttpContextAccessor"/>. Verbatim del design
/// §6.1 del change <c>2026-07-13-taxonomia-errores-commandresult</c>
/// (issue #125), con la salvedad de que el helper construye el URL
/// destino directamente (sin delegar a <c>UrlHelper.Page(...)</c>):
/// <c>Page</c> es una extension method estática sobre
/// <see cref="IUrlHelper"/> que requiere un <c>ActionContext</c> con
/// routing data completo; el helper se invoca desde un PageModel que
/// sí lo tiene pero no queremos que el cálculo del redirect falle
/// silenciosamente por una mala resolución de routing. Construir el
/// path manualmente elimina ese acoplamiento y mantiene el contrato
/// observacional del design (redirect a <c>/auth/sign-in</c> con
/// <c>returnUrl</c> cuando es local).
/// <para>
/// Guard anti open-redirect (F9): rechaza URLs absolutas externas y
/// protocol-relative (<c>//host/path</c>). URLs loopback se preservan
/// porque el helper considera local cualquier URL cuyo host sea
/// <see cref="Uri.IsLoopback"/> o coincida con el
/// <see cref="HostString.Host"/> del request vigente.
/// </para>
/// </summary>
internal sealed class AuthSessionRedirector(
    IHttpContextAccessor accessor) : IAuthSessionRedirector
{
    /// <summary>
    /// Login path canónico. Espejo del <c>LoginPath</c> configurado en
    /// <c>src/SGV.Web/Program.cs</c> dentro del cookie authentication
    /// scheme (<c>/auth/sign-in</c>).
    /// </summary>
    private const string SignInPath = "/auth/sign-in";

    /// <inheritdoc />
    public IActionResult? TryRedirectToLogin(string? returnUrl = null)
    {
        var ctx = accessor.HttpContext;
        if (ctx is null)
        {
            return null;
        }

        var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
                            && IsLocalUrl(returnUrl, ctx)
            ? returnUrl
            : null;

        var target = safeReturnUrl is null
            ? SignInPath
            : $"{SignInPath}?returnUrl={Uri.EscapeDataString(safeReturnUrl)}";

        return new RedirectResult(target);
    }

    /// <summary>
    /// Devuelve <see langword="true"/> cuando <paramref name="url"/> puede
    /// considerarse un path local: rutas relativas que empiezan con
    /// <c>/</c> (excepto protocol-relative <c>//</c> o escapes de barra
    /// invertida <c>/\\</c>), o URLs absolutas cuyo host es
    /// <see cref="Uri.IsLoopback"/> o coincide con el host del request
    /// vigente. URLs externas (<c>https://attacker.example/...</c>) se
    /// rechazan siempre para mitigar open-redirect.
    /// </summary>
    private static bool IsLocalUrl(string url, HttpContext ctx)
    {
        // Guard defensivo: rechaza protocol-relative y escapes antes de
        // intentar Uri.TryCreate, que de otro modo aceptaría "//host" como
        // URI absoluta.
        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.IsLoopback
                   || string.Equals(
                       absolute.Host,
                       ctx.Request.Host.Host,
                       StringComparison.OrdinalIgnoreCase);
        }

        return url.StartsWith("/", StringComparison.Ordinal)
               && !url.StartsWith("//", StringComparison.Ordinal)
               && !url.StartsWith("/\\", StringComparison.Ordinal);
    }
}