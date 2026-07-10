namespace SGV.Contracts.Organizacion.Consultas.Dtos;

/// <summary>
/// Defines the listing segment for cargo queries: active (non-deleted) cargos
/// or soft-deleted cargos. The value <c>Activas</c> is the default used by the
/// query contract and by the HTTP/Web boundary when no explicit <c>status</c>
/// is provided.
/// </summary>
public enum CargoSegmentoListado
{
    /// <summary>Return only active (non-deleted) cargos. This is the default.</summary>
    Activas = 0,

    /// <summary>Return only soft-deleted cargos.</summary>
    Eliminadas = 1
}

/// <summary>
/// Query parameters for paginated, filtered listing of cargos. All filters
/// are optional; omitting them returns all active cargos for the requested
/// page.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Search">Optional substring filter applied to code/name/description/level.</param>
/// <param name="Sort">Optional sort expression (e.g. <c>nombre_asc</c>).</param>
/// <param name="Segmento">Active/deleted segment; defaults to <see cref="CargoSegmentoListado.Activas"/>.</param>
public sealed record CargoListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    CargoSegmentoListado Segmento = CargoSegmentoListado.Activas);
