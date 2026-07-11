using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Puestos;

/// <summary>
/// PageModel del listado web de puestos. Espejo de
/// <c>CargoIndexModel</c> ajustado al backend de Puestos (que NO expone
/// <c>/consulta?status=...</c> segmentado), con baja lógica
/// (<c>?handler=Delete</c>) y reactivación (<c>?handler=Reactivate</c>) que
/// preserva el contexto del listado.
/// </summary>
[Authorize]
public sealed class IndexModel(IPuestosApiClient puestosApiClient, ILogger<IndexModel> logger) : PageModel
{
    /// <summary>Etiqueta "Eliminadas" que se renderiza en el tooltip del toggle deshabilitado (locked #2).</summary>
    private const string DeletedView = "eliminadas";

    /// <summary>Filas visibles en la página actual.</summary>
    public IReadOnlyList<PuestoListItemViewModel> Items { get; private set; } = [];

    /// <summary>
    /// Página actual (1-based). El backend de Puestos no expone paginación; se
    /// conserva el parámetro para preservar contexto en PRG (forward-compat
    /// cuando el backend sume <c>page</c>/<c>pageSize</c>).
    /// </summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>
    /// <c>true</c> cuando el backend expone paginación. Para Puestos siempre es
    /// <c>false</c> en el slice actual — la lista se renderiza plana sin
    /// controles de paginación.
    /// </summary>
    public bool IsPaginated => false;

    /// <summary>Total de puestos activos en la respuesta del backend.</summary>
    public int TotalCount { get; private set; }

    /// <summary>Término de búsqueda normalizado.</summary>
    public string? Search { get; private set; }

    /// <summary>Expresión de orden actual (e.g. <c>nombre_asc</c>).</summary>
    public string? Sort { get; private set; }

    /// <summary>Segmento vigente del listado: <c>null</c> para activas, <c>"eliminadas"</c> para eliminadas.</summary>
    public string? Segmento { get; private set; }

    /// <summary><c>true</c> cuando el segmento vigente es <c>eliminadas</c>.</summary>
    public bool IsDeletedView =>
        string.Equals(Segmento, DeletedView, StringComparison.OrdinalIgnoreCase);

    /// <summary>Mensaje de error visible cuando la carga inicial del listado falla.</summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>Mensaje de feedback tras una operación (baja lógica, reactivación).</summary>
    public string? StatusMessage => PageFeedback.GetStatusMessage(TempData);

    /// <summary>Tipo de feedback: <c>success</c> o <c>danger</c>.</summary>
    public string StatusKind => PageFeedback.GetStatusKind(TempData);

    /// <summary>
    /// Identificador del último puesto eliminado, persistido en TempData
    /// durante el PRG desde <see cref="OnPostDeleteAsync"/>. El valor se
    /// limpia tras una reactivación exitosa
    /// (<see cref="OnPostReactivateAsync"/>).
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

        // PR 2: si el POST de Delete propagó el id del puesto eliminado como
        // query string, lo persistimos en TempData para que el banner pueda
        // renderizar el CTA de reactivación rápida.
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
        var normalizedSearch = Normalize(search);
        var normalizedSort = Normalize(sort);
        var normalizedSegmento = NormalizeSegmento(status);
        currentPage = Math.Max(1, currentPage);

        var result = await puestosApiClient.DeleteAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            PageFeedback.SetSuccess(TempData, "El puesto se eliminó correctamente.");

