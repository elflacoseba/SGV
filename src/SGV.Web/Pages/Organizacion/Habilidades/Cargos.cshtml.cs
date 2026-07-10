using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Seguridad;
using SGV.Web.Integration.Habilidades;

namespace SGV.Web.Pages.Organizacion.Habilidades;

/// <summary>
/// PageModel readonly de la página <c>Habilidades/Cargos</c>. Muestra los
/// cargos asociados a una habilidad usando el subrecurso
/// <c>GET /api/v1/skills/{skillId}/cargos</c> (espejo del subrecurso
/// <c>Cargos/Habilidades</c> del lado Cargo). La página distingue entre
/// "habilidad inexistente" (estado recuperable) y "habilidad sin cargos en
/// el segmento" (estado vacío con tabla). Cualquier usuario autenticado
/// puede navegar; el botón <c>Gestionar habilidades del cargo</c> solo se
/// renderiza para <see cref="RolesSgv.Administrador"/> para evitar el 403
/// que produciría la página admin-only destino.
/// </summary>
[Authorize]
public sealed class HabilidadesCargosModel(
    IHabilidadApiClient habilidadApiClient,
    ILogger<HabilidadesCargosModel> logger) : PageModel
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const string DeletedView = "eliminadas";

    /// <summary>
    /// Identificador de la habilidad padre del subrecurso.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    /// <summary>
    /// Página actual (1-based). Se normaliza a ≥ 1 antes de delegar al cliente.
    /// Se nombra <c>CurrentPage</c> para evitar colisión con
    /// <see cref="PageModel.Page"/> (método auxiliar de Razor Pages).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Tamaño de página solicitado. Se normaliza a [1..<see cref="MaxPageSize"/>].
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>
    /// Filtro textual opcional pasado al subrecurso (búsqueda por código/nombre).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    /// <summary>
    /// Expresión de orden opcional (e.g. <c>codigo_asc</c>).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    /// <summary>
    /// Segmento vigente en la query: <c>"eliminadas"</c> para ver cargos
    /// soft-deleted; cualquier otro valor (incluido <c>null</c>) cae a activas.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    /// <summary>
    /// Filas de la grilla; <c>empty</c> mientras no se haya cargado o cuando
    /// la habilidad no tiene cargos en el segmento vigente.
    /// </summary>
    public IReadOnlyList<HabilidadCargoListItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Total reportado por el subrecurso para el segmento y filtros vigentes.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Total de páginas calculado a partir de <see cref="TotalCount"/> y
    /// <see cref="PageSize"/>; mínimo 1.
    /// </summary>
    public int TotalPages { get; private set; } = 1;

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
    /// <c>true</c> cuando el usuario autenticado pertenece al rol
    /// <see cref="RolesSgv.Administrador"/>. El botón "Gestionar habilidades
    /// del cargo" sólo se renderiza en ese caso para evitar el 403 que
    /// produciría la página admin-only destino.
    /// </summary>
    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// Carga el subrecurso de cargos para la habilidad vigente. Si la
    /// habilidad no existe (404 devuelto por <c>GetByIdAsync</c>) entra en
    /// estado recuperable. Las fallas de transporte se traducen en un
    /// <see cref="ErrorMessage"/> accionable y un estado vacío sin stack
    /// trace filtrado al HTML.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // PR #88 (review 🟡5): fail-fast explícito para Guid.Empty. El route
        // constraint {id:guid} acepta Guid.Empty como Guid válido; el
        // comportamiento implícito (GetByIdAsync retorna null → estado
        // recuperable con copy "no está disponible") es correcto pero
        // ambiguo. Validar acá permite un mensaje específico y evita
        // un round-trip innecesario al cliente HTTP.
        if (Id == Guid.Empty)
        {
            IsRecoverable = true;
            ErrorMessage = "El identificador de la habilidad es inválido.";
            logger.LogWarning("Cargos page invoked with Guid.Empty.");
            return Page();
        }

        HabilidadDto? habilidad;
        try
        {
            habilidad = await habilidadApiClient.GetByIdAsync(Id, cancellationToken);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load habilidad with Id {HabilidadId} for cargos page.", Id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar la página de cargos. Intentá nuevamente.";
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
        PageSize = PageSize < 1 ? DefaultPageSize : Math.Min(MaxPageSize, PageSize);
        Search = Normalize(Search);
        Sort = Normalize(Sort);

        var segmento = string.Equals(Status, DeletedView, StringComparison.OrdinalIgnoreCase)
            ? HabilidadSegmentoListado.Eliminadas
            : HabilidadSegmentoListado.Activas;

        try
        {
            var result = await habilidadApiClient.GetCargosAsync(
                Id,
                new HabilidadCargosListQuery(CurrentPage, PageSize, Search, Sort, segmento),
                cancellationToken);

            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, result.PageSize)));

            Items = result.Items.Select(MapToViewModel).ToArray();
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            // Mismo patrón que la falla de GetByIdAsync arriba: una falla de
            // transporte del subrecurso se traduce a estado recuperable con
            // mensaje accionable. Sin IsRecoverable = true la vista
            // renderizaría simultáneamente el banner de error y el empty
            // state "no hay cargos" — UX contradictoria.
            logger.LogError(ex, "Failed to load cargos for habilidad {HabilidadId}.", Id);
            IsRecoverable = true;
            ErrorMessage = "No se pudo cargar el listado de cargos asociados. Intentá nuevamente.";
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
    /// Construye los route values para el botón "Detalle del cargo" de cada
    /// fila. Sólo necesita el <c>id</c> del cargo destino; la página de
    /// detalle de Cargos no acepta el contexto del listado de habilidades.
    /// </summary>
    public object BuildCargoDetailsRouteValues(Guid id) => new
    {
        id,
    };

    /// <summary>
    /// Construye los route values para el botón admin-only
    /// "Gestionar habilidades del cargo". Sólo se renderiza cuando
    /// <see cref="EsAdministrador"/> es <c>true</c>.
    /// </summary>
    public object BuildGestionarHabilidadesRouteValues(Guid id) => new
    {
        id,
    };

    /// <summary>
    /// Construye los route values para los enlaces de paginación preservando
    /// <c>search</c>, <c>sort</c> y <c>status</c>.
    /// </summary>
    public object BuildPaginationRouteValues(int page) => new
    {
        id = Id,
        p = page,
        pageSize = PageSize,
        search = Search,
        sort = Sort,
        status = Status,
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

    private static HabilidadCargoListItemViewModel MapToViewModel(SkillCargoDetailDto item) =>
        new(
            item.CargoId,
            item.Cargo.Codigo,
            item.Cargo.Nombre,
            item.Nivel.Nombre);
}

/// <summary>
/// ViewModel plano para una fila de la grilla de cargos asociados a una
/// habilidad. Conserva sólo los campos que la vista renderiza
/// (Código, Nombre, Nivel del cargo y su id para construir el route
/// value del botón "Detalle del cargo"). Los datos del vínculo
/// (<c>NivelRequeridoId</c>, <c>Ponderacion</c>, <c>EsObligatoria</c>)
/// viajan en el DTO <see cref="SkillCargoDetailDto"/> por contrato del
/// subrecurso (skill-cargo-query-contract Req 1) pero la página readonly
/// no los consume en UI; se omiten acá para no propagar
/// sobre-exposición al view.
/// </summary>
public sealed record HabilidadCargoListItemViewModel(
    Guid CargoId,
    string Codigo,
    string Nombre,
    string NivelNombre);