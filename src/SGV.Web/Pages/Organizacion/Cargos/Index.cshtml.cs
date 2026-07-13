using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;

namespace SGV.Web.Pages.Organizacion.Cargos;

/// <summary>
/// PageModel del listado web de cargos. Usa consulta server-side segmentada
/// (<c>QueryAsync</c>) hacia <c>GET /api/v1/cargos/consulta</c> y soporta
/// alternar entre <c>activas</c> y <c>eliminadas</c>. Mantiene la baja lógica
/// (<c>?handler=Delete</c>) y agrega reactivación (<c>?handler=Reactivate</c>)
/// preservando el segmento cuando la operación falla.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre <see cref="ErrorCategoria"/>
/// en OnPostDelete y OnPostReactivate. <c>Unauthorized</c> redirige vía
/// <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class IndexModel(
    ICargoApiClient cargoApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<IndexModel> logger) : PageModel
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
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>
    /// Tipo de feedback: <c>success</c> o <c>danger</c>.
    /// </summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>
    /// Identificador del último cargo eliminado, persistido en TempData
    /// durante el PRG desde <see cref="OnPostDeleteAsync"/>. El valor
    /// se limpia tras una reactivación exitosa
    /// (<see cref="OnPostReactivateAsync"/>). Es <c>null</c> cuando no hay
    /// una última baja pendiente de reactivación rápida.
    /// </summary>
    public Guid? LastDeletedId { get; private set; }

    /// <summary>
    /// <c>true</c> cuando hay un <see cref="LastDeletedId"/> pendiente de
    /// reactivar desde el banner. El CTA solo se muestra cuando el
    /// segmento vigente es Activas (REQ-CW-06 MUST NOT).
    /// </summary>
    public bool HasLastDeleted => LastDeletedId.HasValue;

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

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

        // REQ-CW-06: si el POST de Delete propagó el id del cargo eliminado
        // como query string, lo persistimos en TempData para que el banner
        // pueda renderizar el CTA de reactivación rápida. El Razor accede
        // por TempData directamente (no por esta propiedad) por compatibilidad
        // con el patrón de Unidades Organizativas.
        if (deletedId.HasValue)
        {
            PageFeedback.SetLastDeletedId(TempData, deletedId.Value);
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
        if (!EsAdministrador)
        {
            return Forbid();
        }

        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedSegmento = NormalizeSegmento(status);
        currentPage = Math.Max(1, currentPage);

        var result = await cargoApiClient.DeleteAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            var redirectPage = await ResolveRedirectPageAsync(currentPage, normalizedSearch, normalizedSort, normalizedSegmento, cancellationToken);
            PageFeedback.SetSuccess(TempData, "El cargo se eliminó correctamente.");

            // REQ-CW-06: propagar el id del cargo eliminado en el PRG para
            // que el siguiente GET pueda persistirlo en TempData y renderizar
            // el CTA de reactivación rápida en el banner.
            return RedirectToPage("/Organizacion/Cargos/Index", new { p = redirectPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento, deletedId = id });
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
            ErrorCategoria.Conflict => $"No se pudo eliminar el cargo. {result.Message}".Trim(),
            ErrorCategoria.NotFound => PageFeedback.NotFoundDeleteMessage,
            ErrorCategoria.Transport => "No se pudo eliminar el cargo. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo eliminar el cargo. Intentá nuevamente.",
            _ => MapCategoriaToMessage(result.Categoria)
        };

        PageFeedback.SetDanger(TempData, message);

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
        if (!EsAdministrador)
        {
            return Forbid();
        }

        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedSegmento = NormalizeSegmento(status);
        currentPage = Math.Max(1, currentPage);

        var result = await cargoApiClient.ReactivateAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "El cargo se reactivó correctamente.");

            // REQ-CW-06: limpiar el LastDeletedId tras una reactivación
            // exitosa para que el banner ya no ofrezca el CTA.
            ClearLastDeleted();

            // Tras éxito, redirigir a la vista Activas sin status=eliminadas.
            return RedirectToPage("/Organizacion/Cargos/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort });
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

        var errorCode = result.Error?.Code;
        var errorMessage = result.Error?.Message;
        var categoria = result.Error?.Categoria ?? ErrorCategoria.Unexpected;
        var message = categoria switch
        {
            ErrorCategoria.Conflict => $"No se pudo reactivar el cargo. {errorMessage}",
            ErrorCategoria.NotFound => "El cargo ya no está disponible para reactivar.",
            ErrorCategoria.Transport => "No se pudo reactivar el cargo. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo reactivar el cargo. Intentá nuevamente.",
            _ => MapCategoriaToMessage(categoria)
        };

        PageFeedback.SetDanger(TempData, message);
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            TempData["ErrorCode"] = errorCode;
        }

        // Tras fallo, permanecer en la vista Eliminadas para permitir reintento.
        return RedirectToPage("/Organizacion/Cargos/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
    }

    /// <summary>
    /// Switch exhaustivo sobre <see cref="ErrorCategoria"/>. Cubre las 7
    /// variantes sin <c>default</c> silencioso (design §8.1, F3).
    /// <c>Unauthorized</c> lanza porque su flujo es redirigir vía
    /// <see cref="IAuthSessionRedirector"/> antes de mostrar mensaje inline.
    /// </summary>
    internal static string MapCategoriaToMessage(ErrorCategoria categoria) => categoria switch
    {
        ErrorCategoria.NotFound => PageFeedback.NotFoundDeleteMessage,
        ErrorCategoria.Conflict => "Conflicto al procesar la operación.",
        ErrorCategoria.Validation => "Revisá los datos ingresados.",
        ErrorCategoria.Unauthorized => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            "Unauthorized se redirige vía IAuthSessionRedirector antes de mostrar mensaje inline."),
        ErrorCategoria.Forbidden => PageFeedback.ForbiddenMessage,
        ErrorCategoria.Transport => PageFeedback.TransportMessage,
        ErrorCategoria.Unexpected => PageFeedback.UnexpectedMessage,
        _ => throw new System.Runtime.CompilerServices.SwitchExpressionException(
            $"Unhandled categoria: {categoria}"),
    };

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

        // REQ-CW-06: leer LastDeletedId desde TempData para que el banner
        // del Razor pueda renderizar el CTA. El getter inline del Razor
        // y esta propiedad quedan sincronizados para que ambas vistas
        // (OnGet/OnPost) accedan al mismo valor persistido.
        LastDeletedId = PageFeedback.GetLastDeletedId(TempData);

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

    private void ClearLastDeleted() => PageFeedback.ClearLastDeletedId(TempData);

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
