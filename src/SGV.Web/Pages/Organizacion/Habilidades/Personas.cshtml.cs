using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Habilidades;

namespace SGV.Web.Pages.Organizacion.Habilidades;

/// <summary>
/// PageModel readonly de la página <c>Habilidades/Personas</c>. Muestra las
/// personas asociadas a una habilidad consumiendo el subrecurso
/// <c>GET /api/v1/skills/{skillId}/personas</c> (espejo del subrecurso
/// <c>Cargos/Habilidades</c> del lado Cargo). La página distingue entre
/// "habilidad inexistente" (estado recuperable) y "habilidad sin personas en
/// el segmento" (estado vacío con tabla).
/// <para>
/// Acceso autenticado sin restricción de rol (REQ-HM-NEW-AUTH). La gestión
/// del vínculo Persona↔Habilidad NO se expone acá (REQ-HM-NEW-READONLY):
/// sigue viviendo en <c>Pages/Personas/PersonaHabilidades</c>.
/// </para>
/// </summary>
[Authorize]
public sealed class PersonasModel(
    IHabilidadApiClient habilidadApiClient,
    ILogger<PersonasModel> logger) : PageModel
{
    private const int DefaultPageSize = 20;
    private const string DeletedView = "eliminadas";

    /// <summary>
    /// Identificador de la habilidad padre del subrecurso.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    /// <summary>
    /// Página actual (1-based). Se normaliza a ≥ 1 antes de delegar al
    /// cliente. Se nombra <c>CurrentPage</c> para evitar colisión con
    /// <see cref="PageModel.Page"/> (método auxiliar de Razor Pages).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Tamaño de página solicitado. Por defecto 20.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>
    /// Filtro textual opcional pasado al subrecurso (búsqueda por
    /// legajo/nombres/apellidos).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    /// <summary>
    /// Expresión de orden opcional (e.g. <c>apellidos_asc</c>).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    /// <summary>
    /// Segmento vigente en la query: <c>"eliminadas"</c> para ver personas
    /// soft-deleted; cualquier otro valor (incluido <c>null</c>) cae a
    /// activas.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    /// <summary>
    /// Filas de la grilla; <c>empty</c> mientras no se haya cargado o cuando
    /// la habilidad no tiene personas en el segmento vigente.
    /// </summary>
    public IReadOnlyList<HabilidadPersonaListItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Total reportado por el subrecurso para el segmento y filtros vigentes.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Nombre de la habilidad para mostrar en el header. <c>null</c> cuando
    /// <see cref="IsRecoverable"/> es <c>true</c>.
    /// </summary>
    public string? HabilidadNombre { get; private set; }

    /// <summary>
    /// <c>true</c> cuando la habilidad padre no existe o la consulta inicial
    /// falla con un error recuperable. La vista muestra el estado
    /// "La habilidad solicitada no está disponible." en lugar de la grilla.
    /// </summary>
    public bool IsRecoverable { get; private set; }

    /// <summary>
    /// Mensaje de error visible cuando la carga inicial falla con un error
    /// de transporte recuperable. Esta página es read-only.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// <c>true</c> cuando el segmento vigente es <c>eliminadas</c>.
    /// </summary>
    public bool IsDeletedView =>
        string.Equals(Status, DeletedView, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Carga el subrecurso de personas para la habilidad vigente. Si la
    /// habilidad no existe (404 devuelto por <c>GetByIdAsync</c>) entra en
    /// estado recuperable. Las fallas de transporte se traducen en un
    /// <see cref="ErrorMessage"/> accionable y un estado vacío sin stack
    /// trace filtrado al HTML.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Fail-fast explícito para Guid.Empty. El route constraint {id:guid}
        // acepta Guid.Empty como Guid válido; validar acá permite un mensaje
        // específico y evita un round-trip innecesario al cliente HTTP.
        if (Id == Guid.Empty)
        {
            IsRecoverable = true;
            ErrorMessage = "El identificador de la habilidad es inválido.";
            logger.LogWarning("Personas page invoked with Guid.Empty.");
            return Page();
        }

        HabilidadDto? habilidad;
        try
        {
            habilidad = await habilidadApiClient.GetByIdAsync(Id, cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load habilidad with Id {HabilidadId} for personas page.", Id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar la página de personas. Intentá nuevamente.";
            return Page();
        }

        if (habilidad is null)
        {
            IsRecoverable = true;
            ErrorMessage = "La habilidad solicitada no está disponible.";
            logger.LogWarning("Habilidad with Id {HabilidadId} was not found or is no longer available.", Id);
            return Page();
        }

        HabilidadNombre = habilidad.Nombre;

        CurrentPage = CurrentPage < 1 ? 1 : CurrentPage;
        if (PageSize < 1)
        {
            PageSize = DefaultPageSize;
        }

        Search = Normalize(Search);
        Sort = Normalize(Sort);

        var segmento = string.Equals(Status, DeletedView, StringComparison.OrdinalIgnoreCase)
            ? PersonaSegmentoListado.Eliminadas
            : PersonaSegmentoListado.Activas;

        try
        {
            var result = await habilidadApiClient.GetPersonasAsync(
                Id,
                new HabilidadPersonasListQuery(CurrentPage, PageSize, Search, Sort, segmento),
                cancellationToken);

            TotalCount = Math.Max(0, result.Total);
            Items = result.Items.Select(MapToViewModel).ToArray();
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load personas for habilidad {HabilidadId}.", Id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar el listado de personas asociadas. Intentá nuevamente.";
            return Page();
        }

        return Page();
    }

    /// <summary>
    /// Construye los route values para alternar entre los segmentos
    /// <c>activas</c> y <c>eliminadas</c> dentro de esta misma página.
    /// Resetea la paginación a 1 al alternar (espejo del patrón del Index).
    /// </summary>
    public object BuildToggleSegmentoRouteValues(string? targetSegmento) => new
    {
        id = Id,
        p = 1,
        pageSize = PageSize,
        search = Search,
        sort = Sort,
        status = string.Equals(targetSegmento, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null,
    };

    /// <summary>
    /// URL del botón "Volver al listado" (apunta al Index de Habilidades
    /// preservando el contexto vigente).
    /// </summary>
    public string BuildVolverAlListadoUrl()
    {
        return Url.Page("/Organizacion/Habilidades/Index", new
        {
            p = CurrentPage,
            search = Search,
            sort = Sort,
            status = Status,
        }) ?? "/organizacion/habilidades";
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsTransportFailure(Exception ex) =>
        ex is HttpRequestException ||
        ex is TaskCanceledException ||
        ex is JsonException;

    private static HabilidadPersonaListItemViewModel MapToViewModel(SkillPersonaDetailDto item) =>
        new(
            item.PersonaId,
            item.Persona.Legajo,
            item.Persona.Apellidos,
            item.Persona.Nombres,
            item.Persona.Email,
            item.Nivel.Nombre);
}

/// <summary>
/// ViewModel plano para una fila de la grilla de personas asociadas a una
/// habilidad. Conserva sólo los campos que la vista renderiza (Legajo,
/// Apellidos, Nombres, Email, Nivel del vínculo + PersonaId para el route
/// value del detalle de Persona).
/// </summary>
public sealed record HabilidadPersonaListItemViewModel(
    Guid PersonaId,
    string? Legajo,
    string Apellidos,
    string Nombres,
    string? Email,
    string NivelNombre);