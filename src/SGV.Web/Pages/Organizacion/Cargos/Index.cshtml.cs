using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel del listado web de cargos. Usa consulta server-side segmentada
/// (<c>QueryAsync</c>) hacia <c>GET /api/v1/cargos/consulta</c> y soporta
/// alternar entre <c>activas</c> y <c>eliminadas</c>. Mantiene la baja lógica
/// (<c>?handler=Delete</c>) y agrega reactivación (<c>?handler=Reactivate</c>)
/// preservando el segmento cuando la operación falla.
/// </summary>
[Authorize]
public sealed class IndexModel(ICargoApiClient cargoApiClient, ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;
    private const string DeletedView = "eliminadas";

    /// <summary>
    /// Filas visibles en la página actual.
    /// </summary>
    public IReadOnlyList<CargoListItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Página actual (1-based).
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Cantidad total de páginas calculadas a partir del backend segmentado.
    /// </summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>
    /// Total de cargos que matchean el segmento y filtros vigentes.
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
    /// <c>true</c> cuando el segmento actual es <c>eliminadas</c> y la página
    /// expone la acción de reactivación por fila.
    /// </summary>
    public bool HasLastDeleted => false;

    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);
        Segmento = NormalizeSegmento(status);

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

        var result = await cargoApiClient.DeleteAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            var redirectPage = await ResolveRedirectPageAsync(currentPage, normalizedSearch, normalizedSort, normalizedSegmento, cancellationToken);
            TempData[nameof(StatusMessage)] = "El cargo se eliminó correctamente.";
            TempData[nameof(StatusKind)] = "success";

            return RedirectToPage("/Organizacion/Cargos/Index", new { p = redirectPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
        }

        var message = result.StatusCode == System.Net.HttpStatusCode.Conflict
            ? $"No se pudo eliminar el cargo. {result.Message}".Trim()
            : result.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "El cargo ya no está disponible."
                : "No se pudo eliminar el cargo. Intentá nuevamente.";

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";

        return RedirectToPage("/Organizacion/Cargos/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
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

        var result = await cargoApiClient.ReactivateAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[nameof(StatusMessage)] = "El cargo se reactivó correctamente.";
            TempData[nameof(StatusKind)] = "success";

            // Tras éxito, redirigir a la vista Activas sin status=eliminadas.
            return RedirectToPage("/Organizacion/Cargos/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort });
        }

        var errorCode = result.Error?.Code;
        var errorMessage = result.Error?.Message;
        var message = result.Error?.Type switch
        {
            CargoErrorType.Conflict => $"No se pudo reactivar el cargo. {errorMessage}",
            CargoErrorType.NotFound => "El cargo ya no está disponible para reactivar.",
            _ => "No se pudo reactivar el cargo. Intentá nuevamente."
        };

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            TempData["ErrorCode"] = errorCode;
        }

        // Tras fallo, permanecer en la vista Eliminadas para permitir reintento.
        return RedirectToPage("/Organizacion/Cargos/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
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

    /// <summary>
    /// Construye los route values del enlace "Editar" preservando el contexto
    /// del listado (página, búsqueda, orden y segmento) para que la página de
    /// edición pueda devolver al usuario a la misma vista.
    /// </summary>
    public object BuildEditRouteValues(Guid id) => new
    {
        id,
        p = CurrentPage,
        search = Search,
        sort = Sort,
        returnStatus = Segmento
    };

    /// <summary>
    /// Construye los route values del enlace "Detalle" preservando el contexto.
    /// </summary>
    public object BuildDetailsRouteValues(Guid id) => new
    {
        id,
        p = CurrentPage,
        search = Search,
        sort = Sort,
        returnStatus = Segmento
    };

    /// <summary>
    /// Construye los route values del toggle Activas/Eliminadas con reset
    /// de página y preservación de búsqueda y orden.
    /// </summary>
    public object BuildToggleSegmentoRouteValues(string? targetSegmento) => new
    {
        p = 1,
        search = Search,
        sort = Sort,
        status = string.Equals(targetSegmento, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null
    };

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadErrorMessage = null;

        try
        {
            // El sort viaja al backend (REQ-CM-01): el repositorio aplica el
            // orden ANTES del Skip/Take, por lo que NO reordenamos en memoria
            // (eso solo ordena la página recibida y rompe la consistencia
            // entre páginas). El backend garantiza la página correcta.
            var result = await cargoApiClient.QueryAsync(
                new CargoListQuery(CurrentPage, DefaultPageSize, Search, Sort, Segmento),
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
            logger.LogError(ex, "Failed to load cargos page.");
            Items = [];
            TotalCount = 0;
            TotalPages = 1;
            CurrentPage = 1;
            LoadErrorMessage = "No se pudo cargar el listado de cargos. Intentá nuevamente.";
        }
    }

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
            // Sin un endpoint de TotalCount sin paginar, consultamos la página
            // vigente. Si quedó vacía, retrocedemos una página.
            var refreshed = await cargoApiClient.QueryAsync(
                new CargoListQuery(currentPage, DefaultPageSize, search, sort, segmento),
                cancellationToken);
            return refreshed.Items.Count == 0 ? currentPage - 1 : currentPage;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recalculate redirect page after deleting cargo.");
            return currentPage;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSegmento(string? status)
        => string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null;

    private static CargoListItemViewModel MapToViewModel(CargoDto item)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.Descripcion,
            item.NivelNombre);
}