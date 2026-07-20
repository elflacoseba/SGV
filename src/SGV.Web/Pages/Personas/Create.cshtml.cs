using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Personas;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Personas;

/// <summary>
/// PageModel del formulario de creación de una Persona. Espejo del
/// <see cref="SGV.Web.Pages.Organizacion.Cargos.CreateModel"/>: exige rol
/// <c>Administrador</c>, valida el formulario y lo envía a la API vía
/// <see cref="IPersonaApiClient.CreateAsync"/>. PRG al detalle de la
/// persona creada tras 201; 400 mapea <c>FieldErrors</c> a los controles
/// correspondientes; 409 muestra el campo afectado sin perder el resto
/// del formulario.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> (sin <c>default</c>). <c>Unauthorized</c>
/// redirige vía <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class CreateModel(
    IPersonaApiClient personaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : PageModel, IPersonaForm
{
    [BindProperty]
    public PersonaInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    public string ReturnToListUrl => PersonaFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler del formulario. Si el usuario no es Administrador, devuelve
    /// <c>Forbid()</c> que delega al cookie scheme (redirige a AccessDeniedPath,
    /// <c>/error/403</c>).
    /// </summary>
    public IActionResult OnGet(string? p = null, string? search = null, string? sort = null)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ReturnPage = p ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

        return Page();
    }

    /// <summary>
    /// POST handler del formulario. Valida ModelState, llama
    /// <c>POST /api/v1/personas</c> y mapea el resultado. Tras éxito, PRG al
    /// detalle de la persona creada con TempData. Tras 400, mapea
    /// <c>FieldErrors</c> con <see cref="PersonaFormHelpers.ApplyFieldErrorsToModelState"/>;
    /// tras 409 muestra el campo afectado. Fallos de transporte se traducen
    /// a error recuperable preservando el input del usuario.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new CrearPersonaRequest(
            Input.Legajo.Trim(),
            Input.Nombres.Trim(),
            Input.Apellidos.Trim(),
            string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            // Issue #147: TipoDocumentoId reemplaza al string TipoDocumento.
            // PR3 agregará el <select> con catálogo cargado vía
            // GetTiposDocumentoAsync; por ahora el binding del <select> legacy
            // (string TipoDocumento) sigue siendo el origen.
            PersonaFormHelpers.ParseTipoDocumentoIdBackCompat(Input.TipoDocumentoId, Input.TipoDocumento),
            string.IsNullOrWhiteSpace(Input.NumeroDocumento) ? null : Input.NumeroDocumento.Trim(),
            string.IsNullOrWhiteSpace(Input.Telefono) ? null : Input.Telefono.Trim());

        PersonaCommandResult result;
        try
        {
            result = await personaApiClient.CreateAsync(request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Transport-level failure (network down, timeout, malformed body).
            // Map to a recoverable error: keep user input, re-render the page
            // so the user can retry. We do not propagate as 500 because the
            // user action is recoverable.
            logger.LogError(ex, "Persona create transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"La persona \"{result.Value.Apellidos}, {result.Value.Nombres}\" se creó correctamente.";
            TempData["StatusKind"] = "success";
            return RedirectToPage("/Personas/Details", new { id = result.Value.Id, p = ReturnPage, search = ReturnSearch, sort = ReturnSort });
        }

        if (result.Error is not null)
        {
            // Issue #125 / Slice 3: switch exhaustivo sobre ErrorCategoria.
            if (result.Error.Categoria == ErrorCategoria.Unauthorized)
            {
                var redirect = authRedirector.TryRedirectToLogin(Request.Path);
                if (redirect is not null)
                {
                    return redirect;
                }

                ErrorMessage = PageFeedback.UnauthorizedMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                return Page();
            }

            // Conflict 409 (Legajo/Email/NumeroDocumento duplicado) →
            // field-level error general para que el usuario vea el mensaje
            // en el ValidationSummary. Los 400 con FieldErrors son manejados
            // por PersonaPostResultMapper.TryMap.
            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
            else if (!PersonaPostResultMapper.TryMap(result, ModelState))
            {
                ErrorMessage = ErrorCategoryMapper.Map(result.Error.Categoria,
                    notFoundMessage: "La persona solicitada no está disponible.",
                    conflictMessage: "Conflicto al persistir la persona.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        return Page();
    }
}