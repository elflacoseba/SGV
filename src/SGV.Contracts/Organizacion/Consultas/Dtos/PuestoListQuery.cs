namespace SGV.Contracts.Organizacion.Consultas.Dtos;

/// <summary>
/// Defines the listing segment for puesto queries: active (non-deleted) puestos
/// or soft-deleted puestos. The value <c>Activas</c> is the default used by the
/// query contract and by the HTTP/Web boundary when no explicit <c>status</c>
/// is provided.
/// </summary>
public enum PuestoSegmentoListado
{
    /// <summary>Return only active (non-deleted) puestos. This is the default.</summary>
    Activas = 0,

    /// <summary>Return only soft-deleted puestos.</summary>
    Eliminadas = 1
}

/// <summary>
/// Query parameters for paginated, filtered listing of puestos. All filters
/// are optional; omitting them returns all active puestos for the requested
/// page.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Search">Optional substring filter applied to code/name/description.</param>
/// <param name="Sort">Optional sort expression (e.g. <c>nombre_asc</c>).</param>
/// <param name="Segmento">Active/deleted segment; defaults to <see cref="PuestoSegmentoListado.Activas"/>.</param>
public sealed record PuestoListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    PuestoSegmentoListado Segmento = PuestoSegmentoListado.Activas);
