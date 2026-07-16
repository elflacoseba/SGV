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
/// PageModel del formulario de edición de un Usuario. Espejo del
/// <see cref="SGV.Web.Pages.Personas.EditModel"/>: exige rol
/// <c>Administrador</c>, precarga los datos vía
/// <see cref="IUsuarioApiClient.GetByIdAsync"/> (incluida la Persona
/// vinculada para mostrarla como read-only), y persiste vía
/// <see cref="IUsuarioApiClient.UpdateAsync"/>. PRG re-redirige al propio
/// edit tras 200; 400 mapea <c>FieldErrors</c>; 409 muestra el campo
/// afectado. Usuario inexistente muestra estado recuperable.
/// <para>
/// Edit sólo modifica <c>UserName</c>, <c>Email</c> y <c>Roles</c>; la
/// <c>Persona</c> es inmutable (fuera del scope del change — ver
/// <c>specs/usuario-web-crear-editar/spec.md</c> §Out of scope). El cambio
/// de password desde la UI administrativa también queda fuera del scope.
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
    IPersonaOptionsProvider personaOptionsProvider,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel, IUsuarioForm
{
    [BindProperty]
    public UsuarioInputModel Input { get; set; } = new();

    public IReadOnlyList<PersonaDto> PersonaOptions { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

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
    /// GET handler. Carga el usuario por id y, en paralelo, el catálogo
    /// de Personas activas para resolver la descripción read-only de la
    /// Persona vinculada. Si el usuario no existe o la consulta falla,
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

        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
        ReturnStatus = RouteValuesPreserver.NormalizeDeletedStatus(returnStatus) ?? string.Empty;

        try
        {
            // Cargar usuario + catálogo de Personas en paralelo.
            var usuarioTask = usuarioApiClient.GetByIdAsync(id, cancellationToken);
            var personasTask = personaOptionsProvider.GetActivasAsync(cancellationToken);

            await Task.WhenAll(usuarioTask, personasTask);

            var usuario = await usuarioTask;
            if (usuario is null)
            {
                IsRecoverable = true;
                ErrorMessage = "El usuario solicitado no está disponible.";
                // CodeQL [SM02379]: structured logging placeholder, not interpolated.
                logger.LogWarning("Usuario with Id {UsuarioId} was not found or is no longer available.", id);
                PersonaOptions = [];
                return Page();
            }

            PersonaOptions = await personasTask;

            Input.UserName = usuario.UserName ?? string.Empty;
            Input.Email = usuario.Email ?? string.Empty;
            Input.PersonaId = usuario.PersonaId;
            Input.Password = null; // El cambio de password queda fuera del scope.
            Input.Roles = usuario.Roles.ToArray();

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
    /// POST handler. Valida ModelState, llama <c>PUT /api/v1/usuarios/{id}</c>
    /// atómico (UserName+Email+Roles), y mapea el resultado a feedback del
    /// usuario. Tras éxito, PRG a sí mismo con TempData. Tras fallo de
    /// validación/conflicto, re-renderiza el formulario con los mensajes de
    /// error preservando el input. Recarga el catálogo de Personas en
    /// cualquier rama que re-renderice, para mantener el banner read-only
    /// sincronizado con la realidad del backend.
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

        ReturnPage = p ?? string.Empty;
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();
        ReturnStatus = RouteValuesPreserver.NormalizeDeletedStatus(returnStatus) ?? string.Empty;

        // Saneo defensivo: filtrar la lista bindeable contra el catálogo
        // fijo de roles. Roles no vigentes (e.g. defaults de Identity como
        // "User") no deben llegar al backend.
        Input.Roles = UsuarioInputModel.FilterByCatalog(Input.Roles);

        if (!ModelState.IsValid)
        {
            await LoadPersonasAsync(cancellationToken);
            return Page();
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
            await LoadPersonasAsync(cancellationToken);
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
                await LoadPersonasAsync(cancellationToken);
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

        await LoadPersonasAsync(cancellationToken);
        return Page();
    }

    /// <summary>
    /// Carga el catálogo de Personas activas para mantener la sección
    /// read-only (que muestra la Persona vinculada) sincronizada con la
    /// realidad del backend. Idempotente; re-cargar el catálogo tras un
    /// fallo recuperable deja la lista vacía para no romper el render.
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
            logger.LogError(ex, "Failed to load personas activas for usuario edit page.");
            PersonaOptions = [];
            // No pisar ErrorMessage si ya estaba seteado por la rama
            // principal (transporte / recuperable). Sólo lo seteamos si
            // todavía no hay un mensaje más específico.
            if (string.IsNullOrWhiteSpace(ErrorMessage))
            {
                ErrorMessage = "No se pudo cargar el catálogo de personas. Intentá nuevamente.";
            }
        }
    }
}