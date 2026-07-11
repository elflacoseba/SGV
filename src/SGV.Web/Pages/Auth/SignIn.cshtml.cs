using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;

namespace SGV.Web.Pages.Auth;

public sealed class SignInModel(
    IAuthApiClient authApiClient,
    IOptions<JwtOptions> jwtOptions,
    ILogger<SignInModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new LoginRequest(Input.UserNameOrEmail, Input.Password);
        var response = await authApiClient.LoginAsync(request, cancellationToken);

        if (response is null)
        {
            ModelState.AddModelError(string.Empty, "Credenciales inválidas.");
            return Page();
        }

        ClaimsPrincipal principal;
        try
        {
            principal = AuthSessionFactory.CreatePrincipal(logger, jwtOptions.Value, request, response);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning(ex, "SGV.Api returned an access token that SGV.Web could not validate.");
            ModelState.AddModelError(string.Empty, "No se pudo validar la sesión de autenticación.");
            return Page();
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "SGV.Api returned a malformed access token that SGV.Web could not validate.");
            ModelState.AddModelError(string.Empty, "No se pudo validar la sesión de autenticación.");
            return Page();
        }

        var properties = AuthSessionFactory.CreateProperties(response);

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
