using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Common;
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
/// del formulario. El dropdown de Personas activas se carga vía
/// <see cref="IPersonaOptionsProvider"/>; cuando el catálogo está vacío,
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
    IPersonaOptionsProvider personaOptionsProvider,
    IAuthSessionRedirector authRedirector,
    ILogger<CreateModel> logger) : PageModel, IUsuarioForm
{
    [BindProperty]
    public UsuarioInputModel Input { get; set; } = new();

    public IReadOnlyList<PersonaDto> PersonaOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => false;

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
    /// AccessDeniedPath, <c>/error/403</c>). Si el catálogo de Personas
    /// activas está vacío, el formulario se renderiza igual pero el submit
    /// queda bloqueado por la ausencia de opciones en el dropdown y un
    /// banner informativo con link a <c>/personas/crear</c>.
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

        await LoadPersonasAsync(cancellationToken);
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
            await LoadPersonasAsync(cancellationToken);
            return Page();
        }

        // Crear requiere PersonaId no nulo; ModelState.IsValid ya garantiza
        // [Required] a nivel de modelo, pero defensivamente revalidamos.
        if (Input.PersonaId is null)
        {
            ModelState.AddModelError(UsuarioFormKeys.PersonaIdKey, "Debe seleccionar una persona activa.");
            await LoadPersonasAsync(cancellationToken);
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
            await LoadPersonasAsync(cancellationToken);
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
                await LoadPersonasAsync(cancellationToken);
                return Page();
            }

            // Conflict 409 → field-level error general para que aparezca en
            // el ValidationSummary. 400 con FieldErrors los maneja
            // UsuarioPostResultMapper.TryMap.
            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
            else if (!UsuarioPostResultMapper.TryMap(result, ModelState))
            {
                ErrorMessage = ErrorCategoryMapper.Map(result.Error.Categoria,
                    notFoundMessage: "La persona solicitada no está disponible.",
                    conflictMessage: "Conflicto al persistir el usuario.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        await LoadPersonasAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Carga el catálogo de Personas activas vía
    /// <see cref="IPersonaOptionsProvider"/>. Cualquier excepción
    /// (transporte o payload) se traduce a un estado recuperable: la
    /// lista queda vacía, el dropdown muestra el placeholder y se setea
    /// <see cref="ErrorMessage"/> para que el form siga visible y el
    /// usuario pueda reintentar.
    /// </summary>
    private async Task LoadPersonasAsync(CancellationToken cancellationToken)
    {
        try
        {
            PersonaOptions = await personaOptionsProvider.GetActivasAsync(cancellationToken);
            ErrorMessage = null;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load personas activas for usuario create page.");
            PersonaOptions = [];
            ErrorMessage = "No se pudo cargar el catálogo de personas. Intentá nuevamente.";
        }
    }
}