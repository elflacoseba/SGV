using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;

namespace SGV.Web.Pages.Auth;

/// <summary>
/// POST-only logout. <see cref="AutoValidateAntiforgeryTokenAttribute"/>
/// cierra el vector C-2 (CSRF contra sign-out): un atacante no puede
/// desautenticar al usuario forzando un POST a <c>/auth/logout</c> desde
/// un sitio externo porque el token antiforgery no viaja en cross-site
/// POST con cookies <c>SameSite=Lax</c>.
/// </summary>
/// <remarks>
/// PR3 (change <c>implementa-refresh-tokens</c>): antes de limpiar las
/// cookies locales, el handler invoca <c>POST /api/v1/auth/logout</c> para
/// que la API revoque la familia de refresh tokens server-side. El
/// request se hace fail-open: si la API falla (network, 5xx, 401 de
/// sesión expirada), el handler sigue limpiando las cookies locales y
/// redirige — el usuario siempre puede salir (design §3.3 / spec
/// REQ-AUTH-COOKIES-2).
/// </remarks>
[AutoValidateAntiforgeryToken]
public sealed class LogoutModel(
    IAuthApiClient authApiClient,
    IRefreshTokenCookieAccessor refreshCookieAccessor,
    ILogger<LogoutModel> logger) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        var refreshToken = refreshCookieAccessor.Get();

        // Paso 1: revocar la familia de refresh tokens en la API. La API
        // usa la JWT del cookie auth para resolver el usuario; el refresh
        // token del body es opcional y enriquece la auditoría. Fail-open:
        // si la API falla, igual limpiamos cookies locales.
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await authApiClient.LogoutAsync(new LogoutRequest(refreshToken));
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Fallo de transporte al revocar la familia de refresh tokens en la API");
            }
            catch (TaskCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                // Cancellation del cliente: no hacer nada, el usuario se fue.
            }
        }

        // Paso 2: limpiar el ticket de cookie auth (sgv.auth).
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Paso 3: limpiar el refresh cookie (sgv.rt) usando la misma
        // política que en emisión (mismas Path/SameSite/Secure).
        refreshCookieAccessor.Delete();

        return LocalRedirect("/auth/sign-in");
    }
}
