using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Setup;
using SGV.Web.Integration.Setup;

namespace SGV.Web.Pages.Auth;

/// <summary>
/// PageModel de la pantalla de setup inicial (issue #195 / WU-4). Se
/// muestra únicamente cuando <see cref="ISetupApiClient.ObtenerEstadoAsync"/>
/// devuelve <c>RequiresSetup=true</c>; en ese caso
/// <c>SignIn.OnGetAsync</c> redirige a <c>/auth/setup</c>.
/// </summary>
/// <remarks>
/// <para>
/// El <see cref="OnPostAsync"/> implementa el patrón PRG (Post-Redirect-Get)
/// vía <c>RedirectToPage("/auth/sign-in")</c> + TempData. Los fallos
/// de transporte (HttpRequestException / TaskCanceledException)
/// producen un mensaje recuperable sin reintento ciego.
/// </para>
/// <para>
/// <see cref="AutoValidateAntiforgeryTokenAttribute"/> cierra el vector
/// C-2 (CSRF contra setup): la vista emite <c>@Html.AntiForgeryToken()</c>
/// y el atributo rechaza POSTs cross-site que intenten crear el primer
/// <c>Administrador</c> cuando la base está vacía.
/// </para>
/// </remarks>
[AutoValidateAntiforgeryToken]
public sealed class SetupModel(
    ISetupApiClient setupApiClient,
    ILogger<SetupModel> logger) : PageModel
{
    private const string TransportMessage =
        "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar.";
    private const string TimeoutMessage =
        "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos.";
    private const string SuccessMessage =
        "Configuración inicial completada. Iniciá sesión con tus credenciales.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Lista de tipos de documento para el dropdown.</summary>
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> TiposDocumentoOptions { get; private set; }
        = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var status = await setupApiClient.ObtenerEstadoAsync(cancellationToken).ConfigureAwait(false);
        if (!status.RequiresSetup)
        {
            // Spec REQ-SETUP-005 escenario "Setup no disponible": si
            // AspNetUsers ya tiene usuarios, /auth/setup NO debe
            // renderizar.
            return RedirectToPage("/Auth/SignIn");
        }

        // Usamos LoadTiposDocumentoAsync (con try-catch) en vez de llamar
        // directamente a GetTiposDocumentoAsync para evitar un 500 si el
        // catálogo falla después de que el status se cacheó como true.
        await LoadTiposDocumentoAsync(cancellationToken).ConfigureAwait(false);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadTiposDocumentoAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        var request = new SetupRequest(
            Nombres: Input.Nombres,
            Apellidos: Input.Apellidos,
            Legajo: string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo,
            Email: Input.Email,
            UserName: Input.UserName,
            Password: Input.Password,
            TipoDocumentoId: string.IsNullOrWhiteSpace(Input.TipoDocumentoId) || !Guid.TryParse(Input.TipoDocumentoId, out var tipoDocId)
                ? null
                : tipoDocId,
            NumeroDocumento: string.IsNullOrWhiteSpace(Input.NumeroDocumento) ? null : Input.NumeroDocumento,
            Telefono: string.IsNullOrWhiteSpace(Input.Telefono) ? null : Input.Telefono);

        SetupHttpResult result;
        try
        {
            result = await setupApiClient.CrearAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Fallo de transporte al crear el administrador inicial");
            ModelState.AddModelError(string.Empty, TransportMessage);
            await LoadTiposDocumentoAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timeout al crear el administrador inicial");
            ModelState.AddModelError(string.Empty, TimeoutMessage);
            await LoadTiposDocumentoAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        if (result.IsSuccess)
        {
            TempData["SetupSuccess"] = SuccessMessage;
            return RedirectToPage("/Auth/SignIn");
        }

        ApplyFailureToModelState(result);
        await LoadTiposDocumentoAsync(cancellationToken).ConfigureAwait(false);
        return Page();
    }

    private void ApplyFailureToModelState(SetupHttpResult result)
    {
        if (result.FieldErrors is { Count: > 0 })
        {
            foreach (var entry in result.FieldErrors)
            {
                var key = NormaliseFieldKey(entry.Key);
                foreach (var message in entry.Value)
                {
                    ModelState.AddModelError(key, message);
                }
            }

            // Mantener el resumen de errores a nivel form cuando
            // la API devolvió fieldErrors pero también un mensaje
            // global de error.
            if (!string.IsNullOrWhiteSpace(result.Error?.Message))
            {
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
            return;
        }

        ModelState.AddModelError(string.Empty, result.Error?.Message ?? "No se pudo crear el administrador.");
    }

    /// <summary>
    /// Convierte la clave camelCase que devuelve la API
    /// (e.g. "nombres", "password") al path con prefijo
    /// "Input." que espera la Razor Page para que
    /// <c>asp-validation-for</c> lo muestre junto al campo.
    /// </summary>
    private static string NormaliseFieldKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        // El setup tiene un solo nivel de binding: cualquier clave
        // de la API se proyecta al InputModel.
        return "Input." + char.ToUpperInvariant(apiKey[0]) + apiKey[1..];
    }

    private async Task LoadTiposDocumentoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tipos = await setupApiClient.GetTiposDocumentoAsync(cancellationToken).ConfigureAwait(false);
            TiposDocumentoOptions = tipos
                .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(t.Codigo, t.Id.ToString()))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Si la API del catálogo cae durante un render
            // post-failure, dejamos el dropdown vacío: el usuario
            // verá el form y el error principal, no otro stack
            // trace. La validación cliente de Required no
            // impedirá el submit si TipoDocumentoId es opcional.
            logger.LogWarning(ex, "No se pudo recargar el catálogo de TipoDocumento durante el render post-failure");
            TiposDocumentoOptions = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
        }
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Los nombres son obligatorios.")]
        [StringLength(100, ErrorMessage = "Los nombres no pueden superar los 100 caracteres.")]
        public string Nombres { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los apellidos son obligatorios.")]
        [StringLength(100, ErrorMessage = "Los apellidos no pueden superar los 100 caracteres.")]
        public string Apellidos { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "El legajo no puede superar los 50 caracteres.")]
        public string? Legajo { get; set; }

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(50, ErrorMessage = "El nombre de usuario no puede superar los 50 caracteres.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(128, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 128 caracteres.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? TipoDocumentoId { get; set; }

        [StringLength(20, ErrorMessage = "El número de documento no puede superar los 20 caracteres.")]
        public string? NumeroDocumento { get; set; }

        [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
        [Phone(ErrorMessage = "El formato del teléfono no es válido.")]
        public string? Telefono { get; set; }
    }
}
