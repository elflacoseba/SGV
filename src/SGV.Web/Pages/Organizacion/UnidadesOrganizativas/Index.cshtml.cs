using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.UnidadesOrganizativas;

/// <summary>
/// PageModel del listado y árbol de unidades organizativas.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre
/// <see cref="ErrorCategoria"/> en OnPostDelete y OnPostReactivate.
/// <c>Unauthorized</c> redirige vía <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class IndexModel(
    IUnidadOrganizativaApiClient unidadOrganizativaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;
    private const string ListView = "list";
    private const string TreeView = "tree";
    private const string DeletedView = "eliminadas";

    public IReadOnlyList<UnidadOrganizativaListItemViewModel> Items { get; private set; } = [];

    public IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> TreeItems { get; private set; } = [];

    public int CurrentPage { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public int TotalCount { get; private set; }

    public string? Search { get; private set; }

    public string? Sort { get; private set; }

    /// <summary>
    /// Fecha de referencia para el filtro de vigencia. Se persiste como
    /// query string <c>vigenteEn</c> en todos los route values generados
    /// por este PageModel (issue #281).
    /// </summary>
    public DateOnly? VigenteEn { get; private set; }

    public string? LoadErrorMessage { get; private set; }

    public string CurrentView { get; private set; } = ListView;

    public bool IsTreeView => string.Equals(CurrentView, TreeView, StringComparison.OrdinalIgnoreCase);

    public string? Segmento { get; private set; }

    public bool IsDeletedView => string.Equals(Segmento, DeletedView, StringComparison.OrdinalIgnoreCase);

    public string? StatusMessage => TempData[nameof(StatusMessage)] as string;

    public string StatusKind => TempData[nameof(StatusKind)] as string ?? "success";

    [TempData]
    public string? TempDataStatusMessage { get; set; }

    [TempData]
    public string? TempDataStatusKind { get; set; }

    public string? LastDeletedId => TempData[nameof(LastDeletedId)] as string;

    public bool HasLastDeleted => !string.IsNullOrWhiteSpace(LastDeletedId);

    public async Task OnGetAsync([FromQuery(Name = "p")] int currentPage = 1, string? search = null, string? sort = null, string? view = null, Guid? deletedId = null, string? status = null, DateOnly? vigenteEn = null, CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);
        CurrentView = NormalizeView(view);
        Segmento = NormalizeSegmento(status);
        VigenteEn = vigenteEn;

        if (deletedId.HasValue)
        {
            TempData[nameof(LastDeletedId)] = deletedId.Value.ToString();
        }

        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, [FromForm(Name = "page")] int currentPage = 1, string? search = null, string? sort = null, string? view = null, string? status = null, DateOnly? vigenteEn = null, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedView = NormalizeView(view);
        var normalizedSegmento = NormalizeSegmento(status);
        currentPage = Math.Max(1, currentPage);

        var result = await unidadOrganizativaApiClient.DeleteAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            var redirectPage = await ResolveRedirectPageAsync(currentPage, normalizedSearch, normalizedSort, vigenteEn, cancellationToken);
            TempData[nameof(StatusMessage)] = "La unidad organizativa se eliminó correctamente.";
            TempData[nameof(StatusKind)] = "success";
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { p = redirectPage, search = normalizedSearch, sort = normalizedSort, view = normalizedView, deletedId = id, status = normalizedSegmento, vigenteEn = FormatVigenteEn(vigenteEn) });
        }

        // Issue #125 / Slice 3: Unauthorized redirige vía IAuthSessionRedirector.
        if (result.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        var message = result.Categoria switch
        {
            ErrorCategoria.Conflict => $"No se pudo eliminar la unidad organizativa. {result.Message}".Trim(),
            ErrorCategoria.NotFound => PageFeedback.NotFoundDeleteMessage,
            ErrorCategoria.Transport => "No se pudo eliminar la unidad organizativa. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo eliminar la unidad organizativa. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(result.Categoria)
        };

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";

        return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, view = normalizedView, status = normalizedSegmento, vigenteEn = FormatVigenteEn(vigenteEn) });
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id, [FromForm(Name = "page")] int currentPage = 1, string? search = null, string? sort = null, string? view = null, string? status = null, DateOnly? vigenteEn = null, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedView = NormalizeView(view);
        var normalizedSegmento = NormalizeSegmento(status);
        currentPage = Math.Max(1, currentPage);

        var result = await unidadOrganizativaApiClient.ReactivateAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            TempData[nameof(StatusMessage)] = "La unidad organizativa se reactivó correctamente.";
            TempData[nameof(StatusKind)] = "success";
            ClearLastDeleted();
            // After success, redirect to activas list
            return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, view = normalizedView, vigenteEn = FormatVigenteEn(vigenteEn) });
        }

        // Issue #125 / Slice 3: Unauthorized redirige vía IAuthSessionRedirector.
        if (result.Error?.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        var categoria = result.Error?.Categoria ?? ErrorCategoria.Unexpected;
        var message = categoria switch
        {
            ErrorCategoria.Conflict => $"No se pudo reactivar la unidad organizativa. {result.Error.Message}",
            ErrorCategoria.NotFound => "La unidad organizativa ya no está disponible para reactivar.",
            ErrorCategoria.Transport => "No se pudo reactivar la unidad organizativa. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo reactivar la unidad organizativa. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(categoria)
        };

        TempData[nameof(StatusMessage)] = message;
        TempData[nameof(StatusKind)] = "danger";

        // After failure, stay in the same segment (eliminadas) so user can retry
        return RedirectToPage("/Organizacion/UnidadesOrganizativas/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, view = normalizedView, status = normalizedSegmento, vigenteEn = FormatVigenteEn(vigenteEn) });
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
    /// Builds query-string parameters for return-to-list links, preserving the current page, search, sort, segmento, and vigencia (issue #281).
    /// </summary>
    public object ReturnToListRouteValues => new
    {
        p = CurrentPage,
        search = Search,
        sort = Sort,
        view = IsTreeView ? CurrentView : null,
        status = IsTreeView ? null : Segmento,
        vigenteEn = FormatVigenteEn(VigenteEn)
    };

    /// <summary>
    /// Builds the return URL for the listado, preserving current page, search, sort, segmento, and vigencia (issue #281).
    /// </summary>
    public string ReturnToListUrl => Url.Page("/Organizacion/UnidadesOrganizativas/Index", ReturnToListRouteValues) ?? "/organizacion/unidades-organizativas";

    public object CreateRouteValues => new
    {
        p = CurrentPage,
        search = Search,
        sort = Sort,
        view = IsTreeView ? CurrentView : null,
        status = IsTreeView ? null : Segmento,
        vigenteEn = FormatVigenteEn(VigenteEn)
    };

    public object BuildDetailsRouteValues(Guid id) => new
    {
        id,
        p = CurrentPage,
        search = Search,
        sort = Sort,
        returnView = IsTreeView ? CurrentView : null,
        returnStatus = IsTreeView ? null : Segmento,
        returnVigenteEn = FormatVigenteEn(VigenteEn)
    };

    public object BuildEditRouteValues(Guid id) => new
    {
        id,
        p = CurrentPage,
        search = Search,
        sort = Sort,
        returnView = IsTreeView ? CurrentView : null,
        returnStatus = IsTreeView ? null : Segmento,
        returnVigenteEn = FormatVigenteEn(VigenteEn)
    };

    public object BuildViewToggleRouteValues(string view) => new
    {
        p = CurrentPage,
        search = Search,
        sort = Sort,
        view = NormalizeView(view),
        status = string.Equals(view, ListView, StringComparison.OrdinalIgnoreCase) ? Segmento : null,
        vigenteEn = FormatVigenteEn(VigenteEn)
    };

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadErrorMessage = null;

        if (IsTreeView)
        {
            await LoadTreeAsync(cancellationToken);
            return;
        }

        await LoadListAsync(cancellationToken);
    }

    private async Task LoadListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await unidadOrganizativaApiClient.QueryAsync(new UnidadOrganizativaListQuery(CurrentPage, DefaultPageSize, Search, Sort, Segmento, VigenteEn), cancellationToken);
            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, result.PageSize)));

            // Issue #282: el backend ya ordena por `Sort` antes de paginar
            // (`UnidadOrganizativaRepository.ApplySort`); confiamos en ese
            // orden. Antes este cliente reordenaba sólo la página recibida,
            // lo que hacía que el icono de flecha mintiera al paginar.
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            Items = result.Items
                .Select(item => MapToViewModel(item, hoy))
                .ToArray();
            TreeItems = [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load unidades organizativas page.");
            Items = [];
            TreeItems = [];
            TotalCount = 0;
            TotalPages = 1;
            LoadErrorMessage = "No se pudo cargar el listado. Intentá nuevamente.";
        }
    }

    private async Task LoadTreeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await unidadOrganizativaApiClient.GetTreeAsync(cancellationToken);
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            TreeItems = result.Arbol.Select(node => MapToTreeViewModel(node, hoy)).ToArray();
            Items = [];
            TotalCount = CountTreeNodes(TreeItems);
            TotalPages = 1;
            CurrentPage = 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load unidades organizativas tree page.");
            TreeItems = [];
            Items = [];
            TotalCount = 0;
            TotalPages = 1;
            CurrentPage = 1;
            LoadErrorMessage = "No se pudo cargar el árbol. Intentá nuevamente o volvé al listado.";
        }
    }

    private async Task<int> ResolveRedirectPageAsync(int currentPage, string? search, string? sort, DateOnly? vigenteEn, CancellationToken cancellationToken)
    {
        if (currentPage <= 1)
        {
            return 1;
        }

        try
        {
            var refreshed = await unidadOrganizativaApiClient.QueryAsync(new UnidadOrganizativaListQuery(currentPage, DefaultPageSize, search, sort, Status: null, VigenteEn: vigenteEn), cancellationToken);
            return refreshed.Items.Count == 0 ? currentPage - 1 : currentPage;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recalculate redirect page after deleting unidad organizativa.");
            return currentPage;
        }
    }

    private void ClearLastDeleted()
    {
        TempData.Remove(nameof(LastDeletedId));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UnidadOrganizativaListItemViewModel MapToViewModel(UnidadOrganizativaDto item, DateOnly hoy)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.TipoUnidadNombre,
            item.Descripcion,
            item.UnidadPadreId,
            VigenciaViewModel.Desde(item.VigenteDesde, item.VigenteHasta, hoy));

    private static UnidadOrganizativaTreeNodeViewModel MapToTreeViewModel(UnidadOrganizativaTreeNodeDto item, DateOnly hoy)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.TipoUnidadNombre,
            VigenciaViewModel.Desde(item.VigenteDesde, item.VigenteHasta, hoy),
            item.VigenteDesde,
            item.VigenteHasta,
            item.Hijas.Select(child => MapToTreeViewModel(child, hoy)).ToArray());

    private static int CountTreeNodes(IReadOnlyList<UnidadOrganizativaTreeNodeViewModel> nodes)
        => nodes.Sum(static node => 1 + CountTreeNodes(node.Children));

    /// <summary>
    /// Serializa <see cref="VigenteEn"/> a <c>yyyy-MM-dd</c> para query
    /// strings y formularios. Devuelve <c>null</c> cuando no hay filtro
    /// (issue #281).
    /// </summary>
    private static string? FormatVigenteEn(DateOnly? vigenteEn)
        => vigenteEn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NormalizeView(string? view)
        => string.Equals(view, TreeView, StringComparison.OrdinalIgnoreCase) ? TreeView : ListView;

    private static string? NormalizeSegmento(string? status)
        => string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null;
}
