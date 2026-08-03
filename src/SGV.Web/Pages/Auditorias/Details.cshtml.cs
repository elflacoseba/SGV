using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Auditoria;
using SGV.Web.Integration.Common;
using SGV.Web.Pages.Common;

namespace SGV.Web.Pages.Auditorias;

/// <summary>
/// PageModel del detalle readonly de auditoría (Slice B del
/// change <c>2026-07-31-ajustes-listado-auditoria</c> /
/// issue #248). Consume
/// <see cref="IAuditoriaApiClient.GetDetalleAsync"/> y expone la
/// vista de solo lectura del
/// <see cref="AuditoriaDetalleDto"/> enriquecido (única vía del
/// sistema para arrastrar <c>EntityId</c>, <c>OldValuesJson</c> y
/// <c>NewValuesJson</c> al wire — D-2 cerrado por separación
/// física de tipos).
/// </summary>
/// <remarks>
/// <para>
/// Acceso restringido al rol
/// <see cref="RolesSgv.Administrador"/>. La autorización vive en
/// la PageModel (defensa en profundidad) y se revalida en el
/// endpoint backend <c>GET /api/v1/auditorias/{id}</c>.
/// </para>
/// <para>
/// El handler distingue tres estados recuperables:
/// <list type="bullet">
/// <item><c>GetDetalleAsync</c> devuelve <c>null</c> → estado
/// legible «no disponible» (404 upstream).</item>
/// <item>Una falla de transporte clasificada por
/// <see cref="TransportFailureClassifier"/> → banner de error
/// recuperable preservando el <c>id</c> consultado (el CTA
/// "Volver al listado" sigue armado, no se rompe el
/// flujo).</item>
/// <item>Resto de excepciones → se propagan para no enmascarar
/// bugs reales.</item>
/// </list>
/// </para>
/// <para>
/// El botón "Volver al listado" preserva el contexto del
/// listado (<c>p</c>, <c>pageSize</c>, <c>sort</c>,
/// <c>correlationId</c> + filtros) para que el usuario no pierda
/// el estado al descender al detalle. Esto es una guía de UX no
/// normativa del diseño.
/// </para>
/// </remarks>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class DetailsModel(
    IAuditoriaApiClient auditoriaApiClient,
    ILogger<DetailsModel> logger) : PageModel
{
    /// <summary>
    /// DTO enriquecido del registro de auditoría solicitado.
    /// <c>null</c> cuando el registro no existe o cuando el
    /// upstream devolvió un error recuperable (en cuyo caso
    /// <see cref="IsNotFound"/> o
    /// <see cref="TransportErrorMessage"/> estarán poblados para
    /// que la vista muestre el estado adecuado).
    /// </summary>
    public AuditoriaDetalleDto? Detalle { get; private set; }

    /// <summary>
    /// <c>true</c> cuando el registro no existe o la consulta
    /// upstream falló. La vista debe mostrar el estado legible
    /// «no disponible».
    /// </summary>
    public bool IsNotFound { get; private set; }

    /// <summary>
    /// Mensaje visible cuando el upstream cae con un error de
    /// transporte recuperable. Se renderiza como
    /// <c>alert-danger</c> en la vista y NO se mezcla con el
    /// estado de «no encontrado»: ambos estados pueden
    /// coexistir en el flujo de UX.
    /// </summary>
    public string? TransportErrorMessage { get; private set; }

    /// <summary>Página del listado desde la que se navegó al detalle.</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Tamaño de página vigente al navegar al detalle.</summary>
    public int PageSize { get; private set; } = IndexModel.DefaultPageSize;

    /// <summary>Orden server-side vigente al navegar al detalle.</summary>
    public string? Sort { get; private set; }

    /// <summary>Filtro de correlación vigente al navegar al detalle.</summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>Filtro vigente: nombre de la entidad auditada.</summary>
    public string? EntityName { get; private set; }

    /// <summary>Filtro vigente: operación.</summary>
    public string? Operation { get; private set; }

    /// <summary>Filtro vigente: fecha desde.</summary>
    public DateTime? DateFrom { get; private set; }

    /// <summary>Filtro vigente: fecha hasta.</summary>
    public DateTime? DateTo { get; private set; }

    /// <summary>Filtro vigente: userId.</summary>
    public string? UserId { get; private set; }

    /// <summary>
    /// Construye la URL de retorno al listado preservando el
    /// contexto del usuario (página, pageSize, orden, filtros).
    /// Misma estrategia que la página <c>Cargos/Details</c> y
    /// <c>Personas/Details</c>.
    /// </summary>
    public string BuildBackUrl()
    {
        var values = new
        {
            p = CurrentPage,
            pageSize = PageSize,
            sort = Sort,
            correlationId = CorrelationId,
            entityName = EntityName,
            operation = Operation,
            dateFrom = DateFrom,
            dateTo = DateTo,
            userId = UserId
        };
        return Url.Page("/Auditorias/Index", values) ?? "/auditorias";
    }

    /// <summary>
    /// Handler GET del detalle. Carga el
    /// <see cref="AuditoriaDetalleDto"/> vía
    /// <see cref="IAuditoriaApiClient.GetDetalleAsync"/> y
    /// clasifica el resultado en éxito / no-encontrado / falla de
    /// transporte. Los parámetros del querystring se preservan
    /// para armar el CTA "Volver al listado".
    /// </summary>
    public async Task OnGetAsync(
        Guid id,
        [FromQuery(Name = "p")] int currentPage = 1,
        [FromQuery(Name = "pageSize")] int pageSize = IndexModel.DefaultPageSize,
        [FromQuery(Name = "sort")] string? sort = null,
        [FromQuery(Name = "correlationId")] Guid? correlationId = null,
        [FromQuery(Name = "entityName")] string? entityName = null,
        [FromQuery(Name = "operation")] string? operation = null,
        [FromQuery(Name = "dateFrom")] DateTime? dateFrom = null,
        [FromQuery(Name = "dateTo")] DateTime? dateTo = null,
        [FromQuery(Name = "userId")] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(1, currentPage);
        PageSize = NormalizePageSize(pageSize);
        Sort = NormalizeSort(sort);
        CorrelationId = correlationId;
        EntityName = Normalize(entityName);
        Operation = Normalize(operation);
        DateFrom = dateFrom;
        DateTo = dateTo;
        UserId = Normalize(userId);

        try
        {
            var detalle = await auditoriaApiClient
                .GetDetalleAsync(id, cancellationToken)
                .ConfigureAwait(false);

            if (detalle is null)
            {
                IsNotFound = true;
                logger.LogWarning(
                    "Auditoria with Id {Id} was not found or is no longer available.",
                    id);
                return;
            }

            Detalle = detalle;
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            // Estado recuperable preservando el id consultado: el
            // usuario puede reintentar con "Volver al listado" o
            // refrescar la página sin re-armar el request. El
            // banner usa la copy canónica del shell.
            IsNotFound = true;
            TransportErrorMessage = PageFeedback.TransportMessage;
            logger.LogError(ex, "Failed to load auditoria with Id {Id}.", id);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Espejo del
    /// <see cref="SGV.Web.Pages.Auditorias.IndexModel.NormalizeSort"/>.
    /// La PageModel de Details NO pinta el icono del sort pero
    /// debe preservar el criterio en el link de retorno, así
    /// que también colapsa claves no reconocidas al
    /// <see cref="SGV.Web.Pages.Auditorias.IndexModel.DefaultSort"/>.
    /// </summary>
    private static string? NormalizeSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return IndexModel.DefaultSort;
        var trimmed = value.Trim();
        return trimmed switch
        {
            "fecha_asc" or "fecha_desc"
                or "entidad_asc" or "entidad_desc"
                or "operacion_asc" or "operacion_desc"
                or "usuario_asc" or "usuario_desc"
                or "correlacion_asc" or "correlacion_desc" => trimmed,
            _ => IndexModel.DefaultSort
        };
    }

    /// <summary>
    /// Espejo del
    /// <see cref="SGV.Web.Pages.Auditorias.IndexModel.NormalizePageSize"/>:
    /// el <c>pageSize</c> que viaja en el link de retorno debe
    /// estar normalizado al set canónico.
    /// </summary>
    private static int NormalizePageSize(int value)
    {
        if (value <= 0) return IndexModel.DefaultPageSize;
        return IndexModel.AllowedPageSizes.Contains(value)
            ? value
            : IndexModel.DefaultPageSize;
    }
}
