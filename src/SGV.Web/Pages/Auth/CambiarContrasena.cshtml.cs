using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;

namespace SGV.Web.Pages.Auth;

/// <summary>
/// Página para que un usuario ya autenticado cambie su propia contraseña.
/// No es un recovery flow: exige la contraseña actual y cierra la sesión
/// activa al terminar (la rotación del <c>SecurityStamp</c> en la API ya
/// invalida el JWT vigente; acá hacemos <c>SignOutAsync</c> explícito
/// para limpiar la cookie local).
/// </summary>
[Authorize]
[AutoValidateAntiforgeryToken]
public sealed class CambiarContrasenaModel(
    IAuthApiClient authApiClient,
    ILogger<CambiarContrasenaModel> logger) : PageModel
{
    private const string SuccessMessage = "Tu contraseña se cambió correctamente. Volvé a iniciar sesión.";
    private const string MismatchMessage = "Las contraseñas no coinciden.";
    private const string RateLimitMessage =
        "Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo.";
    private const string TransportMessage =
        "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar.";
    private const string TimeoutMessage =
        "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos.";
    private const string InvalidCurrentMessage = "La contraseña actual no es correcta.";
    private const string PolicyMessage =
        "La contraseña debe tener al menos 6 caracteres, una minúscula, una mayúscula, un número y un símbolo.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
        // Render del formulario.
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Primera barrera cliente: coincidencia de contraseñas. El validator
        // server también la chequea, pero queremos feedback inmediato en la UI.
        if (!string.Equals(Input.NewPassword, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(InputModel.ConfirmPassword)}",
                MismatchMessage);
        }

        // Primera barrera cliente: política de password. El validator
        // server también la chequea, pero queremos feedback inmediato.
        if (!string.IsNullOrEmpty(Input.NewPassword) && !MeetsPasswordPolicy(Input.NewPassword))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(InputModel.NewPassword)}",
                PolicyMessage);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var outcome = await authApiClient.ChangePasswordAsync(
                new ChangePasswordRequest(
                    Input.CurrentPassword,
                    Input.NewPassword,
                    Input.ConfirmPassword),
                cancellationToken);

            switch (outcome)
            {
                case ChangePasswordOutcome.Success:
                    // La API rotó el SecurityStamp, así que el JWT y la cookie
                    // vigente ya son inválidos. Igual cerramos la sesión local
                    // explícitamente para limpiar el ticket de cookie.
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    TempData["PasswordChangeMessage"] = SuccessMessage;
                    return LocalRedirect("/auth/sign-in");

                case ChangePasswordOutcome.InvalidCurrentPassword:
                    ModelState.AddModelError(
                        $"{nameof(Input)}.{nameof(InputModel.CurrentPassword)}",
                        InvalidCurrentMessage);
                    return Page();

                case ChangePasswordOutcome.ValidationError:
                    ModelState.AddModelError(
                        $"{nameof(Input)}.{nameof(InputModel.NewPassword)}",
                        "La nueva contraseña no cumple la política de seguridad.");
                    return Page();

                case ChangePasswordOutcome.RateLimited:
                    ModelState.AddModelError(string.Empty, RateLimitMessage);
                    return Page();
            }
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            // La cookie venció mientras el usuario escribía el formulario:
            // lo mandamos a sign-in sin re-renderizar el form (no tiene
            // sentido pedirle que reescriba las contraseñas si la sesión
            // ya venció).
            return LocalRedirect("/auth/sign-in");
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(exception, "Change password request was rate limited by the API.");
            ModelState.AddModelError(string.Empty, RateLimitMessage);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Change password API request failed.");
            ModelState.AddModelError(string.Empty, TransportMessage);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Change password API request timed out.");
            ModelState.AddModelError(string.Empty, TimeoutMessage);
        }

        return Page();
    }

    /// <summary>
    /// Mirror cliente de <see cref="SGV.Contracts.Seguridad.PasswordPolicy"/>
    /// (la misma fuente única que consume <c>IdentityOptions.Password</c>
    /// en <c>SGV.Api/Program.cs</c> y los FluentValidation rules de
    /// <c>ChangePasswordRequestValidator</c>). Devuelve false si la
    /// política no se cumple para cortocircuitar el POST antes del
    /// round-trip a la API; la API re-valida con el FluentValidator y
    /// devuelve 400 <c>ValidationProblemDetails</c> si el cliente se saltea
    /// el check.
    /// </summary>
    private static bool MeetsPasswordPolicy(string password)
        => SGV.Contracts.Seguridad.PasswordPolicy.IsCompliant(password);

    public sealed class InputModel
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La confirmación de contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}