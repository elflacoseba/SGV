using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;

namespace SGV.Web.Pages.Auth;

public sealed class ForgotPasswordModel(
    IAuthApiClient authApiClient,
    ILogger<ForgotPasswordModel> logger) : PageModel
{
    private const string GenericConfirmation =
        "Si el email existe, recibirás un enlace para restablecer tu contraseña.";
    private const string RateLimitMessage =
        "Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo.";
    private const string TransportMessage =
        "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar.";
    private const string TimeoutMessage =
        "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var outcome = await authApiClient.ForgotPasswordAsync(
                new ForgotPasswordRequest(Input.Email),
                cancellationToken);

            if (outcome == PasswordResetOutcome.RateLimited)
            {
                ModelState.AddModelError(string.Empty, RateLimitMessage);
                return Page();
            }

            StatusMessage = GenericConfirmation;
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(exception, "Password recovery request was rate limited by the API.");
            ModelState.AddModelError(string.Empty, RateLimitMessage);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Password recovery API request failed.");
            ModelState.AddModelError(string.Empty, TransportMessage);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Password recovery API request timed out.");
            ModelState.AddModelError(string.Empty, TimeoutMessage);
        }

        return Page();
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
        public string Email { get; set; } = string.Empty;
    }
}
