using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Usuarios;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Seguridad.Usuarios;

/// <summary>
/// Paginated and segmented Usuarios listing. Reads are available to every
/// authenticated shell user; lifecycle writes require Administrador.
/// </summary>
[Authorize]
public sealed class IndexModel(
    IUsuarioApiClient usuarioApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;
    private const string ActiveView = "activas";
    private const string DeletedView = "eliminadas";

    public IReadOnlyList<UsuarioListItemViewModel> Items { get; private set; } = [];

    public int CurrentPage { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public int TotalCount { get; private set; }

    public string? Search { get; private set; }

    public string? Sort { get; private set; }

    public string Segmento { get; private set; } = ActiveView;

    public bool IsDeletedView =>
        string.Equals(Segmento, DeletedView, StringComparison.OrdinalIgnoreCase);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    public string? LoadErrorMessage { get; private set; }

    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    public string? LastDeletedId { get; private set; }

    public bool HasLastDeleted => !string.IsNullOrWhiteSpace(LastDeletedId);

    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        string? deletedId = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        Search = Normalize(search);
        Sort = Normalize(sort);
        Segmento = NormalizeSegmento(status);

        if (!string.IsNullOrWhiteSpace(deletedId))
        {
            StoreLastDeletedId(deletedId);
        }

        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        string id,
        [FromForm(Name = "page")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        var context = NormalizeContext(currentPage, search, sort, status);
        UsuarioCommandResult result;

        try
        {
            result = await usuarioApiClient.DesactivarAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogWarning(ex, "Failed to deactivate usuario with Id {UsuarioId}.", id);
            PageFeedback.SetDanger(TempData, "No se pudo eliminar el usuario. Intentá nuevamente.");
            return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
        }

        if (result.IsSuccess)
        {
            var redirectPage = await ResolveRedirectPageAsync(
                context.Page,
                context.Search,
                context.Sort,
                ActiveView,
                cancellationToken);

            PageFeedback.SetSuccess(TempData, "El usuario se eliminó correctamente.");
            StoreLastDeletedId(id);

            return RedirectToPage("/Seguridad/Usuarios/Index", new
            {
                p = redirectPage,
                search = context.Search,
                sort = context.Sort,
                status = ActiveView,
                deletedId = id
            });
        }

        if (result.Error?.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        var error = result.Error;
        var categoria = error?.Categoria ?? ErrorCategoria.Unexpected;
        var message = categoria switch
        {
            ErrorCategoria.Forbidden when string.Equals(error?.Code, "AutoBaja", StringComparison.Ordinal) =>
                error?.Message ?? "No se puede dar de baja el usuario actual.",
            ErrorCategoria.Conflict => $"No se pudo eliminar el usuario. {error?.Message}".Trim(),
            ErrorCategoria.NotFound => "El usuario ya no está disponible.",
            ErrorCategoria.Transport => "No se pudo eliminar el usuario. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo eliminar el usuario. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(categoria)
        };

        SetFailureFeedback(message, error?.Code);
        return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
    }

    public async Task<IActionResult> OnPostReactivateAsync(
        string id,
        [FromForm(Name = "page")] int currentPage = 1,
        string? search = null,
        string? sort = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!EsAdministrador)
        {
            return Forbid();
        }

        var context = NormalizeContext(currentPage, search, sort, status);
        UsuarioCommandResult result;

        try
        {
            result = await usuarioApiClient.ReactivarAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogWarning(ex, "Failed to reactivate usuario with Id {UsuarioId}.", id);
            PageFeedback.SetDanger(TempData, "No se pudo reactivar el usuario. Intentá nuevamente.");
            return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "El usuario se reactivó correctamente.");
            PageFeedback.ClearLastDeletedId(TempData);

            return RedirectToIndex(context.Page, context.Search, context.Sort, ActiveView);
        }

        if (result.Error?.Categoria == ErrorCategoria.Unauthorized)
        {
            var redirect = authRedirector.TryRedirectToLogin(Request.Path);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        var error = result.Error;
        var categoria = error?.Categoria ?? ErrorCategoria.Unexpected;
        var message = categoria switch
        {
            ErrorCategoria.Conflict when string.Equals(error?.Code, "PersonaInactiva", StringComparison.Ordinal) =>
                $"No se pudo reactivar el usuario. {error?.Message ?? "La persona vinculada está inactiva."}",
            ErrorCategoria.Conflict => $"No se pudo reactivar el usuario. {error?.Message}".Trim(),
            ErrorCategoria.NotFound => "El usuario ya no está disponible para reactivar.",
            ErrorCategoria.Transport => "No se pudo reactivar el usuario. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo reactivar el usuario. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(categoria)
        };

        SetFailureFeedback(message, error?.Code);
        return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
    }

    public string GetSortRoute(string column)
    {
        var isSameColumn = Sort?.StartsWith(column, StringComparison.OrdinalIgnoreCase) == true;
        var isDescending = Sort?.EndsWith("_desc", StringComparison.OrdinalIgnoreCase) == true;

        return isSameColumn && !isDescending
            ? $"{column}_desc"
            : $"{column}_asc";
    }

    public string? GetSortIcon(string column)
    {
        if (Sort?.StartsWith(column, StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        return Sort.EndsWith("_desc", StringComparison.OrdinalIgnoreCase)
            ? "ti ti-arrow-down"
            : "ti ti-arrow-up";
    }

    public object BuildToggleSegmentoRouteValues(string targetSegmento) => new
    {
        p = 1,
        search = Search,
        sort = Sort,
        status = NormalizeSegmento(targetSegmento)
    };

    public string BuildDetailsUrl(string id) =>
        BuildContextUrl($"/seguridad/usuarios/detalle/{Uri.EscapeDataString(id)}", "returnStatus");

    public string BuildEditUrl(string id) =>
        BuildContextUrl($"/seguridad/usuarios/editar/{Uri.EscapeDataString(id)}", "returnStatus");

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadErrorMessage = null;
        LastDeletedId = TempData[PageFeedback.LastDeletedIdKey] as string;

        try
        {
            var segmento = IsDeletedView
                ? UsuarioSegmentoListado.Bloqueadas
                : UsuarioSegmentoListado.Activas;
            var response = await usuarioApiClient.QueryAsync(
                new UsuarioListQuery(CurrentPage, DefaultPageSize, Search, Sort, segmento),
                cancellationToken);
            var result = response.Result;

            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(
                TotalCount / (double)Math.Max(1, result.PageSize)));
            Items = result.Items.Select(MapToViewModel).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load usuarios page.");
            Items = [];
            CurrentPage = 1;
            TotalCount = 0;
            TotalPages = 1;
            LoadErrorMessage = "No se pudo cargar el listado de usuarios. Intentá nuevamente.";
        }
    }

    private async Task<int> ResolveRedirectPageAsync(
        int currentPage,
        string? search,
        string? sort,
        string status,
        CancellationToken cancellationToken)
    {
        if (currentPage <= 1)
        {
            return 1;
        }

        try
        {
            var segmento = string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase)
                ? UsuarioSegmentoListado.Bloqueadas
                : UsuarioSegmentoListado.Activas;
            var refreshed = await usuarioApiClient.QueryAsync(
                new UsuarioListQuery(currentPage, DefaultPageSize, search, sort, segmento),
                cancellationToken);

            return refreshed.Result.Items.Count == 0 ? currentPage - 1 : currentPage;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogWarning(ex, "Failed to recalculate redirect page after deleting usuario.");
            return currentPage;
        }
    }

    private IActionResult RedirectToIndex(int page, string? search, string? sort, string status) =>
        RedirectToPage("/Seguridad/Usuarios/Index", new { p = page, search, sort, status });

    private void SetFailureFeedback(string message, string? errorCode)
    {
        PageFeedback.SetDanger(TempData, message);
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            TempData["ErrorCode"] = errorCode;
        }
    }

    private void StoreLastDeletedId(string id) =>
        TempData[PageFeedback.LastDeletedIdKey] = id;

    private string BuildContextUrl(string basePath, string statusKey)
    {
        var values = new List<string>();
        if (CurrentPage > 1)
        {
            values.Add($"p={CurrentPage}");
        }
        if (!string.IsNullOrWhiteSpace(Search))
        {
            values.Add($"search={Uri.EscapeDataString(Search)}");
        }
        if (!string.IsNullOrWhiteSpace(Sort))
        {
            values.Add($"sort={Uri.EscapeDataString(Sort)}");
        }
        values.Add($"{statusKey}={Uri.EscapeDataString(Segmento)}");

        return $"{basePath}?{string.Join("&", values)}";
    }

    private static (int Page, string? Search, string? Sort, string Status) NormalizeContext(
        int currentPage,
        string? search,
        string? sort,
        string? status)
        => (Math.Max(1, currentPage), Normalize(search), Normalize(sort), NormalizeSegmento(status));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeSegmento(string? status) =>
        string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase)
            ? DeletedView
            : ActiveView;

    private static UsuarioListItemViewModel MapToViewModel(UsuarioDto item) => new(
        item.Id,
        item.UserName,
        item.Email,
        item.Nombres,
        item.Apellidos,
        item.Roles.ToArray(),
        item.PersonaId);
}
