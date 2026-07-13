using Microsoft.AspNetCore.Mvc;

namespace SGV.Web.Integration.Common;

/// <summary>
/// Helper inyectable que traduce <see cref="SGV.Contracts.Comun.ErrorCategoria.Unauthorized"/>
/// en una redirección a <c>/auth/sign-in?returnUrl=...</c> en lugar de
/// mostrar el formulario con un mensaje inline. La decisión queda en el
/// <c>PageModel</c> para mantener simetría con el resto de la frontera
/// de auth (no es un middleware cross-cutting).
/// <para>
/// Implementación por defecto: <see cref="AuthSessionRedirector"/>. El
/// guard anti open-redirect rechaza URLs absolutas externas y
/// protocol-relative; URLs loopback se preservan (forward-compat con
/// deep-links internos que llegan con host:port explícito).
/// </para>
/// </summary>
public interface IAuthSessionRedirector
{
    /// <summary>
    /// Si existe <see cref="Microsoft.AspNetCore.Http.HttpContext"/> y
    /// <paramref name="returnUrl"/> es local, emite un
    /// <see cref="RedirectResult"/> a <c>/auth/sign-in</c> con
    /// <c>returnUrl</c> preservado. Si el <c>returnUrl</c> NO es local
    /// (URL absoluta externa, protocolo distinto, o path externo), se
    /// ignora silenciosamente para mitigar open-redirect.
    /// Devuelve el <see cref="IActionResult"/> si redirigió, o
    /// <see langword="null"/> si no hay contexto (tests sin host).
    /// </summary>
    /// <param name="returnUrl">
    /// Path local al que el usuario quería llegar antes de la sesión
    /// expirada. Si es <see langword="null"/> o no local, se omite del
    /// query string del redirect.
    /// </param>
    IActionResult? TryRedirectToLogin(string? returnUrl = null);
}