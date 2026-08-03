using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Auditoria;
using SGV.Web.Integration.Common;

namespace SGV.Web.Pages.Auditorias;

/// <summary>
/// PageModel readonly del listado transversal de auditoría (Slice B
/// del change <c>2026-07-31-ajustes-listado-auditoria</c> /
/// issue #248). Consume el endpoint admin-only
/// <c>GET /api/v1/auditorias</c> vía
/// <see cref="IAuditoriaApiClient"/> y aplica los filtros
/// combinables (<c>EntityName</c>, <c>Operation</c>, <c>DateFrom</c>,
/// <c>DateTo</c>, <c>UserId</c>, <c>CorrelationId</c>) más orden
/// server-side (<c>Sort</c>) y selector de <c>PageSize</c>.
/// </summary>
/// <remarks>
/// <para>
/// Acceso restringido al rol
/// <see cref="RolesSgv.Administrador"/> (D-1). El
/// <c>[Authorize]</c> cubre la autorización por acción; la
/// autorización por filtro vive en el backend y se reusa el
/// guard de la pipeline JWT del shell web
/// (<c>ApiBearerTokenHandler</c>).
/// </para>
/// <para>
/// Slice B: la sidebar lateral se reemplaza por una toolbar
/// horizontal de filtros (estilo <c>Habilidades/Index</c>) sobre la
/// tabla. Los <c>&lt;th&gt;</c> ordenables exponen el criterio
/// vigente y permiten alternar dirección con un click; cambiar el
/// criterio resetea la página a 1 (spec <c>auditoria-sort</c>). El
/// <c>&lt;select&gt;</c> de <c>pageSize</c> restringe el universo a
/// {10, 20, 50, 100} y cae al default (20) ante input inválido
/// (spec <c>auditoria-page-size</c>).
/// </para>
/// <para>
/// Las fallas de transporte se ramifican vía
/// <see cref="TransportFailureClassifier"/> y se traducen a un
/// banner recuperable; el orden de renderizado
/// (banner de error → empty state → tabla) preserva la UX
/// aún ante caídas de la API upstream.
/// </para>
/// </remarks>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class IndexModel(
    IAuditoriaApiClient auditoriaApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    /// <summary>
    /// Tamaño de página default para el shell de auditoría (espejo
    /// de los otros módulos; debe matchear la entrada del
    /// selector <c>{10, 20, 50, 100}</c>).
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Tamaño de página máximo aceptado por el selector (el
    /// backend clampea a <c>[1, 100]</c>; la shell restringe
    /// aún más al set canónico {10, 20, 50, 100}).
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Conjunto canónico de <c>PageSize</c> expuesto por el
    /// selector (spec <c>auditoria-page-size</c>). Cualquier valor
    /// fuera de este set cae a <see cref="DefaultPageSize"/>.
    /// </summary>
    public static readonly IReadOnlyCollection<int> AllowedPageSizes = new[] { 10, 20, 50, 100 };

    /// <summary>
    /// Orden server-side por defecto. <see cref="AuditoriaServicioConsulta"/>
    /// también cae aquí en <c>switch(Sort)</c> cuando el valor es
    /// null/vacío/no reconocido (D-6).
    /// </summary>
    public const string DefaultSort = "fecha_desc";

    /// <summary>Filas visibles en la página actual.</summary>
    public IReadOnlyList<AuditoriaDto> Items { get; private set; } = [];

    /// <summary>Página actual (1-based).</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Cantidad total de páginas calculadas a partir del backend.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Total de filas que matchean los filtros vigentes.</summary>
    public int TotalCount { get; private set; }

    /// <summary>Filtro vigente: nombre de la entidad auditada.</summary>
    public string? EntityName { get; private set; }

    /// <summary>Filtro vigente: operación (Alta / Modificacion / BajaLogica / etc).</summary>
    public string? Operation { get; private set; }

    /// <summary>Filtro vigente: fecha desde (inclusivo).</summary>
    public DateTime? DateFrom { get; private set; }

    /// <summary>Filtro vigente: fecha hasta (inclusivo).</summary>
    public DateTime? DateTo { get; private set; }

    /// <summary>Filtro vigente: identificador del usuario que ejecutó la operación.</summary>
    public string? UserId { get; private set; }

    /// <summary>
    /// Filtro vigente: identificador de correlación (aísla los
    /// registros que comparten un mismo <c>CorrelationId</c>).
    /// </summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>
    /// Criterio de orden server-side vigente (claves
    /// <c>{fecha|entidad|operacion|usuario|correlacion}_{asc|desc}</c>;
    /// <c>null</c> o valor no reconocido se normaliza al
    /// <see cref="DefaultSort"/>).
    /// </summary>
    public string? Sort { get; private set; }

    /// <summary>
    /// Tamaño de página vigente (siempre dentro del set
    /// canónico {10, 20, 50, 100}).
    /// </summary>
    public int PageSize { get; private set; } = DefaultPageSize;

    /// <summary>
    /// Mensaje de error visible cuando la carga inicial del
    /// listado falla con un error de transporte recuperable.
    /// </summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>
    /// Handler GET del listado. Carga el listado aplicando los
    /// filtros del querystring + orden + pageSize. Cualquier
    /// excepción de transporte capturada por
    /// <see cref="TransportFailureClassifier"/> se traduce a un
    /// banner accionable sin filtrar stack trace al HTML. El
    /// resto de las excepciones se propagan para no enmascarar
    /// bugs reales.
    /// </summary>
    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int currentPage = 1,
        int pageSize = DefaultPageSize,
        string? entityName = null,
        string? operation = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? userId = null,
        string? sort = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = currentPage < 1 ? 1 : currentPage;
        PageSize = NormalizePageSize(pageSize);
        EntityName = Normalize(entityName);
        Operation = Normalize(operation);
        DateFrom = dateFrom;
        DateTo = dateTo;
        UserId = Normalize(userId);
        Sort = NormalizeSort(sort);
        CorrelationId = correlationId;

        var query = new AuditoriaListQuery(
            Page: CurrentPage,
            PageSize: PageSize,
            EntityName: EntityName,
            Operation: Operation,
            DateFrom: DateFrom,
            DateTo: DateTo,
            UserId: UserId,
            Sort: Sort,
            CorrelationId: CorrelationId);

        try
        {
            var result = await auditoriaApiClient
                .QueryAsync(query, cancellationToken)
                .ConfigureAwait(false);

            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, result.PageSize)));
            Items = result.Items.ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load auditoria page: transport failure.");
            SetLoadErrorState();
        }
    }

    /// <summary>
    /// Construye los route values de un enlace de paginación
    /// preservando los filtros vigentes y el orden + pageSize
    /// vigentes. Espejo del patrón de
    /// <c>PuestoIndexModel.BuildPagedRouteValues</c>: usa
    /// <c>p</c> como key del route value (no <c>page</c>) porque
    /// Razor Pages reserva el nombre <c>page</c> como
    /// identificador interno de la página y omite cualquier
    /// route value con ese nombre del URL generado. El binding
    /// del handler lo reescribe vía <c>[FromQuery(Name = "p")]</c>.
    /// </summary>
    public object BuildPagedRouteValues(int page) => new
    {
        p = Math.Max(1, page),
        pageSize = PageSize,
        entityName = EntityName,
        operation = Operation,
        dateFrom = DateFrom,
        dateTo = DateTo,
        userId = UserId,
        sort = Sort,
        correlationId = CorrelationId
    };

    /// <summary>
    /// Construye los route values para los <c>&lt;th&gt;</c>
    /// ordenables. Resetea <c>p</c> a 1 (el primer impacto del
    /// cambio de criterio cae siempre en la primera página,
    /// coherente con la spec <c>auditoria-sort</c>) y preserva
    /// <c>pageSize</c>, los filtros vigentes y el resto del
    /// contexto. La clave resultante
    /// (<c>{columna}_asc</c>/<c>{columna}_desc</c>) se calcula
    /// en <see cref="GetSortRoute"/>.
    /// </summary>
    public object BuildSortRouteValues(string sortKey) => new
    {
        p = 1,
        pageSize = PageSize,
        entityName = EntityName,
        operation = Operation,
        dateFrom = DateFrom,
        dateTo = DateTo,
        userId = UserId,
        sort = GetSortRoute(sortKey),
        correlationId = CorrelationId
    };

    /// <summary>
    /// Construye los route values del enlace "Detalle" de una
    /// fila. Preserva el contexto vigente del listado (<c>p</c>,
    /// <c>pageSize</c>, <c>sort</c>, <c>correlationId</c> y los
    /// filtros) para que la page <c>Details</c> pueda ofrecer un
    /// "Volver al listado" con el mismo estado en el que el
    /// usuario estaba antes de descender al detalle.
    /// </summary>
    public object BuildDetailsRouteValues(Guid id) => new
    {
        id,
        p = CurrentPage,
        pageSize = PageSize,
        sort = Sort,
        correlationId = CorrelationId,
        entityName = EntityName,
        operation = Operation,
        dateFrom = DateFrom,
        dateTo = DateTo,
        userId = UserId
    };

    /// <summary>
    /// Calcula la clave de orden que corresponde al click sobre
    /// el header <paramref name="column"/>: si el orden vigente
    /// ya es por esa misma columna ascendente, alterna a
    /// descendente; en cualquier otro caso arranca ascendente.
    /// Espejo del patrón de <c>Habilidades/Index</c>.
    /// </summary>
    public string GetSortRoute(string column)
    {
        var isSameColumn = Sort?.StartsWith(column, StringComparison.OrdinalIgnoreCase) == true;
        var isDesc = Sort?.EndsWith("_desc", StringComparison.OrdinalIgnoreCase) == true;

        return isSameColumn && !isDesc
            ? $"{column}_desc"
            : $"{column}_asc";
    }

    /// <summary>
    /// Devuelve el icono Ti adecuado para la columna
    /// <paramref name="column"/> según el orden vigente, o
    /// <c>null</c> cuando la columna no es la del sort activo
    /// (para no pintar indicadores en headers irrelevantes).
    /// </summary>
    public string? GetSortIcon(string column)
    {
        if (Sort is null) return null;

        var isSameColumn = Sort.StartsWith(column, StringComparison.OrdinalIgnoreCase);
        if (!isSameColumn) return null;

        return Sort.EndsWith("_desc", StringComparison.OrdinalIgnoreCase)
            ? "ti ti-arrow-down"
            : "ti ti-arrow-up";
    }

    /// <summary>
    /// Resetea el estado de carga a un fallback vacío tras un
    /// fallo controlado de carga inicial. Mismo patrón que el
    /// resto de los Index read-only del shell
    /// (Habilidades/Cargos/Personas): cualquier excepción
    /// considerada transporte/serialización por
    /// <see cref="TransportFailureClassifier"/> cae acá.
    /// </summary>
    private void SetLoadErrorState()
    {
        Items = [];
        TotalCount = 0;
        CurrentPage = 1;
        TotalPages = 1;
        LoadErrorMessage = "No se pudo cargar el listado de auditoría. Intentá nuevamente.";
    }

    /// <summary>
    /// Normaliza un parámetro de filtro opcional: <c>null</c> o
    /// whitespace → <c>null</c>; en otro caso
    /// <see cref="string.Trim"/>.
    /// </summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Normaliza el <c>Sort</c> recibido por querystring. La
    /// primera línea de defensa es el
    /// <c>switch(query.Sort)</c> del
    /// <see cref="AuditoriaServicioConsulta"/> (D-6: valor no
    /// reconocido → <c>fecha_desc</c> sin error), pero acá
    /// queremos reflejar el criterio vigente en los <c>&lt;th&gt;</c>
    /// ordenables para pintar el icono correcto, así que la shell
    /// también normaliza a <see cref="DefaultSort"/> cuando el valor
    /// es null/vacío. Validación de claves conocidas queda en el
    /// servidor — acá solo colapsamos a default.
    /// </summary>
    private static string? NormalizeSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultSort;
        var trimmed = value.Trim();
        return trimmed switch
        {
            "fecha_asc" or "fecha_desc"
                or "entidad_asc" or "entidad_desc"
                or "operacion_asc" or "operacion_desc"
                or "usuario_asc" or "usuario_desc"
                or "correlacion_asc" or "correlacion_desc" => trimmed,
            _ => DefaultSort
        };
    }

    /// <summary>
    /// Normaliza el <c>PageSize</c> recibido por querystring al
    /// set canónico del selector
    /// {<see cref="DefaultPageSize"/>, 50, 100}. Cualquier valor
    /// fuera del set (incluyendo 0, negativos, no numéricos
    /// representados como <c>0</c> por el binder) cae a
    /// <see cref="DefaultPageSize"/>. La API conserva su propio
    /// clamp <c>[1, 100]</c>; la shell es la primera línea de
    /// normalización visible para el usuario (spec
    /// <c>auditoria-page-size</c> §"PageSize inválido o fuera de
    /// rango se normaliza").
    /// </summary>
    private static int NormalizePageSize(int value)
    {
        if (value <= 0) return DefaultPageSize;
        return AllowedPageSizes.Contains(value) ? value : DefaultPageSize;
    }
}
