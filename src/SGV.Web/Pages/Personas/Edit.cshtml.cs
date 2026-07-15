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
/// PageModel del formulario de edición de una Persona. Espejo del
/// <see cref="SGV.Web.Pages.Organizacion.Cargos.EditModel"/>: exige rol
/// <c>Administrador</c>, precarga los datos vía
/// <see cref="IPersonaApiClient.GetByIdAsync"/>, y persiste vía
/// <see cref="IPersonaApiClient.UpdateAsync"/>. PRG re-redirige al propio
/// edit tras 200; 400 mapea <c>FieldErrors</c>; 409 muestra el campo
/// afectado. Persona inexistente muestra estado recuperable.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> (sin <c>default</c>). <c>Unauthorized</c>
/// redirige vía <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class EditModel(
    IPersonaApiClient personaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<EditModel> logger) : PageModel, IPersonaForm
{
    [BindProperty]
    public PersonaInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsEdit => true;

    /// <summary>
    /// Indica si la persona solicitada no pudo cargarse (404 o error de
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
    public int ReturnPage { get; set; } = 1;

    [BindProperty]
    public string? ReturnSearch { get; set; }

    [BindProperty]
    public string? ReturnSort { get; set; }

    public string ReturnToListUrl => PersonaFormHelpers.BuildReturnToListUrl(
        Url,
        ReturnPage.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ReturnSearch,
        ReturnSort);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// GET handler. Carga la persona por id. Si no existe o la consulta
    /// falla, marca <see cref="IsRecoverable"/> y muestra un mensaje
    /// recuperable sin renderizar el formulario. Los parámetros
    /// <c>p</c>, <c>search</c> y <c>sort</c> se preservan para los
    /// enlaces de retorno al listado.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int page = 1,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ReturnPage = Math.Max(1, page);
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

        try
        {
            var persona = await personaApiClient.GetByIdAsync(id, cancellationToken);
            if (persona is null)
            {
                IsRecoverable = true;
                ErrorMessage = "La persona solicitada no está disponible.";
                logger.LogWarning("Persona with Id {PersonaId} was not found or is no longer available.", id);
                return Page();
            }

            Input.Legajo = persona.Legajo ?? string.Empty;
            Input.Nombres = persona.Nombres;
            Input.Apellidos = persona.Apellidos;
            Input.Email = persona.Email;
            Input.TipoDocumento = persona.TipoDocumento;
            Input.NumeroDocumento = persona.NumeroDocumento;
            Input.Telefono = persona.Telefono;

            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load edit page for persona {Id}.", id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar la persona. Intentá nuevamente.";
            return Page();
        }
    }

    /// <summary>
    /// POST handler. Valida ModelState, llama <c>PUT /api/v1/personas/{id}</c>,
    /// y mapea el resultado a feedback del usuario. Tras éxito, PRG a sí
    /// mismo con TempData. Tras fallo de validación/conflicto, re-renderiza
    /// el formulario con los mensajes de error preservando el input.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        Guid id,
        [FromQuery(Name = "p")] int page = 1,
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        ReturnPage = Math.Max(1, page);
        ReturnSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ReturnSort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new ActualizarPersonaRequest(
            Input.Legajo.Trim(),
            Input.Nombres.Trim(),
            Input.Apellidos.Trim(),
            string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim(),
            string.IsNullOrWhiteSpace(Input.TipoDocumento) ? null : Input.TipoDocumento.Trim(),
            string.IsNullOrWhiteSpace(Input.NumeroDocumento) ? null : Input.NumeroDocumento.Trim(),
            string.IsNullOrWhiteSpace(Input.Telefono) ? null : Input.Telefono.Trim());

        PersonaCommandResult result;
        try
        {
            result = await personaApiClient.UpdateAsync(id, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Transport-level failure (network down, timeout, malformed body).
            // Map to a recoverable error: keep user input, re-render the page
            // so the user can retry.
            logger.LogError(ex, "Persona update transport failure.");
            ErrorMessage = PageFeedback.TransportMessage;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            return Page();
        }

        if (result.IsSuccess && result.Value is not null)
        {
            TempData["StatusMessage"] = $"La persona \"{result.Value.Apellidos}, {result.Value.Nombres}\" se actualizó correctamente.";
            TempData["StatusKind"] = "success";
            // PRG re-redirige al propio edit para que el usuario pueda
            // continuar editando o volver al listado sin reenvío del form.
            return RedirectToPage("/Personas/Edit", new { id, p = ReturnPage, search = ReturnSearch, sort = ReturnSort });
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

            // Conflict 409 → field-level error general. 400 con FieldErrors
            // es manejado por PersonaPostResultMapper.TryMap.
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