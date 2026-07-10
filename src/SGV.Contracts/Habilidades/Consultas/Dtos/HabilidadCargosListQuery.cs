namespace SGV.Contracts.Habilidades.Consultas.Dtos;

/// <summary>
/// Query parameters for the paginated, filtered listing of cargos
/// associated to a habilidad. All filters are optional; omitting them
/// returns all active cargos associated to the requested habilidad for the
/// requested page.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Search">Optional substring filter applied to cargo
/// code/name.</param>
/// <param name="Sort">Optional sort expression (e.g. <c>codigo_asc</c>).</param>
/// <param name="Segmento">Active/deleted segment; defaults to
/// <see cref="HabilidadSegmentoListado.Activas"/>.</param>
public sealed record HabilidadCargosListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    HabilidadSegmentoListado Segmento = HabilidadSegmentoListado.Activas);