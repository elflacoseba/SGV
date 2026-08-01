using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Web.Integration.Auditoria;
using SGV.Web.Integration.Common;

namespace SGV.Web.Pages.Auditorias;

/// <summary>
/// PageModel readonly del listado transversal de auditoría (Slice 3
/// del change <c>implementa-modulo-auditorias</c>). Consume el
/// endpoint admin-only <c>GET /api/v1/auditorias</c> vía
/// <see cref="IAuditoriaApiClient"/> y aplica los filtros
/// (<c>EntityName</c>, <c>Operation</c>, <c>DateFrom</c>, <c>DateTo</c>,
/// <c>UserId</c>) via PRG (Post-Redirect-Get) para que la URL
/// siempre refleje el estado vigente.
/// </summary>
/// <remarks>
/// <para>
/// Acceso restringido al rol <see cref="RolesSgv.Administrador"/>
/// (D-1). El <c>[Authorize]</c> cubre la autorización por acción;
/// la autorización por filtro vive en el backend y se reusa el
/// guard de la pipeline JWT del shell web
/// (<c>ApiBearerTokenHandler</c> adjuntado en la DI del cliente).
/// </para>
/// <para>
/// Filtros: la sidebar lateral usa form <c>method="get"</c> para
/// que la URL siempre refleje el estado (PRG equivalente: el
/// browser resuelve la query string y la page la bindea desde
/// <c>Request.Query</c>). La paginación también pasa por querystring
/// (espejo del patrón de Cargos / Habilidades / Personas) preservando
/// los filtros vigentes.
/// </para>
/// <para>
/// Las fallas de transporte se ramifican vía
/// <see cref="TransportFailureClassifier"/> y se traducen a un
/// banner recuperable; el orden de renderizado
/// (banner de error → empty state → tabla) preserva la UX
/// aún ante caídas de la API upstream.
/// </para>
/// </remarks>
[Authorize(Roles = RolesSgv.Administrador)]
public sealed class IndexModel(
    IAuditoriaApiClient auditoriaApiClient,
    ILogger<IndexModel> logger) : PageModel
{
    /// <summary>Tamaño de página fijo para la grilla (paridad con otros módulos).</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Tamaño de página máximo aceptable; valores mayores se clampean.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Filas visibles en la página actual.</summary>
    public IReadOnlyList<AuditoriaDto> Items { get; private set; } = [];

    /// <summary>Página actual (1-based).</summary>
    public int CurrentPage { get; private set; } = 1;

    /// <summary>Cantidad total de páginas calculadas a partir del backend.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Total de filas que matchean los filtros vigentes.</summary>
    public int TotalCount { get; private set; }

    /// <summary>Filtro vigente: nombre de la entidad auditada.</summary>
    public string? EntityName { get; private set; }

    /// <summary>Filtro vigente: operación (Alta / Modificacion / BajaLogica / etc).</summary>
    public string? Operation { get; private set; }

    /// <summary>Filtro vigente: fecha desde (inclusivo).</summary>
    public DateTime? DateFrom { get; private set; }

    /// <summary>Filtro vigente: fecha hasta (inclusivo).</summary>
    public DateTime? DateTo { get; private set; }

    /// <summary>Filtro vigente: identificador del usuario que ejecutó la operación.</summary>
    public string? UserId { get; private set; }

    /// <summary>
    /// Mensaje de error visible cuando la carga inicial del listado
    /// falla con un error de transporte recuperable. Esta página es
    /// read-only; la página no expone acciones POST.
    /// </summary>
    public string? LoadErrorMessage { get; private set; }

    /// <summary>
    /// Carga el listado paginado de auditoría aplicando los filtros
    /// del querystring. Cualquier excepción de transporte (HTTP /
    /// timeout / JSON malformado) capturada por
    /// <see cref="TransportFailureClassifier"/> se traduce a un
    /// banner accionable sin filtrar stack trace al HTML. El resto
    /// de las excepciones se propagan para no enmascarar bugs reales.
    /// </summary>
    public async Task OnGetAsync(
        [FromQuery(Name = "p")] int currentPage = 1,
        int pageSize = DefaultPageSize,
        string? entityName = null,
        string? operation = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        CurrentPage = currentPage < 1 ? 1 : currentPage;
        var resolvedPageSize = pageSize < 1
            ? DefaultPageSize
            : Math.Min(MaxPageSize, pageSize);

        EntityName = Normalize(entityName);
        Operation = Normalize(operation);
        DateFrom = dateFrom;
        DateTo = dateTo;
        UserId = Normalize(userId);

        var query = new AuditoriaListQuery(
            Page: CurrentPage,
            PageSize: resolvedPageSize,
            EntityName: EntityName,
            Operation: Operation,
            DateFrom: DateFrom,
            DateTo: DateTo,
            UserId: UserId);

        try
        {
            var result = await auditoriaApiClient
                .QueryAsync(query, cancellationToken)
                .ConfigureAwait(false);

            CurrentPage = Math.Max(1, result.Page);
            TotalCount = Math.Max(0, result.TotalCount);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, result.PageSize)));
            Items = result.Items.ToArray();
        }
        catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))
        {
            logger.LogError(ex, "Failed to load auditoria page: transport failure.");
            SetLoadErrorState();
        }
    }

    /// <summary>
    /// Construye los route values de un enlace de paginación preservando
    /// los filtros vigentes (EntityName, Operation, DateFrom, DateTo,
    /// UserId) y reseteando el pageSize al default. Espejo del patrón
    /// de <c>PuestoIndexModel.BuildPagedRouteValues</c>: usa <c>p</c> como
    /// key del route value (no <c>page</c>) porque Razor Pages reserva
    /// el nombre <c>page</c> como identificador interno de la página y
    /// omite cualquier route value con ese nombre del URL generado.
    /// El binding del handler lo reescribe vía
    /// <c>[FromQuery(Name = "p")]</c>.
    /// </summary>
    public object BuildPagedRouteValues(int page) => new
    {
        p = Math.Max(1, page),
        pageSize = DefaultPageSize,
        entityName = EntityName,
        operation = Operation,
        dateFrom = DateFrom,
        dateTo = DateTo,
        userId = UserId
    };

    /// <summary>
    /// Resetea el estado de carga a un fallback vacío tras un fallo
    /// controlado de carga inicial. Mismo patrón que el resto de
    /// los Index read-only del shell (Habilidades/Cargos/Personas):
    /// cualquier excepción considerada transporte/serialización por
    /// <see cref="TransportFailureClassifier"/> cae acá.
    /// </summary>
    private void SetLoadErrorState()
    {
        Items = [];
        TotalCount = 0;
        CurrentPage = 1;
        TotalPages = 1;
        LoadErrorMessage = "No se pudo cargar el listado de auditoría. Intentá nuevamente.";
    }

    /// <summary>
    /// Normaliza un parámetro de filtro opcional: <c>null</c> o
    /// whitespace → <c>null</c>; en otro caso <see cref="string.Trim"/>.
    /// Espejo de los helpers de los otros Index del shell.
    /// </summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
