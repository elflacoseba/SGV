namespace SGV.Contracts.Habilidades.Consultas.Dtos;

/// <summary>
/// Defines the listing segment for habilidad queries: active (non-deleted)
/// habilidades or soft-deleted habilidades. The value <c>Activas</c> is the
/// default used by the query contract and by the HTTP/Web boundary when no
/// explicit <c>status</c> is provided.
/// </summary>
public enum HabilidadSegmentoListado
{
    /// <summary>Return only active (non-deleted) habilidades. This is the default.</summary>
    Activas = 0,

    /// <summary>Return only soft-deleted habilidades.</summary>
    Eliminadas = 1
}

/// <summary>
/// Query parameters for paginated, filtered listing of habilidades. All
/// filters are optional; omitting them returns all active habilidades for
/// the requested page.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Search">Optional substring filter applied to code/name/category/description.</param>
/// <param name="Sort">Optional sort expression (e.g. <c>nombre_asc</c>).</param>
/// <param name="Segmento">Active/deleted segment; defaults to <see cref="HabilidadSegmentoListado.Activas"/>.</param>
public sealed record HabilidadListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    HabilidadSegmentoListado Segmento = HabilidadSegmentoListado.Activas);