using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Habilidades;
using SGV.Web.Integration.Personas;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Personas;

/// <summary>
/// PageModel de la gestión administrativa de habilidades asociadas a una
/// persona. Este slice expone la carga inicial (Slice 3a) y los handlers
/// POST con PRG + TempData feedback (Slice 3b). El acceso está restringido
/// al rol <see cref="RolesSgv.Administrador"/> y las mutaciones sobre
/// personas inactivas se rechazan en el handler antes de invocar al
/// cliente HTTP.
/// </summary>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class PersonaHabilidadesModel(
    IPersonaApiClient personaApiClient,
    IHabilidadApiClient habilidadApiClient,
    ILogger<PersonaHabilidadesModel> logger) : PageModel
{
    /// <summary>Datos que consume la vista de habilidades.</summary>
    public PersonaHabilidadesViewModel ViewModel { get; private set; } = new();

    /// <summary>Indica si el usuario actual tiene el rol administrador.</summary>
    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>Cliente de catálogo de habilidades (expuesto para los tests de PageModel).</summary>
    internal IHabilidadApiClient HabilidadApiClient => habilidadApiClient;

    /// <summary>Mensaje de feedback entregado vía TempData tras un PRG.</summary>
    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    /// <summary>Tipo de feedback (success/warning/danger).</summary>
    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    /// <summary>
    /// Input ligado al form "Asignar" — hidratado manualmente desde
    /// <c>Request.Form</c> por el handler para evitar binding implícito de
    /// <c>Guid</c> cuando el usuario todavía no eligió un valor.
    /// </summary>
    public PersonaHabilidadAsignarInputModel AsignarInput { get; set; } = new();

    // ──────────────────────────────────────────────
    // GET
    // ──────────────────────────────────────────────

    /// <summary>Handler GET de la página de habilidades de una persona.</summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        try
        {
            var persona = await personaApiClient.GetByIdAsync(id, cancellationToken);
            if (persona is null || !persona.IsActive)
            {
                logger.LogWarning(
                    "Persona with Id {PersonaId} is not available for skill management.",
                    id);
                return Redirect("/error/404");
            }

            var skills = await personaApiClient.GetSkillsAsync(id, cancellationToken);
            var (habilidades, niveles, catalogsFailed) = await LoadCatalogsAsync(id, cancellationToken);
            ViewModel = PersonaHabilidadesViewModel.From(persona, skills, habilidades, niveles);
            if (catalogsFailed)
            {
                ViewModel = ViewModel with
                {
                    IsRecoverable = true,
                    ErrorMessage = "No se pudo cargar el catálogo de habilidades o niveles."
                };
            }
            return Page();
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException
            or OperationCanceledException
            or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "Failed to load skills page for persona {PersonaId}.", id);
            ViewModel = ViewModel with
            {
                PersonaId = id,
                IsRecoverable = true,
                ErrorMessage = "No se pudo cargar la página de habilidades. Intentá nuevamente."
            };
            return Page();
        }
    }

    // ──────────────────────────────────────────────
    // POST handlers — patrón análogo a CargoHabilidadesPostHandlers.
    // Cada handler:
    // 1. Gatea admin.
    // 2. Gatea persona activa (no invoca el cliente si la persona está
    //    inactiva/eliminada — incluso si el antiforgery pasó).
    // 3. Ejecuta la operación o devuelve feedback legible por PRG.
    // ──────────────────────────────────────────────

    /// <summary>
    /// Handler POST que recibe un upsert (PUT idempotente) del nivel de
    /// habilidad para una persona. Cubre tanto el formulario "Asignar"
    /// del pie de página como la fila "Actualizar" de la grilla.
    /// </summary>
    public async Task<IActionResult> OnPostAsignarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        AsignarInput = PersonaSkillFormHelpers.ReadAsignarInput(Request.Form, ModelState);
        if (!ModelState.IsValid)
        {
            await ReloadAfterFailedAsignarAsync(id, cancellationToken);
            return Page();
        }

        if (!await EnsurePersonaActivaAsync(id, cancellationToken))
        {
            return RedirectToPage(new { id });
        }

        var request = new AsignarPersonaSkillRequest(AsignarInput.NivelHabilidadId!.Value);

        PersonaSkillCommandResult result;
        try
        {
            result = await personaApiClient.UpsertSkillAsync(
                id, AsignarInput.SkillId!.Value, request, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Persona skill upsert transport failure for persona {PersonaId}.", id);
            PageFeedback.SetDanger(TempData, PageFeedback.TransportMessage);
            return RedirectToPage(new { id });
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData,
                "La habilidad se asignó correctamente a la persona.");
            return RedirectToPage(new { id });
        }

        PageFeedback.SetDanger(TempData,
            PersonaSkillFormHelpers.ResolveFailureMessage(result));
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Handler POST que recibe la baja (DELETE) de una habilidad asociada
    /// a una persona. Maneja NotFound con feedback warning para reflejar
    /// la race condition natural (otra pestaña quitó la asociación).
    /// </summary>
    public async Task<IActionResult> OnPostQuitarAsync(
        Guid id,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        if (!await EnsurePersonaActivaAsync(id, cancellationToken))
        {
            return RedirectToPage(new { id });
        }

        PersonaSkillDeleteResult result;
        try
        {
            result = await personaApiClient.DeleteSkillAsync(id, skillId, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Persona skill delete transport failure for persona {PersonaId}.", id);
            PageFeedback.SetDanger(TempData, PageFeedback.TransportMessage);
            return RedirectToPage(new { id });
        }

        if (result.Succeeded)
        {
            PageFeedback.SetSuccess(TempData,
                "La habilidad se quitó de la persona correctamente.");
            return RedirectToPage(new { id });
        }

        // 404 = ya no existe: race condition natural, feedback warning para
        // que el siguiente GET refresque la grilla sin asustar al usuario.
        if (result.Categoria == ErrorCategoria.NotFound)
        {
            PageFeedback.SetWarning(TempData,
                "La asociación ya no existe. La grilla fue actualizada.");
            return RedirectToPage(new { id });
        }

        var failureMessage = !string.IsNullOrWhiteSpace(result.Message)
            ? result.Message!
            : ErrorCategoryMapper.Map(result.Categoria);
        PageFeedback.SetDanger(TempData, failureMessage);
        return RedirectToPage(new { id });
    }

    // ──────────────────────────────────────────────
    // Internal helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Verifica que la persona esté activa y consultable antes de invocar
    /// el cliente HTTP. Si la API responde null o <c>IsActive == false</c>,
    /// devuelve <c>false</c>, registra warning y setea TempData con un
    /// feedback legible; el caller debe emitir PRG sin invocar al cliente.
    /// </summary>
    private async Task<bool> EnsurePersonaActivaAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var persona = await personaApiClient.GetByIdAsync(id, ct);
            if (persona is null || !persona.IsActive)
            {
                logger.LogWarning(
                    "Persona with Id {PersonaId} is inactive; mutation blocked.",
                    id);
                PageFeedback.SetWarning(TempData,
                    "La persona está inactiva. No se puede modificar su lista de habilidades.");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex,
                "Failed to load persona {PersonaId} during POST gate; blocking mutation conservatively.",
                id);
            PageFeedback.SetDanger(TempData, PageFeedback.TransportMessage);
            return false;
        }
    }

    /// <summary>
    /// Recarga la grilla y datos de la vista para que la página re-renderizada
    /// tras un fallo de validación del form Asignar muestre la persona y
    /// el catálogo de skills disponibles.
    /// </summary>
    private async Task ReloadAfterFailedAsignarAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var persona = await personaApiClient.GetByIdAsync(id, ct);
            if (persona is null || !persona.IsActive)
            {
                return;
            }

            var skills = await personaApiClient.GetSkillsAsync(id, ct);
            var (habilidades, niveles, catalogsFailed) = await LoadCatalogsAsync(id, ct);
            ViewModel = PersonaHabilidadesViewModel.From(persona, skills, habilidades, niveles);
            if (catalogsFailed)
            {
                ViewModel = ViewModel with
                {
                    IsRecoverable = true,
                    ErrorMessage = "No se pudo cargar el catálogo de habilidades o niveles."
                };
            }
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogWarning(ex,
                "Failed to reload PersonaHabilidades data after failed Asignar POST for {PersonaId}.",
                id);
        }
    }

    /// <summary>
    /// Carga en paralelo los catálogos de habilidades activas y de niveles
    /// de habilidad. Réplica estructural de
    /// <c>Habilidades.cshtml.cs::LoadSkillsAndCatalogsAsync</c> reducida a
    /// los dos clientes de catálogo (las asociaciones de la persona ya
    /// vienen del GET handler). Si la consulta falla por transporte, deja
    /// ambas colecciones vacías para que la vista muestre sólo el
    /// placeholder y devuelve <c>HasFailure = true</c> para que el caller
    /// marque el ViewModel como recuperable.
    /// </summary>
    internal async Task<(IReadOnlyList<HabilidadListItemViewModel> Habilidades,
        IReadOnlyList<NivelHabilidadDto> Niveles, bool HasFailure)>
        LoadCatalogsAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var habilidadesTask = habilidadApiClient.GetAllAsync(ct);
            var nivelesTask = habilidadApiClient.GetNivelesHabilidadAsync(ct);
            await Task.WhenAll(habilidadesTask, nivelesTask);

            var habilidades = habilidadesTask.Result
                .Select(h => new HabilidadListItemViewModel(
                    h.Id, h.Codigo, h.Nombre, h.Descripcion, h.CategoriaNombre))
                .ToArray();
            var niveles = (IReadOnlyList<NivelHabilidadDto>)nivelesTask.Result;

            return (habilidades, niveles, false);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex,
                "Failed to load catalogs for PersonaHabilidades page (personaId={PersonaId}).", id);
            return ([], [], true);
        }
    }
}

