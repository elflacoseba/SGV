using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Usuarios;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Seguridad.Usuarios;

/// <summary>
/// PageModel del formulario de creación de un Usuario. Espejo del
/// <see cref="SGV.Web.Pages.Personas.CreateModel"/>: exige rol
/// <c>Administrador</c>, valida el formulario y lo envía a la API vía
/// <see cref="IUsuarioApiClient.CreateAsync"/>. PRG al detalle del usuario
/// creado tras 201; 400 mapea <c>FieldErrors</c> a los controles
/// correspondientes; 409 muestra el campo afectado sin perder el resto
/// del formulario. La disponibilidad de Personas se consulta vía
/// <see cref="IPersonaApiClient.QueryAsync"/>; cuando no hay candidatas,
/// el submit queda bloqueado con un mensaje guía hacia
/// <c>/personas/crear</c>.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> (sin <c>default</c>). <c>Unauthorized</c>
/// redirige vía <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class CreateModel(
    IUsuarioApiClient usuarioApiClient,
    IPersonaApiClient personaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : PageModel, IUsuarioForm
{
    [BindProperty]
    public UsuarioInputModel Input { get; set; } = new();

    [BindProperty]
    public string? PersonaDisplay { get; set; }

    public int TotalCountSugerido { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

    /// <summary>
    /// Create no precarga persona: el usuario elige en el modal del form.
    /// Devuelve <c>null</c> para que el partial caiga al fallback
    /// <see cref="PersonaDisplay"/>.
    /// </summary>
    public PersonaDto? PersonaVinculada => null;

    /// <summary>
    /// Create nunca opera sobre el propio usuario del admin (el id
    /// siempre es un id nuevo asignado por el backend). El partial
    /// puede ignorar la rama de auto-cambio de rol.
    /// </summary>
    public bool EsAccionSobreSiMismo => false;

    [BindProperty]
    public string? ReturnPage { get; set; }

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    [BindProperty]
    public string? ReturnStatus { get; set; }

    public string ReturnToListUrl => UsuarioFormHelpers.BuildReturnToListUrl(
        Url, ReturnPage, ReturnSearch, ReturnSort, ReturnStatus);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler del formulario. Si el usuario no es Administrador,
    /// devuelve <c>Forbid()</c> que delega al cookie scheme (redirige a
    /// AccessDeniedPath, <c>/error/403</c>). Consulta el total de Personas
    /// activas sin usuario; cuando no hay candidatas, el formulario se
    /// renderiza con un banner y CTA a <c>/personas/crear</c>.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        string? p = null,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ReturnPage = p ?? string.Empty;
        ReturnSearch = search ?? string.Empty;
        ReturnSort = sort ?? string.Empty;
        ReturnStatus = RouteValuesPreserver.NormalizeDeletedStatus(status) ?? string.Empty;

        await LoadPersonaAvailabilityAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// POST handler del formulario. Valida ModelState, llama
    /// <c>POST /api/v1/usuarios</c> y mapea el resultado. Tras éxito, PRG
    /// al detalle del usuario creado con TempData. Tras 400, mapea
    /// <c>FieldErrors</c> con
    /// <see cref="UsuarioFormHelpers.ApplyFieldErrorsToModelState"/>; tras
    /// 409 muestra el campo afectado sin perder el resto del formulario.
    /// Fallos de transporte se traducen a error recuperable preservando
    /// el input del usuario.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        // Saneo defensivo: filtrar la lista bindeable contra el catálogo
        // fijo de roles. Roles no vigentes (e.g. defaults de Identity como
        // "User") no deben llegar al backend.
        Input.Roles = UsuarioInputModel.FilterByCatalog(Input.Roles);

        if (!ModelState.IsValid)
        {
            await LoadPersonaAvailabilityAsync(cancellationToken);
            return Page();
        }

        // Crear requiere PersonaId no nulo; ModelState.IsValid ya garantiza
        // [Required] a nivel de modelo, pero defensivamente revalidamos.
        if (Input.PersonaId is null)
        {
            ModelState.AddModelError(UsuarioFormKeys.PersonaIdKey, "Debe seleccionar una persona activa.");
            await LoadPersonaAvailabilityAsync(cancellationToken);
            return Page();
        }

        var request = new CrearUsuarioRequest(
            Input.PersonaId.Value,
            Input.UserName.Trim(),
            Input.Email.Trim(),
            Input.Password ?? string.Empty,
            Input.Roles.ToArray());

        UsuarioCommandResult result;
        try
        {
            result = await usuarioApiClient.CreateAsync(request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Transport-level failure (network down, timeout, malformed
            // body). Map to a recoverable error: keep user input, re-render
            // the page so the user can retry.
            logger.LogError(ex, "Usuario create transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            await LoadPersonaAvailabilityAsync(cancellationToken);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            var dto = result.Value;
            TempData["StatusMessage"] = $"El usuario \"{dto.UserName}\" se creó correctamente.";
            TempData["StatusKind"] = "success";
            // PRG al detalle del usuario creado con los filtros preservados.
            return RedirectToPage("/Seguridad/Usuarios/Details", new
            {
                id = dto.Id,
                p = ReturnPage,
                search = ReturnSearch,
                sort = ReturnSort,
                returnStatus = ReturnStatus
            });
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
                await LoadPersonaAvailabilityAsync(cancellationToken);
                return Page();
            }

            // Conflict 409 → field-level error general para que aparezca en
            // el ValidationSummary. 400 con FieldErrors los maneja
            // UsuarioPostResultMapper.TryMap.
            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                if (string.Equals(result.Error.Code, "PersonaYaTieneUsuario", StringComparison.Ordinal))
                {
                    ModelState.AddModelError(
                        UsuarioFormKeys.PersonaIdKey,
                        "Esa persona ya tiene un usuario activo.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, result.Error.Message);
                }
            }
            else if (!UsuarioPostResultMapper.TryMap(result, ModelState))
            {
                ErrorMessage = ErrorCategoryMapper.Map(result.Error.Categoria,
                    notFoundMessage: "La persona solicitada no está disponible.",
                    conflictMessage: "Conflicto al persistir el usuario.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadPersonaAvailabilityAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Consulta únicamente el total de Personas activas sin usuario para
    /// decidir si el formulario debe mostrar el banner con CTA. La búsqueda
    /// interactiva del modal usa páginas de 25 filas desde JavaScript.
    /// </summary>
    private async Task LoadPersonaAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await personaApiClient.QueryAsync(
                new PersonaListQuery(
                    Page: 1,
                    PageSize: 1,
                    Search: null,
                    Sort: null,
                    Segmento: PersonaSegmentoListado.Activas,
                    SoloSinUsuario: true),
                cancellationToken);
            TotalCountSugerido = result.TotalCount;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to query available personas for usuario create page.");
            TotalCountSugerido = 0;
            if (string.IsNullOrWhiteSpace(ErrorMessage))
            {
                ErrorMessage = "No se pudo consultar las personas disponibles. Intentá nuevamente.";
            }
        }
    }
}