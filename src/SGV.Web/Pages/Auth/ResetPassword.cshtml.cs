using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;

namespace SGV.Web.Pages.Auth;

public sealed class ResetPasswordModel(
    IAuthApiClient authApiClient,
    ILogger<ResetPasswordModel> logger) : PageModel
{
    private const string InvalidTokenMessage = "El link es inválido o expiró. Solicitá uno nuevo.";
    private const string IncompleteLinkMessage = "El enlace de recuperación es inválido o está incompleto.";
    private const string MismatchMessage = "Las contraseñas no coinciden.";
    private const string RateLimitMessage =
        "Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo.";
    private const string TransportMessage =
        "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar.";
    private const string TimeoutMessage =
        "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos.";
    private const string SuccessMessage = "Tu contraseña se restableció correctamente.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        UserId = Decode(UserId);
        Token = Decode(Token);

        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Token))
        {
            ModelState.AddModelError(string.Empty, IncompleteLinkMessage);
            return Page();
        }

        var outcome = await authApiClient.ValidateResetTokenAsync(
            new ValidateResetTokenRequest(UserId, Token),
            cancellationToken);

        if (outcome == PasswordResetOutcome.InvalidToken)
        {
            ModelState.AddModelError(string.Empty, InvalidTokenMessage);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Token))
        {
            ModelState.AddModelError(string.Empty, IncompleteLinkMessage);
        }

        if (!string.Equals(Input.NewPassword, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(InputModel.ConfirmPassword)}", MismatchMessage);
        }

        // Solo validar política si hay contraseña (el [Required] ya rechaza vacíos)
        if (!string.IsNullOrEmpty(Input.NewPassword) && !MeetsPasswordPolicy(Input.NewPassword))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(InputModel.NewPassword)}",
                "La contraseña debe tener al menos 6 caracteres, una minúscula, una mayúscula, un número y un símbolo.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var outcome = await authApiClient.ResetPasswordAsync(
                new ResetPasswordRequest(UserId!, Token!, Input.NewPassword),
                cancellationToken);

            if (outcome == PasswordResetOutcome.InvalidToken)
            {
                ModelState.AddModelError(string.Empty, InvalidTokenMessage);
                return Page();
            }

            if (outcome == PasswordResetOutcome.RateLimited)
            {
                ModelState.AddModelError(string.Empty, RateLimitMessage);
                return Page();
            }

            TempData["PasswordResetMessage"] = SuccessMessage;
            return LocalRedirect("/auth/sign-in");
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            logger.LogInformation(exception, "Password reset token was rejected by the API.");
            ModelState.AddModelError(string.Empty, InvalidTokenMessage);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(exception, "Password reset request was rate limited by the API.");
            ModelState.AddModelError(string.Empty, RateLimitMessage);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Password reset API request failed.");
            ModelState.AddModelError(string.Empty, TransportMessage);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Password reset API request timed out.");
            ModelState.AddModelError(string.Empty, TimeoutMessage);
        }

        return Page();
    }

    private static string? Decode(string? value)
        => value is null ? null : Uri.UnescapeDataString(value);

    /// <summary>
    /// Mirror cliente de <see cref="SGV.Contracts.Seguridad.PasswordPolicy"/>
    /// (la misma fuente única que consume <c>IdentityOptions.Password</c>
    /// en <c>SGV.Api/Program.cs</c> y <c>ResetPasswordRequestValidator</c>).
    /// Devuelve false si la política no se cumple para cortocircuitar el
    /// POST antes del round-trip a la API.
    /// </summary>
    private static bool MeetsPasswordPolicy(string password)
        => SGV.Contracts.Seguridad.PasswordPolicy.IsCompliant(password);

    public sealed class InputModel
    {
        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La confirmación de contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