/// <summary>
/// Estado de presentación de la página de habilidades.
/// </summary>
public sealed record PersonaHabilidadesViewModel
{
    /// <summary>Identificador de la persona.</summary>
    public Guid PersonaId { get; init; }

    /// <summary>Nombre completo mostrado como encabezado.</summary>
    public string PersonaNombre { get; init; } = string.Empty;

    /// <summary>Filas de asociaciones cargadas desde el backend.</summary>
    public IReadOnlyList<PersonaHabilidadRowViewModel> Skills { get; init; } = [];

    /// <summary>
    /// Catálogo de habilidades activas para los <c>&lt;select&gt;</c> del
    /// form "Asignar". Se popula en el GET handler reutilizando el
    /// <c>HabilidadListItemViewModel</c> del módulo Cargo.
    /// </summary>
    public IReadOnlyList<HabilidadListItemViewModel> HabilidadesDisponibles { get; init; } = [];

    /// <summary>Catálogo de niveles para los <c>&lt;select&gt;</c> del form "Asignar".</summary>
    public IReadOnlyList<NivelHabilidadDto> NivelOptions { get; init; } = [];

    /// <summary>Indica que la carga falló de forma recuperable.</summary>
    public bool IsRecoverable { get; init; }

    /// <summary>Mensaje visible para un fallo recuperable.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Mapea los wire-types a los datos que la vista necesita.</summary>
    public static PersonaHabilidadesViewModel From(
        PersonaDto persona,
        IReadOnlyList<PersonaSkillDetailDto> skills)
        => new()
        {
            PersonaId = persona.Id,
            PersonaNombre = $"{persona.Nombres} {persona.Apellidos}",
            Skills = skills
                .Select(skill => PersonaHabilidadRowViewModel.From(skill))
                .ToArray()
        };

