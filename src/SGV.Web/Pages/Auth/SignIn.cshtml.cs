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

        if (string.IsNullOrWhiteSpace(response.AccessToken))
        {
            logger.LogWarning("SGV.Api returned an empty access token.");
            ModelState.AddModelError(string.Empty, "No se pudo validar la sesión de autenticación.");
            return Page();
        }

        ClaimsPrincipal principal;
        try
        {
            principal = AuthSessionFactory.CreatePrincipal(logger, jwtOptions.Value, request, response);
        }
        // Cubre las tres familias de excepciones que JwtSecurityTokenHandler.ValidateToken
        // puede emitir en Microsoft.IdentityModel.Tokens 8.x cuando recibe un access_token
        // que no puede validar:
        //   - SecurityTokenException y subclases (validation: firma, issuer, audience, expiración).
        //   - SecurityTokenArgumentException y subclases (input malformado: "JWT must have
        //     three segments", carácter inválido, etc.). Esta rama es independiente de
        //     SecurityTokenException en 8.x porque Microsoft movió las excepciones de
        //     argumento bajo ArgumentException.
        //   - ArgumentException plano (encoding errors: Base64Url decode failures sobre
        //     segmentos con bytes no-base64). Esta rama no es SecurityTokenArgumentException
        //     porque el decoder falla antes de que el handler clasifique el error.
        // Las dos fuentes legítimas de ArgumentException dentro de AuthSessionFactory.CreatePrincipal
        // son ArgumentNullException.ThrowIfNull(logger/jwtOptions) — que en runtime normal
        // no se disparan porque ambos vienen de DI con ValidateOnStart — y el JWT validator.
        // Aceptamos el riesgo de capturar ArgumentException aquí a cambio de no devolver 500
        // ante un access_token corrupto de la API (proxy, baseUrl incorrecto, respuesta
        // no-JSON de un balanceador).
        catch (Exception ex) when (ex is SecurityTokenException or SecurityTokenArgumentException or ArgumentException)
        {
            logger.LogWarning(ex, "SGV.Api returned an access token that SGV.Web could not validate.");
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
