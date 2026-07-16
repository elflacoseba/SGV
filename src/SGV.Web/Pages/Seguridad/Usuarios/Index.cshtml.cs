using System.Security.Claims;
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
/// authenticated shell user; lifecycle writes (Bloquear / Desbloquear /
/// Eliminar) require Administrador.
/// </summary>
/// <remarks>
/// <para>
/// Phase 3 del change <c>2026-07-15-quita-soft-delete-usuario</c>: el
/// ciclo de baja lógica (Desactivar/Reactivar) se reemplazó por el ciclo
/// de lockout nativo de Identity. La Razor Page expone
/// <c>?handler=Bloquear</c>, <c>?handler=Desbloquear</c> y
/// <c>?handler=Delete</c>; este último invoca hard-delete físico.
/// </para>
/// <para>
/// El segmento <c>activas</c> muestra acciones Bloquear + Eliminar; el
/// segmento <c>bloqueadas</c> muestra sólo Desbloquear. El PageModel
/// aplica auto-fence contra auto-bloqueo / auto-eliminación comparando
/// el <c>id</c> del form contra el <see cref="ClaimTypes.NameIdentifier"/>
/// del usuario autenticado (claim sembrado por
/// <c>AuthSessionFactory</c> en el login). El render de la vista repite
/// el guard para mantener UX coherente con el server-side.
/// </para>
/// </remarks>
[Authorize]
public sealed class IndexModel(
    IUsuarioApiClient usuarioApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;
    private const string ActiveView = "activas";
    private const string BlockedView = "bloqueadas";

    public IReadOnlyList<UsuarioListItemViewModel> Items { get; private set; } = [];

    public int CurrentPage { get; private set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public int TotalCount { get; private set; }

    public string? Search { get; private set; }

    public string? Sort { get; private set; }

    public string Segmento { get; private set; } = ActiveView;

    public bool IsBlockedView =>
        string.Equals(Segmento, BlockedView, StringComparison.OrdinalIgnoreCase);

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

    /// <summary>
    /// Identificador del admin actualmente autenticado (claim
    /// <see cref="ClaimTypes.NameIdentifier"/>). <c>null</c> si la
    /// request no está autenticada (la Page requiere
    /// <see cref="AuthorizeAttribute"/> así que este caso no debería
    /// ocurrir en producción).
    /// </summary>
    public string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? LoadErrorMessage { get; private set; }

    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>
    /// Helper que la vista usa para decidir si debe renderizar el form
    /// de Bloquear / Eliminar / Desbloquear sobre la fila del usuario
    /// con identificador <paramref name="targetUserId"/>. Devuelve
    /// <see langword="true"/> cuando el target es el admin actual; las
    /// acciones de Bloquear y Eliminar deben ocultarse para impedir el
    /// clic que terminaría en un 403 AutoBloqueo / AutoEliminacion.
    /// </summary>
    public bool EsAutoAccion(string targetUserId) =>
        !string.IsNullOrEmpty(CurrentUserId)
        && string.Equals(CurrentUserId, targetUserId, StringComparison.Ordinal);

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

        if (EsAutoAccion(id))
        {
            // Auto-fence: el server repite el guard aunque la vista
            // oculte los botones; defensa en profundidad por si el form
            // se construye fuera del flujo de render normal.
            SetFailureFeedback("No puede eliminar su propio usuario.", "AutoEliminacion");
            return RedirectToIndex(currentPage, search, sort, status);
        }

        var context = NormalizeContext(currentPage, search, sort, status);
        UsuarioCommandResult result;

        try
        {
            result = await usuarioApiClient.EliminarAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogWarning(ex, "Failed to delete usuario with Id {UsuarioId}.", id);
            PageFeedback.SetDanger(TempData, "No se pudo eliminar el usuario. Intentá nuevamente.");
            return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "El usuario se eliminó correctamente.");
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
            ErrorCategoria.Forbidden when string.Equals(error?.Code, "AutoEliminacion", StringComparison.Ordinal) =>
                error?.Message ?? "No puede eliminar su propio usuario.",
            ErrorCategoria.Conflict => $"No se pudo eliminar el usuario. {error?.Message}".Trim(),
            ErrorCategoria.NotFound => "El usuario ya no está disponible.",
            ErrorCategoria.Transport => "No se pudo eliminar el usuario. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo eliminar el usuario. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(categoria)
        };

        SetFailureFeedback(message, error?.Code);
        return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
    }

    public async Task<IActionResult> OnPostBloquearAsync(
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

        if (EsAutoAccion(id))
        {
            SetFailureFeedback("No puede bloquear su propio usuario.", "AutoBloqueo");
            return RedirectToIndex(currentPage, search, sort, status);
        }

        var context = NormalizeContext(currentPage, search, sort, status);
        UsuarioCommandResult result;

        try
        {
            result = await usuarioApiClient.BloquearAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogWarning(ex, "Failed to block usuario with Id {UsuarioId}.", id);
            PageFeedback.SetDanger(TempData, "No se pudo bloquear el usuario. Intentá nuevamente.");
            return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "El usuario se bloqueó correctamente.");
            // Redirigimos al segmento bloqueadas para que el admin vea
            // inmediatamente el cambio (UX feedback loop).
            return RedirectToIndex(context.Page, context.Search, context.Sort, BlockedView);
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
            ErrorCategoria.Forbidden when string.Equals(error?.Code, "AutoBloqueo", StringComparison.Ordinal) =>
                error?.Message ?? "No puede bloquear su propio usuario.",
            ErrorCategoria.NotFound => "El usuario ya no está disponible.",
            ErrorCategoria.Transport => "No se pudo bloquear el usuario. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo bloquear el usuario. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(categoria)
        };

        SetFailureFeedback(message, error?.Code);
        return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
    }

    public async Task<IActionResult> OnPostDesbloquearAsync(
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
            result = await usuarioApiClient.DesbloquearAsync(id, cancellationToken);
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // CodeQL [SM02379]: structured logging placeholder, not interpolated.
            logger.LogWarning(ex, "Failed to unblock usuario with Id {UsuarioId}.", id);
            PageFeedback.SetDanger(TempData, "No se pudo desbloquear el usuario. Intentá nuevamente.");
            return RedirectToIndex(context.Page, context.Search, context.Sort, context.Status);
        }

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "El usuario se desbloqueó correctamente.");
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
            ErrorCategoria.NotFound => "El usuario ya no está disponible.",
            ErrorCategoria.Transport => "No se pudo desbloquear el usuario. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo desbloquear el usuario. Intentá nuevamente.",
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

        try
        {
            var segmento = IsBlockedView
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
        string.Equals(status, BlockedView, StringComparison.OrdinalIgnoreCase)
            ? BlockedView
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