    /// <summary>
    /// Overload que además popula los catálogos para los
    /// <c>&lt;select&gt;</c> del form "Asignar". Lo consume el GET handler
    /// y el reload tras un POST inválido.
    /// </summary>
    public static PersonaHabilidadesViewModel From(
        PersonaDto persona,
        IReadOnlyList<PersonaSkillDetailDto> skills,
        IReadOnlyList<HabilidadListItemViewModel> habilidades,
        IReadOnlyList<NivelHabilidadDto> niveles)
    {
        var assignedSkillIds = skills.Select(skill => skill.Skill.Id).ToHashSet();

        return new()
        {
            PersonaId = persona.Id,
            PersonaNombre = $"{persona.Nombres} {persona.Apellidos}",
            Skills = skills
                .Select(skill => PersonaHabilidadRowViewModel.From(skill))
                .ToArray(),
            HabilidadesDisponibles = habilidades
                .Where(habilidad => !assignedSkillIds.Contains(habilidad.Id))
                .ToArray(),
            NivelOptions = niveles
        };
    }
}

/// <summary>Fila de una asociación Persona-Habilidad para la grilla.</summary>
public sealed record PersonaHabilidadRowViewModel(
    Guid SkillId,
    string SkillCodigo,
    string SkillNombre,
    Guid NivelHabilidadId,
    string NivelNombre)
{
    /// <summary>Mapea el DTO anidado al modelo de presentación.</summary>
    public static PersonaHabilidadRowViewModel From(PersonaSkillDetailDto skill)
        => new(
            skill.Skill.Id,
            skill.Skill.Codigo,
            skill.Skill.Nombre,
            skill.Nivel.Id,
            skill.Nivel.Nombre);
}

