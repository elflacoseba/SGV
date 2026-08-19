using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Setup;

namespace SGV.Web.Pages.Auth;

/// <summary>
/// Pagina de inicio de sesión. <see cref="AutoValidateAntiforgeryTokenAttribute"/>
/// protege <see cref="OnPostAsync"/> contra CSRF (C-2 release-readiness):
/// un atacante no puede forzar al browser de la víctima a enviar un POST
/// de login desde un sitio externo con credenciales conocidas, porque
/// el token antiforgery vive en una cookie <c>SameSite=Lax</c> y el
/// formulario debe incluirlo en el body.
/// </summary>
[AutoValidateAntiforgeryToken]
public sealed class SignInModel(
    IAuthApiClient authApiClient,
    IAuthSessionFactory authSessionFactory,
    ISetupApiClient setupApiClient,
    ILogger<SignInModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Issue #195 / WU-5: si AspNetUsers está vacía, redirigir a
        // /auth/setup. El cliente `ISetupApiClient` aplica fail-open
        // con cache TTL 30s (design §2.3), por eso una caída de la
        // API NO rompe el acceso a producción: simplemente
        // renderizamos SignIn normalmente.
        var status = await setupApiClient.ObtenerEstadoAsync(cancellationToken).ConfigureAwait(false);
        if (status.RequiresSetup)
        {
            return RedirectToPage("/Auth/Setup");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new LoginRequest(Input.UserNameOrEmail, Input.Password);

        LoginResponse? response;
        try
        {
            response = await authApiClient.LoginAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Fallo de transporte al autenticar contra la API");
            ModelState.AddModelError(string.Empty,
                "No pudimos contactar al servicio de autenticación. Intentá nuevamente en unos minutos.");
            return Page();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timeout al autenticar contra la API");
            ModelState.AddModelError(string.Empty,
                "La autenticación tardó demasiado. Intentá nuevamente.");
            return Page();
        }

        if (response is null)
        {
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(response.AccessToken))
        {
            logger.LogWarning("SGV.Api returned an empty access token.");
            ModelState.AddModelError(string.Empty, "No se pudo validar la sesión de autenticación.");
            return Page();
        }

        ClaimsPrincipal principal;
        try
        {
            principal = authSessionFactory.CreatePrincipal(request, response);
        }
        // Cubre las familias de excepciones que JwtSecurityTokenHandler.ValidateToken
        // puede emitir en Microsoft.IdentityModel.Tokens 8.x para un access_token
        // inválido: SecurityTokenException (validation), ArgumentException (input
        // malformado, incluye SecurityTokenArgumentException y Base64Url decode
        // failures). Se excluye ArgumentNullException para que un null inesperado de
        // DI se propague como fail-fast en vez de confundirse con token inválido.
        // Ver AuthSessionFactoryTests para la taxonomía completa.
        catch (Exception ex) when (ex is not ArgumentNullException && (ex is SecurityTokenException or ArgumentException))
        {
            logger.LogWarning("SGV.Api returned an access token that SGV.Web could not validate. {ExceptionType}", ex.GetType().Name);
            ModelState.AddModelError(string.Empty, "No se pudo validar la sesión de autenticación.");
            return Page();
        }

        var properties = authSessionFactory.CreateProperties(response);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        return LocalRedirect("/");
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "El usuario o el correo electrónico son obligatorios.")]
        public string UserNameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
