using System.Security.Claims;
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
/// PageModel del formulario de edición de un Usuario. Espejo del
/// <see cref="SGV.Web.Pages.Personas.EditModel"/>: exige rol
/// <c>Administrador</c>, precarga los datos vía
/// <see cref="IUsuarioApiClient.GetByIdAsync"/> (incluida la Persona
/// vinculada para mostrarla como card preseleccionada), y persiste vía
/// <see cref="IUsuarioApiClient.UpdateAsync"/>. PRG re-redirige al propio
/// edit tras 200; 400 mapea <c>FieldErrors</c>; 409 muestra el campo
/// afectado. Usuario inexistente muestra estado recuperable.
/// <para>
/// Edit modifica <c>UserName</c>, <c>Email</c> y <c>Roles</c>; el selector
/// permite cambiar o quitar la Persona visible sin incorporarla al request
/// <see cref="ActualizarUsuarioRequest"/>. El cambio de password desde la UI
/// administrativa queda fuera del scope.
/// </para>
/// <para>
/// Defense-in-depth contra AutoCambioRol: el backend rechaza la
/// actualización cuando el id del target coincide con el del admin
/// autenticado (espejo de <c>AutoBloqueo</c> / <c>AutoEliminacion</c>).
/// Adicionalmente, este PageModel fuerza en el POST que <c>Input.Roles</c>
/// vuelva al valor real que el backend tiene hoy para ese usuario, lo
/// que blinda contra tampering del form aún si la defensa del backend
/// fuera removida o desfasada.
/// </para>
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> (sin <c>default</c>). <c>Unauthorized</c>
/// redirige vía <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class EditModel(
    IUsuarioApiClient usuarioApiClient,
    IPersonaApiClient personaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel, IUsuarioForm
{
    private string? _routeUserId;

    [BindProperty]
    public UsuarioInputModel Input { get; set; } = new();

    [BindProperty]
    public string? PersonaDisplay { get; set; }

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

    /// <summary>
    /// Persona vinculada al usuario, proyectada como DTO para que el
    /// partial renderice la card enriquecida. <c>null</c> cuando el usuario
    /// no tiene persona asignada, cuando el API devolvió 404, o cuando
    /// el fetch sufrió un fallo de transporte: en esos casos la UI
    /// cae al fallback <see cref="PersonaDisplay"/>.
    /// </summary>
    public PersonaDto? PersonaVinculada { get; private set; }

    /// <summary>
    /// Indica si el usuario solicitado no pudo cargarse (404 o error de
    /// transporte). En ese estado la vista muestra un mensaje
    /// recuperable y oculta el formulario.
    /// </summary>
    public bool IsRecoverable { get; private set; }

    /// <summary>
    /// Mensaje de estado (success/warning) que llega vía TempData tras un PRG.
    /// </summary>
    public string? StatusMessage => TempData["StatusMessage"] as string;

    public string StatusKind => TempData["StatusKind"] as string ?? "success";

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
    /// Identificador del admin actualmente autenticado (claim
    /// <see cref="ClaimTypes.NameIdentifier"/>). Se usa para blindar el
    /// formulario contra auto-cambio de rol, espejando el patrón vigente
    /// en <c>DetailsModel.EsAutoAccion</c>.
    /// </summary>
    public string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Helper que la vista usa para decidir si debe renderizar el form
    /// con los checkboxes de Roles deshabilitados y el alert explicativo
    /// de AutoCambioRol. Compare contra el id de la ruta, que es el id
    /// del usuario que se está editando.
    /// </summary>
    public bool EsAccionSobreSiMismo(string? targetUserId) =>
        !string.IsNullOrEmpty(CurrentUserId)
        && !string.IsNullOrEmpty(targetUserId)
        && string.Equals(CurrentUserId, targetUserId, StringComparison.Ordinal);

    /// <summary>
    /// Variante parameterless que el partial usa desde el contrato
    /// <see cref="IUsuarioForm"/>. Mide contra el id de la ruta
    /// capturado al inicio del request.
    /// </summary>
    bool IUsuarioForm.EsAccionSobreSiMismo => EsAccionSobreSiMismo(_routeUserId);

    /// <summary>
    /// GET handler. Carga el usuario por id y deriva la presentación de la
    /// Persona vinculada directamente del <see cref="UsuarioDto"/>. Adicionalmente,
    /// cuando el usuario tiene una PersonaId asignada y distinta de Guid.Empty,
    /// intenta enriquecer la card trayendo los datos personales completos vía
    /// <see cref="IPersonaApiClient.GetByIdAsync"/>. Un 404 o un fallo de
    /// transporte en ese paso NO bloquea el formulario: la card cae al fallback
    /// <see cref="PersonaDisplay"/> conservando la información mínima. Si el
    /// usuario no existe o la consulta principal falla,
    /// marca <see cref="IsRecoverable"/> y muestra un mensaje recuperable
    /// sin renderizar el formulario. Los parámetros <c>p</c>, <c>search</c>,
    /// <c>sort</c> y <c>returnStatus</c> se preservan para los enlaces
    /// de retorno al listado.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        string id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "returnStatus")] string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _routeUserId = id;
        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
        ReturnStatus = RouteValuesPreserver.NormalizeDeletedStatus(returnStatus) ?? string.Empty;

        try
        {
            var usuario = await usuarioApiClient.GetByIdAsync(id, cancellationToken);
            if (usuario is null)
            {
                IsRecoverable = true;
                ErrorMessage = "El usuario solicitado no está disponible.";
                // CodeQL [SM02379]: structured logging placeholder, not interpolated.
                logger.LogWarning("Usuario with Id {UsuarioId} was not found or is no longer available.", id);
                return Page();
            }

            Input.UserName = usuario.UserName ?? string.Empty;
            Input.Email = usuario.Email ?? string.Empty;
            Input.PersonaId = usuario.PersonaId;
            Input.Password = null; // El cambio de password queda fuera del scope.
            Input.Roles = usuario.Roles.ToArray();
            PersonaDisplay = FormatPersonaDisplay(usuario.Apellidos, usuario.Nombres);

            await TryLoadPersonaVinculadaAsync(usuario.PersonaId, cancellationToken).ConfigureAwait(false);

            return Page();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogError(ex, "Failed to load edit page for usuario {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar el usuario. Intentá nuevamente.";
            return Page();
        }
    }

    /// <summary>
    /// Enriquecimiento opcional de la card de Persona vinculada. 404 y
    /// fallos de transporte son no-bloqueantes: el partial cae al fallback
    /// <see cref="PersonaDisplay"/>. Un <c>Guid.Empty</c> en el id se trata
    /// como "sin persona asignada" sin tocar el API.
    /// </summary>
    private async Task TryLoadPersonaVinculadaAsync(
        Guid personaId,
        CancellationToken cancellationToken)
    {
        if (personaId == Guid.Empty)
        {
            return;
        }

        try
        {
            PersonaVinculada = await personaApiClient
                .GetByIdAsync(personaId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogWarning(
                ex,
                "Failed to enrich linked persona {PersonaId} for edit page; falling back to PersonaDisplay.",
                personaId);
            PersonaVinculada = null;
        }
    }

    /// <summary>
    /// POST handler. Valida ModelState, llama <c>PUT /api/v1/usuarios/{id}</c>
    /// atómico (UserName+Email+Roles), y mapea el resultado a feedback del
    /// usuario. Tras éxito, PRG a sí mismo con TempData. Tras fallo de
    /// validación/conflicto, re-renderiza el formulario con los mensajes de
    /// error preservando el input y el texto de la card seleccionado.
    /// <para>
    /// Defense-in-depth de AutoCambioRol: cuando el id de la ruta coincide
    /// con el del admin autenticado, este handler sobreescribe
    /// <c>Input.Roles</c> con los roles que el API devuelve
    /// <c>ahora mismo</c> para ese usuario, ignorando los checkboxes
    /// enviados en el form. Si la consulta de roles frescos falla (404 o
    /// transporte), se ABORTA la persistencia para no propagar un cambio
    /// de roles posiblemente manipulado: el form se re-renderiza con el
    /// input del usuario y un mensaje de error recuperable.
    /// </para>
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        string id,
        [FromQuery(Name = "p")] string? p = null,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "returnStatus")] string? returnStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _routeUserId = id;
        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
        ReturnStatus = RouteValuesPreserver.NormalizeDeletedStatus(returnStatus) ?? string.Empty;

        // Saneo defensivo: filtrar la lista bindeable contra el catálogo
        // fijo de roles. Roles no vigentes (e.g. defaults de Identity como
        // "User") no deben llegar al backend.
        Input.Roles = UsuarioInputModel.FilterByCatalog(Input.Roles);

        // PersonaId no forma parte de ActualizarUsuarioRequest. Quitar la
        // selección en Edit es válido y no debe activar la regla [Required]
        // compartida con Create.
        ModelState.Remove(UsuarioFormKeys.PersonaIdKey);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Defensa de AutoCambioRol web: si el target es el admin
        // autenticado, forzar los roles a los del backend AHORA. Esto
        // evita que el POST persista roles manipulados en los checkboxes
        // (que la UI sirve deshabilitados pero un POST artesanal podría
        // bypassear) y provee una segunda capa defensiva contra la
        // edición del propio rol.
        if (EsAccionSobreSiMismo(id))
        {
            try
            {
                var current = await usuarioApiClient
                    .GetByIdAsync(id, cancellationToken)
                    .ConfigureAwait(false);
                if (current is null)
                {
                    logger.LogWarning(
                        "Self-role defense aborted: backend returned no user for {UserId}.",
                        id);
                    ErrorMessage = "No se pudo validar tu rol actual. Intentá nuevamente.";
                    ModelState.AddModelError(string.Empty, ErrorMessage);
                    return Page();
                }

                Input.Roles = current.Roles.ToArray();
            }
            catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
            {
                logger.LogError(ex, "Self-role defense transport failure for {UserId}.", id);
                ErrorMessage = PageFeedback.TransportMessage;
                ModelState.AddModelError(string.Empty, ErrorMessage);
                return Page();
            }
        }

        var request = new ActualizarUsuarioRequest(
            Input.UserName.Trim(),
            Input.Email.Trim(),
            Input.Roles.ToArray());

        UsuarioCommandResult result;
        try
        {
            result = await usuarioApiClient.UpdateAsync(id, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Usuario update transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            var dto = result.Value;
            TempData["StatusMessage"] = $"El usuario \"{dto.UserName}\" se actualizó correctamente.";
            TempData["StatusKind"] = "success";
            // PRG re-redirige al propio edit para que el usuario pueda
            // continuar editando o volver al listado sin reenvío del form.
            return RedirectToPage("/Seguridad/Usuarios/Edit", new
            {
                id,
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
                return Page();
            }

            // Conflict 409 (UserNameDuplicado / EmailDuplicado) →
            // field-level error general. 400 con FieldErrors los maneja
            // UsuarioPostResultMapper.TryMap.
            if (result.Error.Categoria == ErrorCategoria.Conflict)
            {
                ModelState.AddModelError(string.Empty, result.Error.Message);
            }
            else if (!UsuarioPostResultMapper.TryMap(result, ModelState))
            {
                ErrorMessage = ErrorCategoryMapper.Map(result.Error.Categoria,
                    notFoundMessage: "El usuario solicitado no está disponible.",
                    conflictMessage: "Conflicto al persistir el usuario.");
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
        }

        return Page();
    }

    private static string FormatPersonaDisplay(string? apellidos, string? nombres)
    {
        var display = string.Join(", ", new[] { apellidos, nombres }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(display) ? "Persona vinculada" : display;
    }
}