            // Propagar el id del puesto eliminado en el PRG para que el
            // siguiente GET pueda persistirlo en TempData y renderizar el
            // CTA de reactivación rápida en el banner (espejo del patrón
            // de CargoIndexModel).
            return RedirectToPage("/Organizacion/Puestos/Index", new
            {
                p = currentPage,
                search = normalizedSearch,
                sort = normalizedSort,
                status = normalizedSegmento,
                deletedId = id
            });
        }

        var message = result.StatusCode == System.Net.HttpStatusCode.Conflict
            ? $"No se pudo eliminar el puesto. {result.Message}".Trim()
            : result.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "El puesto ya no está disponible."
                : "No se pudo eliminar el puesto. Intentá nuevamente.";

        PageFeedback.SetDanger(TempData, message);

        return RedirectToPage("/Organizacion/Puestos/Index", new
        {
            p = currentPage,
            search = normalizedSearch,
            sort = normalizedSort,
            status = normalizedSegmento
        });
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

        var result = await puestosApiClient.ReactivateAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "El puesto se reactivó correctamente.");

            // Limpiar el LastDeletedId tras una reactivación exitosa para
            // que el banner ya no ofrezca el CTA.
            ClearLastDeleted();

            // Tras éxito, redirigir a la vista Activas sin status=eliminadas.
            return RedirectToPage("/Organizacion/Puestos/Index", new
            {
                p = currentPage,
                search = normalizedSearch,
                sort = normalizedSort
            });
        }

        var errorCode = result.Error?.Code;
        var errorMessage = result.Error?.Message;
        var message = result.Error?.Type switch
        {
            SGV.Contracts.Organizacion.Comandos.PuestoErrorType.Conflict =>
                $"No se pudo reactivar el puesto. {errorMessage}",
            SGV.Contracts.Organizacion.Comandos.PuestoErrorType.NotFound =>
                "El puesto ya no está disponible para reactivar.",
            _ => "No se pudo reactivar el puesto. Intentá nuevamente."
        };

        PageFeedback.SetDanger(TempData, message);
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            TempData["ErrorCode"] = errorCode;
        }

        // Tras fallo, permanecer en la vista Eliminadas para permitir reintento.
        return RedirectToPage("/Organizacion/Puestos/Index", new
        {
            p = currentPage,
            search = normalizedSearch,
            sort = normalizedSort,
            status = normalizedSegmento
        });
    }

    /// <summary>
    /// Construye la próxima expresión de orden al alternar la columna. Si
    /// ya ordena por la misma columna ascendente, alterna a descendente.
    /// </summary>
    public string GetSortRoute(string column)
    {
        var isSameColumn = Sort?.StartsWith(column, StringComparison.OrdinalIgnoreCase) == true;
        var isDesc = Sort?.EndsWith("_desc", StringComparison.OrdinalIgnoreCase) == true;

        return isSameColumn && !isDesc
            ? $"{column}_desc"
            : $"{column}_asc";
    }

    /// <summary>Devuelve la clase del icono de orden para la columna, o <c>null</c> si no es la columna activa.</summary>
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
    /// Construye los route values del enlace "Detalle" preservando el contexto
    /// del listado (página, búsqueda, orden y segmento vía returnStatus).
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
    /// Construye los route values del enlace "Editar" preservando el contexto
    /// del listado (página, búsqueda, orden y segmento vía returnStatus) para
    /// que la página de edición pueda devolver al usuario a la misma vista
    /// (espejo de <c>CargoIndexModel.BuildEditRouteValues</c>).
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
    /// PR 3C — refactor a <c>Url.Page</c>. La página <c>Puestos/Details</c>
    /// ya existe (introducida en este PR), por lo que el helper ahora delega
    /// a <see cref="IUrlHelper.Page(string, object?)"/> con
    /// <c>/Organizacion/Puestos/Details</c> como página destino y
    /// <c>id</c>, <c>p</c>, <c>search</c>, <c>sort</c>, <c>returnStatus</c>
    /// como route values. <c>returnStatus</c> (en vez de <c>status</c>) es
    /// el nombre del parámetro del PageModel de Details (espejo del patrón
    /// del Index: el PageModel acepta el segmento como <c>returnStatus</c>
    /// para no colisionar con el filtro del listado).
    /// </summary>
    public string BuildDetailsUrl(Guid id)
    {
        return Url.Page(
            "/Organizacion/Puestos/Details",
            new
            {
                id,
                p = CurrentPage,
                search = Search,
                sort = Sort,
                returnStatus = Segmento
            }) ?? $"/organizacion/puestos/detalles/{id:D}";
    }

    /// <summary>
    /// Construye los route values del toggle Activas/Eliminadas con reset de
    /// página y preservación de búsqueda y orden. El segmento "eliminadas" se
    /// serializa cuando <paramref name="targetSegmento"/> lo pide (en este slice
    /// el botón está deshabilitado, pero el helper queda para forward-compat).
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

        // PR 2: leer LastDeletedId desde TempData para que el banner del
        // Razor pueda renderizar el CTA. El getter inline del Razor y esta
        // propiedad quedan sincronizados para que ambas vistas (OnGet/OnPost)
        // accedan al mismo valor persistido.
        LastDeletedId = PageFeedback.GetLastDeletedId(TempData);

        try
        {
            // PR 2 — Decisión locked #2: el backend de Puestos no expone
            // un endpoint segmentado (/consulta?status=activas|eliminadas),
            // por lo que GetAllAsync() es la fuente única. Forward-compat:
            // cuando llegue el endpoint segmentado, este LoadAsync deberá
            // ramificar por Segmento.
            var dtos = await puestosApiClient.GetAllAsync(cancellationToken);

            // Filtro y orden en memoria (mismo patrón que Cargos pre-PR3).
            IEnumerable<PuestoDto> query = dtos;
            var lowered = Search?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(lowered))
            {
                query = query.Where(p =>
                    p.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase) ||
                    p.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase) ||
                    (p.Descripcion?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    p.UnidadOrganizativaNombre.Contains(lowered, StringComparison.OrdinalIgnoreCase) ||
                    p.CargoNombre.Contains(lowered, StringComparison.OrdinalIgnoreCase));
            }

            query = Sort?.ToLowerInvariant() switch
            {
                "codigo_desc" => query.OrderByDescending(static p => p.Codigo, StringComparer.OrdinalIgnoreCase),
                "codigo_asc" => query.OrderBy(static p => p.Codigo, StringComparer.OrdinalIgnoreCase),
                "nombre_desc" => query.OrderByDescending(static p => p.Nombre, StringComparer.OrdinalIgnoreCase),
                "nombre_asc" => query.OrderBy(static p => p.Nombre, StringComparer.OrdinalIgnoreCase),
                _ => query.OrderBy(static p => p.Codigo, StringComparer.OrdinalIgnoreCase)
            };

            var materialized = query.Select(MapToViewModel).ToArray();
            Items = materialized;
            TotalCount = materialized.Length;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to load puestos page: transport error.");
            SetLoadErrorState();
        }
        catch (TaskCanceledException ex)
        {
            // Cubre timeouts del HttpClient (Cancelación al superar Timeout=10s
            // configurado en Program.cs para IPuestosApiClient).
            logger.LogError(ex, "Failed to load puestos page: request timeout.");
            SetLoadErrorState();
        }
        catch (System.Text.Json.JsonException ex)
        {
            // Cuerpo no parseable: el helper DeleteAsync del cliente lo absorbe,
            // pero GetAllAsync puede propagar al deserializar respuestas malformadas.
            logger.LogError(ex, "Failed to load puestos page: malformed response.");
            SetLoadErrorState();
        }
    }

    /// <summary>
    /// Resetea el estado de carga a un fallback vacío tras un fallo controlado
    /// de carga inicial. Centralizado para mantener consistencia entre los
    /// tres caminos de error capturados en <see cref="LoadAsync"/>.
    /// </summary>
    private void SetLoadErrorState()
    {
        Items = [];
        TotalCount = 0;
        CurrentPage = 1;
        LoadErrorMessage = "No se pudo cargar el listado de puestos. Intentá nuevamente.";
    }

    private void ClearLastDeleted() => PageFeedback.ClearLastDeletedId(TempData);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSegmento(string? status)
        => string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null;

    /// <summary>Mapea un <see cref="PuestoDto"/> al viewmodel de grilla.</summary>
    private static PuestoListItemViewModel MapToViewModel(PuestoDto item)
        => new(
            item.Id,
            item.Codigo,
            item.Nombre,
            item.Descripcion,
            item.UnidadOrganizativaNombre,
            item.CargoNombre,
            item.PuestoSuperiorId);
}