/// <summary>
/// Input del formulario "Asignar" — SkillId y NivelHabilidadId se
/// hidratan manualmente desde <c>Request.Form</c> para evitar binding
/// implícito de <see cref="Guid"/> cuando el usuario todavía no eligió
/// un valor.
/// </summary>
public sealed class PersonaHabilidadAsignarInputModel
{
    public Guid? SkillId { get; set; }

    public Guid? NivelHabilidadId { get; set; }
}

/// <summary>
/// Helpers de parseo de form para la página PersonaHabilidades. Réplica
/// estructural de <c>CargoSkillFormHelpers</c> reducida al subdominio
/// Persona-Skill (sin Ponderacion/EsObligatoria/NivelRequeridoId).
/// </summary>
public static class PersonaSkillFormHelpers
{
    /// <summary>
    /// Lee <c>SkillId</c> y <c>NivelHabilidadId</c> del form, marca
    /// ModelState si faltan y devuelve un input model con los valores
    /// (o null) listos para enviar al cliente HTTP.
    /// </summary>
    public static PersonaHabilidadAsignarInputModel ReadAsignarInput(
        IFormCollection form,
        ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(modelState);

        var skillIdRaw = form["SkillId"].ToString();
        var nivelRaw = form["NivelHabilidadId"].ToString();

        Guid? skillId = Guid.TryParse(skillIdRaw, out var parsedSkill) && parsedSkill != Guid.Empty
            ? parsedSkill
            : null;
        Guid? nivelId = Guid.TryParse(nivelRaw, out var parsedNivel) && parsedNivel != Guid.Empty
            ? parsedNivel
            : null;

        if (skillId is null)
        {
            modelState.AddModelError("SkillId", "Debe seleccionar una habilidad.");
        }

        if (nivelId is null)
        {
            modelState.AddModelError("NivelHabilidadId", "Debe seleccionar un nivel.");
        }

        return new PersonaHabilidadAsignarInputModel
        {
            SkillId = skillId,
            NivelHabilidadId = nivelId
        };
    }

    /// <summary>
    /// Resuelve el mensaje de feedback para un Failure de upsert. La
    /// fuente de verdad es <see cref="ErrorCategoria"/>; cuando el
    /// subdominio aporta un <c>Message</c> con texto accionable, se
    /// preserva.
    /// </summary>
    public static string ResolveFailureMessage(PersonaSkillCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Error is null)
        {
            return ErrorCategoryMapper.Map(ErrorCategoria.Unexpected);
        }

        return result.Error.Categoria switch
        {
            ErrorCategoria.NotFound => "La persona o la habilidad solicitada no existe.",
            ErrorCategoria.Conflict => result.Error.Message,
            ErrorCategoria.Validation => result.Error.Message,
            ErrorCategoria.Unauthorized => PageFeedback.UnauthorizedMessage,
            ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
            ErrorCategoria.Transport => PageFeedback.TransportMessage,
            _ => result.Error.Message
        };
    }
}
