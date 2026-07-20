using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Personas;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Personas;

/// <summary>
/// PageModel del listado web segmentado de personas. Espejo del
/// <see cref="SGV.Web.Pages.Organizacion.Cargos.IndexModel"/>: paginación
/// server-side contra <c>GET /api/v1/personas/consulta</c>, alterna entre
/// segmentos Activas y Eliminadas, soporta baja lógica y reactivación con
/// PRG preservando filtros. Acceso autenticado; las acciones de escritura
/// requieren rol <c>Administrador</c>.
/// <para>
/// Issue #125 / Slice 3: switch exhaustivo sobre <see cref="ErrorCategoria"/>
/// en los handlers POST. <c>Unauthorized</c> redirige vía
/// <see cref="IAuthSessionRedirector"/>.
/// </para>
/// </summary>
[Authorize]
public sealed class IndexModel(
    IPersonaApiClient personaApiClient,
    IAuthSessionRedirector authRedirector,
    ILogger<IndexModel> logger) : PageModel
{
    private const int DefaultPageSize = 10;
    private const string DeletedView = "eliminadas";

    /// <summary>Filas visibles en la página actual.</summary>
    public IReadOnlyList<PersonaListItemViewModel> Items { get; private set; } = [];

    /// <summary>Página actual (1-based).</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Cantidad total de páginas.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Total de personas que matchean el segmento y filtros vigentes.</summary>
    public int TotalCount { get; private set; }

    /// <summary>Término de búsqueda normalizado.</summary>
    public string? Search { get; private set; }

    /// <summary>Expresión de orden actual (e.g. <c>apellidos_asc</c>).</summary>
    public string? Sort { get; private set; }

    /// <summary>Segmento vigente: <c>null</c> para activas, <c>"eliminadas"</c> para eliminadas.</summary>
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
    /// Identificador del último Persona eliminado, persistido en TempData
    /// durante el PRG desde <see cref="OnPostDeleteAsync"/>. Se limpia tras
    /// una reactivación exitosa.
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

        // REQ-CW-06: si el POST de Delete propagó el id de la persona eliminada
        // como query string, lo persistimos en TempData para que el banner
        // pueda renderizar el CTA de reactivación rápida.
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

        var result = await personaApiClient.DesactivarAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            var redirectPage = await ResolveRedirectPageAsync(currentPage, normalizedSearch, normalizedSort, normalizedSegmento, cancellationToken);
            PageFeedback.SetSuccess(TempData, "La persona se eliminó correctamente.");

            // REQ-CW-06: propagar el id de la persona eliminada en el PRG para
            // que el siguiente GET pueda persistirlo en TempData y renderizar
            // el CTA de reactivación rápida en el banner.
            return RedirectToPage("/Personas/Index", new { p = redirectPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento, deletedId = id });
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
            ErrorCategoria.Conflict => $"No se pudo eliminar la persona. {result.Message}".Trim(),
            ErrorCategoria.NotFound => PageFeedback.NotFoundDeleteMessage,
            ErrorCategoria.Transport => "No se pudo eliminar la persona. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo eliminar la persona. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(result.Categoria)
        };

        PageFeedback.SetDanger(TempData, message);

        return RedirectToPage("/Personas/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
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

        var result = await personaApiClient.ReactivarAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            PageFeedback.SetSuccess(TempData, "La persona se reactivó correctamente.");

            // REQ-CW-06: limpiar el LastDeletedId tras una reactivación
            // exitosa para que el banner ya no ofrezca el CTA.
            ClearLastDeleted();

            // Tras éxito, redirigir a la vista Activas sin status=eliminadas.
            return RedirectToPage("/Personas/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort });
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
            ErrorCategoria.Conflict => $"No se pudo reactivar la persona. {errorMessage}",
            ErrorCategoria.NotFound => "La persona ya no está disponible para reactivar.",
            ErrorCategoria.Transport => "No se pudo reactivar la persona. Intentá nuevamente.",
            ErrorCategoria.Unexpected => "No se pudo reactivar la persona. Intentá nuevamente.",
            _ => ErrorCategoryMapper.Map(categoria)
        };

        PageFeedback.SetDanger(TempData, message);
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            TempData["ErrorCode"] = errorCode;
        }

        // Tras fallo, permanecer en la vista Eliminadas para permitir reintento.
        return RedirectToPage("/Personas/Index", new { p = currentPage, search = normalizedSearch, sort = normalizedSort, status = normalizedSegmento });
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
    /// del listado (página, búsqueda, orden y segmento).
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
        // pueda renderizar el CTA.
        LastDeletedId = PageFeedback.GetLastDeletedId(TempData);

        try
        {
            var segmento = IsDeletedView
                ? PersonaSegmentoListado.Eliminadas
                : PersonaSegmentoListado.Activas;

            var result = await personaApiClient.QueryAsync(
                new PersonaListQuery(CurrentPage, DefaultPageSize, Search, Sort, segmento),
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
            logger.LogError(ex, "Failed to load personas page.");
            Items = [];
            TotalCount = 0;
            TotalPages = 1;
            CurrentPage = 1;
            LoadErrorMessage = "No se pudo cargar el listado de personas. Intentá nuevamente.";
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
            var segmentoEnum = string.Equals(segmento, DeletedView, StringComparison.OrdinalIgnoreCase)
                ? PersonaSegmentoListado.Eliminadas
                : PersonaSegmentoListado.Activas;

            var refreshed = await personaApiClient.QueryAsync(
                new PersonaListQuery(currentPage, DefaultPageSize, search, sort, segmentoEnum),
                cancellationToken);
            return refreshed.Items.Count == 0 ? currentPage - 1 : currentPage;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recalculate redirect page after deleting persona.");
            return currentPage;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSegmento(string? status)
        => string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null;

    private static PersonaListItemViewModel MapToViewModel(PersonaDto item)
        => new(
            item.Id,
            item.Legajo,
            item.Nombres,
            item.Apellidos,
            item.Email,
            // Issue #147: TipoDocumentoCodigo se proyecta como null en PR1.
            // El JOIN denormalizado entra en PR2 (T16 del tasks.md).
            item.TipoDocumentoCodigo,
            item.NumeroDocumento,
            item.Telefono,
            item.IsActive);
}