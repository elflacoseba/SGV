using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    /// <summary>Filtro vigente: nombre legible del usuario que ejecutó la operación.</summary>
    public string? UserName { get; private set; }

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
    /// Opciones para el <c>&lt;select&gt;</c> de filtro
    /// <c>EntityName</c>, pobladas desde el endpoint
    /// <c>GET /api/v1/auditorias/filter-options</c>. La primera
    /// opción es <c>Todos</c> (<c>value=""</c>) y permite limpiar
    /// el filtro desde la UI. <c>null</c> cuando el endpoint de
    /// opciones falló y la vista debe renderizar un
    /// <c>&lt;input type="search"&gt;</c> en su lugar (fallback
    /// no bloqueante, spec <c>auditoria-query</c>
    /// §"Shell web admin-only" — escenario "filtros como select").
    /// </summary>
    public IReadOnlyList<SelectListItem>? EntityNameOptions { get; private set; }

    /// <summary>
    /// Opciones para el <c>&lt;select&gt;</c> de filtro
    /// <c>Operation</c>. Misma semántica que
    /// <see cref="EntityNameOptions"/>: <c>null</c> en fallback.
    /// </summary>
    public IReadOnlyList<SelectListItem>? OperationOptions { get; private set; }

    /// <summary>
    /// <c>true</c> cuando el endpoint de filter-options falló con
    /// un error de transporte recuperable. La vista usa esta
    /// bandera para mostrar <c>&lt;input type="search"&gt;</c> en
    /// lugar de los <c>&lt;select&gt;</c> y para pintar un banner
    /// no bloqueante (<c>alert-info</c>), NO un error rojo.
    /// </summary>
    public bool FilterOptionsLoadFailed { get; private set; }

    /// <summary>
    /// Mensaje del banner no bloqueante cuando
    /// <see cref="FilterOptionsLoadFailed"/> es <c>true</c>. El
    /// texto es canónico y se muestra en español por convención
    /// del shell (UI copy consistente con el resto del módulo).
    /// </summary>
    public string? FilterOptionsMessage { get; private set; }

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
        string? userName = null,
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
        UserName = Normalize(userName);
        Sort = NormalizeSort(sort);
        CorrelationId = correlationId;

        // Cargar las opciones de los <select> ANTES del listado
        // principal. Si falla, la vista cae al fallback
        // <input type="search"> + banner no bloqueante. La falla
        // NO interrumpe la carga del query (D-2: el listado
        // sigue siendo usable aunque el endpoint de opciones
        // responda con error).
        await LoadFilterOptionsAsync(cancellationToken).ConfigureAwait(false);

        var query = new AuditoriaListQuery(
            Page: CurrentPage,
            PageSize: PageSize,
            EntityName: EntityName,
            Operation: Operation,
            DateFrom: DateFrom,
            DateTo: DateTo,
            UserName: UserName,
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
        userName = UserName,
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
        userName = UserName,
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
        userName = UserName
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
    /// Carga las opciones para los <c>&lt;select&gt;</c> de
    /// <c>EntityName</c> y <c>Operation</c> desde el endpoint
    /// <c>GET /api/v1/auditorias/filter-options</c>. En éxito,
    /// construye los <see cref="SelectListItem"/> con una primera
    /// opción <c>Todos</c> (<c>value=""</c>) seguida de los
    /// valores del backend (ya ordenados alfabéticamente por el
    /// servicio). En falla de transporte, activa la bandera
    /// <see cref="FilterOptionsLoadFailed"/> y deja
    /// <see cref="EntityNameOptions"/>/<see cref="OperationOptions"/>
    /// en <c>null</c> para que la vista renderice los
    /// <c>&lt;input type="search"&gt;</c> de fallback.
    /// </summary>
    private async Task LoadFilterOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = await auditoriaApiClient
                .GetFilterOptionsAsync(cancellationToken)
                .ConfigureAwait(false);

            EntityNameOptions = BuildSelectListItems(
                values: options.EntityNames,
                selectedValue: EntityName);

            OperationOptions = BuildSelectListItems(
                values: options.Operations,
                selectedValue: Operation);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogWarning(
                ex,
                "Fallo de transporte al cargar filter-options para /auditorias; "
                + "se renderiza fallback a <input type=\"search\">.");

            EntityNameOptions = null;
            OperationOptions = null;
            FilterOptionsLoadFailed = true;
            FilterOptionsMessage =
                "No se pudieron cargar las opciones de filtros. Ingresá los valores manualmente.";
        }
    }

    /// <summary>
    /// Materializa una colección de strings en una lista de
    /// <see cref="SelectListItem"/> para renderizar en un
    /// <c>&lt;select&gt;</c>. La primera entrada es la opción
    /// neutra <c>Todos</c> (<c>value=""</c>); queda
    /// <c>selected</c> si el filtro vigente es null/vacío. Los
    /// strings subsiguientes se exponen con <c>value == text</c>
    /// (las opciones de filtro son strings simples, no tienen
    /// campos separados).
    /// </summary>
    /// <remarks>
    /// Si el filtro vigente tiene un valor que NO aparece en
    /// <paramref name="values"/> (e.g. la entidad/operación ya no
    /// existe en la tabla de auditoría, o el listado filtrado a
    /// futuro), lo añadimos como <c>&lt;option&gt;</c> seleccionada
    /// para preservar la intención del usuario en el round-trip.
    /// Sin esta salvaguarda, el select abriría en "Todos" y la
    /// próxima navegación perdería el filtro.
    /// </remarks>
    private static IReadOnlyList<SelectListItem> BuildSelectListItems(
        IReadOnlyList<string> values,
        string? selectedValue)
    {
        var result = new List<SelectListItem>(values.Count + 2)
        {
            new()
            {
                Value = string.Empty,
                Text = "Todos",
                Selected = string.IsNullOrEmpty(selectedValue)
            }
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!seen.Add(value))
            {
                continue;
            }

            result.Add(new SelectListItem
            {
                Value = value,
                Text = value,
                Selected = string.Equals(value, selectedValue, StringComparison.Ordinal)
            });
        }

        // Preservar el filtro vigente aunque no esté en la lista
        // (entidad/operación huérfana del catálogo actual).
        if (!string.IsNullOrEmpty(selectedValue)
            && !seen.Contains(selectedValue))
        {
            result.Add(new SelectListItem
            {
                Value = selectedValue,
                Text = selectedValue,
                Selected = true
            });
        }

        return result;
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
    /// <remarks>
    /// <c>public static</c> para que <see cref="DetailsModel"/>
    /// pueda reusar la misma normalización al armar el link
    /// "Volver al listado" sin duplicar la lógica.
    /// </remarks>
    public static string? NormalizeSort(string? value)
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
    /// <remarks>
    /// <c>public static</c> para que <see cref="DetailsModel"/>
    /// pueda reusar la misma normalización al armar el link
    /// "Volver al listado" sin duplicar la lógica.
    /// </remarks>
    public static int NormalizePageSize(int value)
    {
        if (value <= 0) return DefaultPageSize;
        return AllowedPageSizes.Contains(value) ? value : DefaultPageSize;
    }
}
