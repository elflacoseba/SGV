using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel del listado web de cargos activos. Realiza búsqueda, orden y
/// paginación en memoria sobre el resultado completo de
/// <c>GET /api/v1/cargos</c> y expone la baja lógica confirmada vía
/// <c>?handler=Delete</c>.
/// </summary>
[Authorize]
public sealed class IndexModel(ICargoApiClient cargoApiClient, ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;

    /// <summary>
    /// Filas visibles en la página actual.
    /// </summary>
    public IReadOnlyList<CargoListItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Página actual (1-based).
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// Cantidad total de páginas luego del filtro/sort.
    /// </summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>
    /// Total de cargos activos visibles después del filtro.
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
    /// Mensaje de error visible cuando la carga inicial del listado falla.
    /// </summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>
    /// Mensaje de feedback tras una baja lógica (éxito/conflicto/no disponible).
    /// </summary>
    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    /// <summary>
    /// Tipo de feedback: <c>success</c> o <c>danger</c>.
    /// </summary>
    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    /// <summary>
    /// Handler GET del listado. Carga los cargos activos y aplica
    /// búsqueda, orden y paginación en memoria.
    /// </summary>
    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);

        await LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Handler POST para la baja lógica confirmada. Devuelve al listado
    /// preservando filtros, sort y página. Si la página actual queda vacía
    /// tras el borrado, recalcula la página anterior para evitar una página
    /// huérfana.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(
        Guid id,
        [FromForm(Name = "page")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        currentPage = Math.Max(1, currentPage);

        var result = await cargoApiClient.DeleteAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            var redirectPage = await ResolveRedirectPageAsync(currentPage, normalizedSearch, normalizedSort, cancellationToken);
            TempData[nameof(StatusMessage)] = "El cargo se eliminó correctamente.";
            TempData[nameof(StatusKind)] = "success";

            return RedirectToPage("/Organizacion/Cargos/Index", new { p = redirectPage, search = normalizedSearch, sort = normalizedSort });
        }

        var message = result.StatusCode == System.Net.HttpStatusCode.Conflict
            ? $"No se pudo eliminar el cargo. {result.Message}".Trim()
            : result.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "El cargo ya no está disponible."
                : "No se pudo eliminar el cargo. Intentá nuevamente.";

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";

        return RedirectToPage("/Organizacion/Cargos/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort });
    }

    /// <summary>
    /// Devuelve el valor de orden a aplicar cuando el usuario hace click en
    /// el encabezado de una columna. Alterna asc/desc cuando ya estaba
    /// ordenada por esa columna.
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
    /// Devuelve el ícono (CSS class) correspondiente al orden actual de una
    /// columna, o <c>null</c> si la columna no está ordenada.
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

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadErrorMessage = null;

        try
        {
            var all = await cargoApiClient.GetAllAsync(cancellationToken);
            var filtered = ApplyVisibleFilter(all, Search);
            var ordered = ApplyVisibleSort(filtered, Sort);

            TotalCount = filtered.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)DefaultPageSize));

            // Si la página pedida excede las disponibles, se acota a la última.
            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            Items = ordered
                .Skip((CurrentPage - 1) * DefaultPageSize)
                .Take(DefaultPageSize)
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

    private async Task<int> ResolveRedirectPageAsync(int currentPage, string? search, string? sort, CancellationToken cancellationToken)
    {
        if (currentPage <= 1)
        {
            return 1;
        }

        try
        {
            var all = await cargoApiClient.GetAllAsync(cancellationToken);
            var filtered = ApplyVisibleFilter(all, search);
            var ordered = ApplyVisibleSort(filtered, sort);
            var pageCount = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)DefaultPageSize));
            var safePage = Math.Min(currentPage, pageCount);

            var hasItemsOnPage = ordered
                .Skip((safePage - 1) * DefaultPageSize)
                .Take(DefaultPageSize)
                .Any();

            return hasItemsOnPage ? safePage : Math.Max(1, safePage - 1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recalculate redirect page after deleting cargo.");
            return currentPage;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<CargoDto> ApplyVisibleFilter(IReadOnlyList<CargoDto> items, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return items;
        }

        return items
            .Where(item => Matches(item.Codigo, search)
                || Matches(item.Nombre, search)
                || Matches(item.Descripcion, search)
                || Matches(item.NivelNombre, search))
            .ToArray();
    }

    private static bool Matches(string? value, string search)
        => !string.IsNullOrEmpty(value)
            && value!.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;

    private static IReadOnlyList<CargoDto> ApplyVisibleSort(IReadOnlyList<CargoDto> items, string? sort)
    {
        IEnumerable<CargoDto> ordered = sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => items.OrderByDescending(static item => item.Codigo, StringComparer.CurrentCultureIgnoreCase),
            "codigo_asc" => items.OrderBy(static item => item.Codigo, StringComparer.CurrentCultureIgnoreCase),
            "nombre_desc" => items.OrderByDescending(static item => item.Nombre, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
            "nombre_asc" => items.OrderBy(static item => item.Nombre, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
            "nivel_desc" => items.OrderByDescending(static item => item.NivelNombre ?? string.Empty, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
            "nivel_asc" => items.OrderBy(static item => item.NivelNombre ?? string.Empty, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
            _ => items.AsEnumerable()
        };

        return ordered.ToArray();
    }

    private static CargoListItemViewModel MapToViewModel(CargoDto item)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.Descripcion,
            item.NivelNombre);
}