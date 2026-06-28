using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

[Authorize]
public sealed class IndexModel(IUnidadOrganizativaApiClient unidadOrganizativaApiClient, ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;

    public IReadOnlyList<UnidadOrganizativaListItemViewModel> Items { get; private set; } = [];

    public int CurrentPage { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public int TotalCount { get; private set; }

    public string? Search { get; private set; }

    public string? Sort { get; private set; }

    public string? LoadErrorMessage { get; private set; }

    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    [TempData]
    public string? TempDataStatusMessage { get; set; }

    [TempData]
    public string? TempDataStatusKind { get; set; }

    public async Task OnGetAsync([FromQuery(Name = "page")] int currentPage = 1, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);

        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, [FromForm(Name = "page")] int currentPage = 1, string? search = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        currentPage = Math.Max(1, currentPage);

        var result = await unidadOrganizativaApiClient.DeleteAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            var redirectPage = await ResolveRedirectPageAsync(currentPage, normalizedSearch, normalizedSort, cancellationToken);
            TempData[nameof(StatusMessage)] = "La unidad organizativa se eliminó correctamente.";
            TempData[nameof(StatusKind)] = "success";
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { page = redirectPage, search = normalizedSearch, sort = normalizedSort });
        }

        var message = result.StatusCode == System.Net.HttpStatusCode.Conflict
            ? $"No se pudo eliminar la unidad organizativa. {result.Message}".Trim()
            : result.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "La unidad organizativa ya no está disponible."
                : "No se pudo eliminar la unidad organizativa. Intentá nuevamente.";

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";

        return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { page = currentPage, search = normalizedSearch, sort = normalizedSort });
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
    /// Builds query-string parameters for return-to-list links, preserving the current page, search, and sort.
    /// </summary>
    public object ReturnToListRouteValues => new
    {
        page = CurrentPage,
        search = Search,
        sort = Sort
    };

    /// <summary>
    /// Builds the return URL for the listado, preserving current page, search, and sort.
    /// </summary>
    public string ReturnToListUrl => Url.Page("/Organizacion/UnidadesOrganizativas/Index", ReturnToListRouteValues);

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await unidadOrganizativaApiClient.QueryAsync(new UnidadOrganizativaListQuery(CurrentPage, DefaultPageSize, Search, Sort), cancellationToken);
            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, result.PageSize)));

            Items = ApplyVisibleSort(result.Items, Sort)
                .Select(MapToViewModel)
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load unidades organizativas page.");
            Items = [];
            TotalCount = 0;
            TotalPages = 1;
            LoadErrorMessage = "No se pudo cargar el listado. Intentá nuevamente.";
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
            var refreshed = await unidadOrganizativaApiClient.QueryAsync(new UnidadOrganizativaListQuery(currentPage, DefaultPageSize, search, sort), cancellationToken);
            return refreshed.Items.Count == 0 ? currentPage - 1 : currentPage;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recalculate redirect page after deleting unidad organizativa.");
            return currentPage;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<UnidadOrganizativaDto> ApplyVisibleSort(IReadOnlyList<UnidadOrganizativaDto> items, string? sort)
    {
        IEnumerable<UnidadOrganizativaDto> ordered = sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => items.OrderByDescending(static item => item.Codigo, StringComparer.CurrentCultureIgnoreCase),
            "codigo_asc" => items.OrderBy(static item => item.Codigo, StringComparer.CurrentCultureIgnoreCase),
            "nombre_desc" => items.OrderByDescending(static item => item.Nombre, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
            "nombre_asc" => items.OrderBy(static item => item.Nombre, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true)),
            "tipo_desc" => items.OrderByDescending(static item => item.TipoUnidadNombre, StringComparer.CurrentCultureIgnoreCase),
            "tipo_asc" => items.OrderBy(static item => item.TipoUnidadNombre, StringComparer.CurrentCultureIgnoreCase),
            _ => items.AsEnumerable()
        };

        return ordered.ToArray();
    }

    private static UnidadOrganizativaListItemViewModel MapToViewModel(UnidadOrganizativaDto item)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.TipoUnidadNombre,
            item.Descripcion,
            item.UnidadPadreId,
            BuildVigencia(item.VigenteDesde, item.VigenteHasta));

    private static string BuildVigencia(DateOnly? vigenteDesde, DateOnly? vigenteHasta)
    {
        if (vigenteDesde is null && vigenteHasta is null)
        {
            return "Vigencia abierta";
        }

        if (vigenteDesde is not null && vigenteHasta is null)
        {
            return $"Desde {vigenteDesde:dd/MM/yyyy}";
        }

        if (vigenteDesde is null)
        {
            return $"Hasta {vigenteHasta:dd/MM/yyyy}";
        }

        return $"{vigenteDesde:dd/MM/yyyy} - {vigenteHasta:dd/MM/yyyy}";
    }
}
