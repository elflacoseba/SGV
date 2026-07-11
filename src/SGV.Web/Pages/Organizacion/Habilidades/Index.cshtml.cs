using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Web.Integration.Habilidades;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;

namespace SGV.Web.Pages.Organizacion.Habilidades;

/// <summary>
/// PageModel del listado web de habilidades. Usa consulta server-side
/// segmentada (<c>QueryAsync</c>) hacia <c>GET /api/v1/skills/consulta</c>
/// y soporta alternar entre <c>activas</c> y <c>eliminadas</c>. Mantiene
/// la baja lógica (<c>?handler=Delete</c>) y agrega reactivación
/// (<c>?handler=Reactivate</c>) preservando el segmento cuando la
/// operación falla.
/// </summary>
[Authorize]
public sealed class IndexModel(IHabilidadApiClient habilidadApiClient, ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;
    private const string DeletedView = "eliminadas";

    /// <summary>
    /// Filas visibles en la página actual.
    /// </summary>
    public IReadOnlyList<HabilidadListItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Página actual (1-based).
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Cantidad total de páginas calculadas a partir del backend segmentado.
    /// </summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>
    /// Total de habilidades que matchean el segmento y filtros vigentes.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Término de búsqueda normalizado.
    /// </summary>
    public string? Search { get; private set; }

    /// <summary>
    /// Expresión de orden actual (e.g. <c>nombre_asc</c>).
    /// </summary>
    public string? Sort { get; private set; }

    /// <summary>
    /// Segmento vigente del listado: <c>null</c> para activas,
    /// <c>"eliminadas"</c> para eliminadas.
    /// </summary>
    public string? Segmento { get; private set; }

    /// <summary>
    /// <c>true</c> cuando el segmento vigente es <c>eliminadas</c>.
    /// </summary>
    public bool IsDeletedView =>
        string.Equals(Segmento, DeletedView, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Mensaje de error visible cuando la carga inicial del listado falla.
    /// </summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>
    /// Mensaje de feedback tras una operación (baja lógica, reactivación).
    /// </summary>
    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    /// <summary>
    /// Tipo de feedback: <c>success</c> o <c>danger</c>.
    /// </summary>
    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    /// <summary>
    /// Identificador de la última habilidad eliminada, persistido en TempData
    /// durante el PRG desde <see cref="OnPostDeleteAsync"/>. El valor se
    /// limpia tras una reactivación exitosa. Es <c>null</c> cuando no hay
    /// una última baja pendiente de reactivación rápida.
    /// </summary>
    public Guid? LastDeletedId { get; private set; }

    /// <summary>
    /// <c>true</c> cuando hay un <see cref="LastDeletedId"/> pendiente de
    /// reactivar desde el banner. El CTA solo se muestra cuando el
    /// segmento vigente es Activas.
    /// </summary>
    public bool HasLastDeleted => LastDeletedId.HasValue;

    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        Guid? deletedId = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);
        Segmento = NormalizeSegmento(status);

        if (deletedId.HasValue)
        {
            TempData[nameof(LastDeletedId)] = deletedId.Value.ToString();
        }

        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        Guid id,
        [FromForm(Name = "page")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedSegmento = NormalizeSegmento(status);
        currentPage = Math.Max(1, currentPage);

        var result = await habilidadApiClient.DeleteAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            // Si la baja dejó vacía la página vigente, retrocedemos una para
            // evitar que el PRG caiga en una página sin filas. Espejo del
            // helper que PR #71 añadió al Index de Cargos.
            var redirectPage = await ResolveRedirectPageAsync(currentPage, normalizedSearch, normalizedSort, normalizedSegmento, cancellationToken);
            TempData[nameof(StatusMessage)] = "La habilidad se eliminó correctamente.";
            TempData[nameof(StatusKind)] = "success";

            return RedirectToPage("/Organizacion/Habilidades/Index", new { p = redirectPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento, deletedId = id });
        }

        var message = result.StatusCode == System.Net.HttpStatusCode.Conflict
            ? $"No se pudo eliminar la habilidad. {result.Message}".Trim()
            : result.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "La habilidad ya no está disponible."
                : "No se pudo eliminar la habilidad. Intentá nuevamente.";

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";

        return RedirectToPage("/Organizacion/Habilidades/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
    }

    public async Task<IActionResult> OnPostReactivateAsync(
        Guid id,
        [FromForm(Name = "page")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedSegmento = NormalizeSegmento(status);
        currentPage = Math.Max(1, currentPage);

        var result = await habilidadApiClient.ReactivarAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[nameof(StatusMessage)] = "La habilidad se reactivó correctamente.";
            TempData[nameof(StatusKind)] = "success";

            ClearLastDeleted();

            return RedirectToPage("/Organizacion/Habilidades/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort });
        }

        var errorCode = result.Error?.Code;
        var errorMessage = result.Error?.Message;
        var message = result.Error?.Type switch
        {
            SGV.Contracts.Habilidades.Comandos.HabilidadErrorType.Conflict => $"No se pudo reactivar la habilidad. {errorMessage}",
            SGV.Contracts.Habilidades.Comandos.HabilidadErrorType.NotFound => "La habilidad ya no está disponible para reactivar.",
            _ => "No se pudo reactivar la habilidad. Intentá nuevamente."
        };

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            TempData["ErrorCode"] = errorCode;
        }

        return RedirectToPage("/Organizacion/Habilidades/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
    }

    public string GetSortRoute(string column)
    {
        var isSameColumn = Sort?.StartsWith(column, StringComparison.OrdinalIgnoreCase) == true;
        var isDesc = Sort?.EndsWith("_desc", StringComparison.OrdinalIgnoreCase) == true;

        return isSameColumn && !isDesc
            ? $"{column}_desc"
            : $"{column}_asc";
    }

    public string? GetSortIcon(string column)
    {
        if (Sort is null) return null;

        var isSameColumn = Sort.StartsWith(column, StringComparison.OrdinalIgnoreCase);
        if (!isSameColumn) return null;

        return Sort.EndsWith("_desc", StringComparison.OrdinalIgnoreCase)
            ? "ti ti-arrow-down"
            : "ti ti-arrow-up";
    }

    public object BuildEditRouteValues(Guid id) => new
    {
        id,
        p = CurrentPage,
        search = Search,
        sort = Sort,
        returnStatus = Segmento
    };

    public object BuildDetailsRouteValues(Guid id) => new
    {
        id,
        p = CurrentPage,
        search = Search,
        sort = Sort,
        returnStatus = Segmento
    };

    public object BuildToggleSegmentoRouteValues(string? targetSegmento) => new
    {
        p = 1,
        search = Search,
        sort = Sort,
        status = string.Equals(targetSegmento, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null
    };

    /// <summary>
    /// Construye los route values para el botón "Cargos" que navega a
    /// <c>Pages/Organizacion/Habilidades/Cargos.cshtml</c>. Preserva
    /// <c>p</c>, <c>search</c>, <c>sort</c> y <c>status</c> para que el
    /// usuario pueda volver al listado con el mismo contexto que tenía al
    /// hacer click. El botón solo se renderiza en filas activas (ver
    /// <see cref="IsDeletedView"/>), así que la "vista eliminadas" MUST
    /// NOT exponer este enlace (espejo del comportamiento ya fijado por
    /// <c>Cargos/Index</c> con su botón "Habilidades").
    /// </summary>
    /// <remarks>
    /// PR #88 (review 🟡6): este helper retorna <see cref="RouteValueDictionary"/>
    /// explícitamente (en lugar de un anonymous object como hacen los
    /// demás helpers de este archivo) para fijar el orden de las claves
    /// y, sobre todo, para que <c>Segmento</c> pueda ser <c>null</c> y
    /// ASP.NET Core OMITA la query string <c>?status=</c> en vista
    /// activas. Con un anonymous object, un valor <c>null</c> se
    /// serializa como <c>?status=</c> en algunas rutas, rompiendo la
    /// convención del módulo (en activas el status NO viaja en la URL).
    /// </remarks>
    public RouteValueDictionary BuildCargosRouteValues(Guid id) => new()
    {
        ["id"] = id,
        ["p"] = CurrentPage,
        ["search"] = Search,
        ["sort"] = Sort,
        ["status"] = Segmento,
    };

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadErrorMessage = null;

        var rawLastDeleted = TempData[nameof(LastDeletedId)] as string;
        if (Guid.TryParse(rawLastDeleted, out var parsedDeleted))
        {
            LastDeletedId = parsedDeleted;
        }
        else
        {
            LastDeletedId = null;
        }

        try
        {
            var result = await habilidadApiClient.QueryAsync(
                new HabilidadListQuery(CurrentPage, DefaultPageSize, Search, Sort, Segmento),
                cancellationToken);

            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, result.PageSize)));

            Items = result.Items
                .Select(MapToViewModel)
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load habilidades page.");
            Items = [];
            TotalCount = 0;
            TotalPages = 1;
            CurrentPage = 1;
            LoadErrorMessage = "No se pudo cargar el listado de habilidades. Intentá nuevamente.";
        }
    }

    private void ClearLastDeleted()
    {
        TempData.Remove(nameof(LastDeletedId));
        LastDeletedId = null;
    }

    /// <summary>
    /// Tras una baja lógica puede ocurrir que la página vigente quede vacía.
    /// En ese caso retrocedemos una sola posición. Sin un endpoint de TotalCount
    /// sin paginar, recalculamos consultando la misma página: si quedó sin
    /// filas, devolvemos <c>currentPage - 1</c>; en cualquier excepción
    /// caemos al <c>currentPage</c> original para no bloquear el flujo.
    /// </summary>
    private async Task<int> ResolveRedirectPageAsync(
        int currentPage,
        string? search,
        string? sort,
        string? segmento,
        CancellationToken cancellationToken)
    {
        if (currentPage <= 1)
        {
            return 1;
        }

        try
        {
            var refreshed = await habilidadApiClient.QueryAsync(
                new HabilidadListQuery(currentPage, DefaultPageSize, search, sort, segmento),
                cancellationToken);
            return refreshed.Items.Count == 0 ? currentPage - 1 : currentPage;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recalculate redirect page after deleting habilidad.");
            return currentPage;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSegmento(string? status)
        => string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null;

    private static HabilidadListItemViewModel MapToViewModel(HabilidadDto item)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.Descripcion,
            item.Categoria);
}