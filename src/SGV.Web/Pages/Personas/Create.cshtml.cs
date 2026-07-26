using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
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
/// Issue #147 PR3: el catálogo de tipos de documento se carga en cada
/// <see cref="OnGetAsync"/> y <see cref="OnPostAsync"/> que retorna
/// <c>Page()</c> vía <c>LoadTiposDocumentoAsync</c> (espejo del patrón
/// <c>LoadCatalogsAsync</c> de Cargos). El binding de
/// <c>TipoDocumentoId</c> es directo desde el <c>&lt;select&gt;</c>; el
/// legacy <c>ParseTipoDocumentoIdBackCompat</c> se elimina.
/// </para>
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

    public IReadOnlyList<TipoDocumentoDto> TiposDocumento { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

    /// <summary>
    /// Issue #202: slot reservado para que módulos downstream que
    /// exijan <c>Legajo</c> activen la advertencia contextual en
    /// <c>_Form.cshtml</c>. Create no muestra la advertencia por
    /// defecto; el módulo que lo necesite lo setea a <c>true</c>.
    /// </summary>
    public bool ShowLegajoContextWarning => false;

    /// <summary>
    /// Issue #202 (H4): mensaje personalizado para la advertencia
    /// contextual. <c>null</c> deja al partial usar el texto por defecto.
    /// </summary>
    public string? LegajoContextWarningMessage => null;

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    public string ReturnToListUrl => PersonaFormHelpers.BuildReturnToListUrl(Url, ReturnPage, ReturnSearch, ReturnSort);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler del formulario. Carga el catálogo de tipos de documento
    /// para popular el <c>&lt;select&gt;</c> (issue #147 PR3). Si el usuario
    /// no es Administrador, devuelve <c>Forbid()</c> que delega al cookie
    /// scheme (redirige a AccessDeniedPath, <c>/error/403</c>).
    /// </summary>
    public async Task<IActionResult> OnGetAsync(string? p = null, string? search = null, string? sort = null, CancellationToken ct = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ReturnPage = p ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;

        await LoadTiposDocumentoAsync(ct);
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
            await LoadTiposDocumentoAsync(cancellationToken);
            return Page();
        }

        // Issue #147 PR3: binding directo desde el <select>. El legacy
        // ParseTipoDocumentoIdBackCompat se elimina porque el frontend ya no
        // envía el string TipoDocumento.
        // Issue #202: Legajo se normaliza a null cuando es whitespace-only,
        // para alinear con el dominio nullable y el wire string?. El resto
        // de los campos opcionales (Email / NumeroDocumento / Telefono)
        // siguen el mismo patrón.
        var tipoDocumentoId = Input.TipoDocumentoId;
        var legajoNormalizado = string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo.Trim();
        var request = new CrearPersonaRequest(
            legajoNormalizado,
            Input.Nombres.Trim(),
            Input.Apellidos.Trim(),
            string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            tipoDocumentoId,
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
            await LoadTiposDocumentoAsync(cancellationToken);
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
                await LoadTiposDocumentoAsync(cancellationToken);
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

        await LoadTiposDocumentoAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Carga el catálogo de tipos de documento vía
    /// <see cref="IPersonaApiClient.GetTiposDocumentoAsync"/>. Si la
    /// llamada falla (transport error, etc.), se loguea, se setea
    /// <see cref="ErrorMessage"/> con un mensaje recuperable y la lista
    /// queda vacía — el view aún renderiza el placeholder "Seleccionar
    /// tipo…" sin propagar la excepción.
    /// </summary>
    private async Task LoadTiposDocumentoAsync(CancellationToken cancellationToken)
    {
        try
        {
            TiposDocumento = await personaApiClient.GetTiposDocumentoAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tipos-documento catalog for persona create page.");
            ErrorMessage = "No se pudo cargar el catálogo de tipos de documento. Intentá nuevamente.";
        }
    }
}
