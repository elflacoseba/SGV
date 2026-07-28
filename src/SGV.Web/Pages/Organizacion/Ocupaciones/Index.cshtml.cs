using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Common;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// PageModel del listado web de Ocupaciones. Espejo de
/// <c>PuestoIndexModel</c> ajustado al backend segmentado
/// (<c>?status=activas|eliminadas</c>) y los filtros contextuales
/// (<c>?personaId=&amp;puestoId=</c>). Toggle vigentes/historial, paginación
/// server-side y feedback uniforme con <see cref="PageFeedback"/>.
/// </summary>
/// <remarks>
/// Issue #208 / Slice 2: las acciones de mutación (Finalizar/Eliminar/Reactivar)
/// se renderizan en el Index como placeholders; los handlers POST se completan
/// en Slice 3a junto con la página <c>Details</c>. La forma del Index
/// (segmento + paginación + acciones Ver/Editar) ya queda consolidada en este
/// slice para que la carga visual del módulo sea operativa end-to-end para
/// los usuarios de lectura.
/// </remarks>
[Authorize]
public sealed class IndexModel(
    IOcupacionApiClient ocupacionApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    /// <summary>Etiqueta "Eliminadas" que se renderiza en el toggle y se usa como valor del parámetro <c>status</c>.</summary>
    private const string DeletedView = "eliminadas";

    /// <summary>Tamaño de página fijo para la grilla activa/eliminada.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Filas visibles en la página actual.</summary>
    public IReadOnlyList<OcupacionListItemViewModel> Items { get; private set; } = [];

    /// <summary>Página actual (1-based).</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary><c>true</c> cuando el backend expone paginación (siempre en este slice).</summary>
    public bool IsPaginated => true;

    /// <summary>Total de ocupaciones que matchean el segmento y filtros vigentes.</summary>
    public int TotalCount { get; private set; }

    /// <summary>Total de páginas calculadas a partir del backend segmentado.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Término de búsqueda normalizado.</summary>
    public string? Search { get; private set; }

    /// <summary>Expresión de orden actual (e.g. <c>persona_asc</c>).</summary>
    public string? Sort { get; private set; }

    /// <summary>Segmento vigente del listado: <c>null</c> para activas, <c>"eliminadas"</c> para historial.</summary>
    public string? Segmento { get; private set; }

    /// <summary><c>true</c> cuando el segmento vigente es <c>"eliminadas"</c>.</summary>
    public bool IsDeletedView =>
        string.Equals(Segmento, DeletedView, StringComparison.OrdinalIgnoreCase);

    /// <summary>Mensaje de error visible cuando la carga inicial del listado falla.</summary>
    public string? LoadErrorMessage { get; private set; }

    public bool EsAdministrador => User.IsInRole(RolesSgv.Administrador);

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

    /// <summary>
    /// Construye la URL del enlace "Detalle" preservando el contexto del
    /// listado (página, búsqueda, orden y segmento vía <c>returnStatus</c>).
    /// Espejo de <c>PuestoIndexModel.BuildDetailsUrl</c>: usa un fallback
    /// hard-coded a <c>/organizacion/ocupaciones/detalles/{id}</c> porque
    /// <see cref="IUrlHelper.Page(string, object?)"/> devuelve <c>null</c>
    /// cuando la Razor Page destino aún no existe en el set de páginas del
    /// host (la página Details llega en Slice 3a).
    /// </summary>
    public string BuildDetailsUrl(Guid id)
    {
        return Url.Page(
            "/Organizacion/Ocupaciones/Details",
            new
            {
                id,
                p = CurrentPage,
                search = Search,
                sort = Sort,
                returnStatus = Segmento
            }) ?? $"/organizacion/ocupaciones/detalles/{id:D}";
    }

    /// <summary>
    /// Construye la URL del enlace "Editar" preservando el contexto del
    /// listado. Mismo fallback hard-coded que <see cref="BuildDetailsUrl"/>
    /// porque la página Edit también pertenece a Slice 3a.
    /// </summary>
    public string BuildEditUrl(Guid id)
    {
        return Url.Page(
            "/Organizacion/Ocupaciones/Edit",
            new
            {
                id,
                p = CurrentPage,
                search = Search,
                sort = Sort,
                returnStatus = Segmento
            }) ?? $"/organizacion/ocupaciones/editar/{id:D}";
    }

    /// <summary>
    /// Construye los route values del toggle Activas/Eliminadas con reset de
    /// página y preservación de búsqueda y orden.
    /// </summary>
    public object BuildToggleSegmentoRouteValues(string? targetSegmento) => new
    {
        p = 1,
        search = Search,
        sort = Sort,
        status = string.Equals(targetSegmento, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null
    };

    /// <summary>
    /// Construye los route values de un enlace de paginación preservando el
    /// segmento, la búsqueda y el orden vigentes.
    /// </summary>
    public object BuildPagedRouteValues(int page) => new
    {
        p = Math.Max(1, page),
        search = Search,
        sort = Sort,
        status = Segmento
    };

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadErrorMessage = null;

        try
        {
            var segmento = IsDeletedView
                ? OcupacionSegmentoListado.Eliminadas
                : OcupacionSegmentoListado.Activas;
            var query = new OcupacionListQuery(
                Page: CurrentPage,
                PageSize: DefaultPageSize,
                Search: Search,
                Sort: Sort,
                Segmento: segmento);

            var result = await ocupacionApiClient.ListarAsync(query, cancellationToken);

            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, result.PageSize)));

            Items = result.Items
                .Select(OcupacionListItemViewModel.FromDto)
                .ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load ocupaciones page: transport failure.");
            SetLoadErrorState();
        }
    }

    /// <summary>
    /// Resetea el estado de carga a un fallback vacío tras un fallo controlado
    /// de carga inicial. Centralizado para mantener consistencia con el patrón
    /// de <c>PuestoIndexModel.SetLoadErrorState</c>.
    /// </summary>
    private void SetLoadErrorState()
    {
        Items = [];
        TotalCount = 0;
        CurrentPage = 1;
        LoadErrorMessage = "No se pudo cargar el listado de ocupaciones. Intentá nuevamente.";
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSegmento(string? status)
        => string.Equals(status, DeletedView, StringComparison.OrdinalIgnoreCase) ? DeletedView : null;
}