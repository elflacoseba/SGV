namespace SGV.Contracts.Organizacion.Consultas.Dtos;

/// <summary>
/// Defines the listing segment for unidad organizativa queries.
/// </summary>
public enum UnidadOrganizativaSegmentoListado
{
    /// <summary>Return only active (non-deleted) units. This is the default.</summary>
    Activas = 0,
    /// <summary>Return only soft-deleted units.</summary>
    Eliminadas = 1
}

/// <summary>
/// Query parameters for paginated, filtered listing of organizational units.
/// All filters are optional; omitting them returns all active units.
/// </summary>
/// <param name="Page">1-based page number. The service clamps any value below <c>1</c> up to <c>1</c>.</param>
/// <param name="PageSize">
/// Items per page. The service clamps the value to the closed range
/// <c>[<see cref="MinPageSize"/>, <see cref="MaxPageSize"/>]</c>. Values
/// outside that range never reach the underlying repository, so
/// <c>Skip((page - 1) * pageSize)</c> cannot receive a negative count and
/// <c>Take(pageSize)</c> cannot receive a value larger than
/// <see cref="MaxPageSize"/>.
/// </param>
/// <param name="Search">Optional substring filter applied to code/name.</param>
/// <param name="TipoUnidadOrganizativaId">Optional filter by unit type.</param>
/// <param name="UnidadPadreId">Optional filter by parent unit.</param>
/// <param name="VigenteEn">Optional filter: unit must be effective on this date.</param>
/// <param name="Segmento">Active/deleted segment; defaults to <see cref="UnidadOrganizativaSegmentoListado.Activas"/>.</param>
public sealed record UnidadOrganizativaQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? TipoUnidadOrganizativaId = null,
    Guid? UnidadPadreId = null,
    DateOnly? VigenteEn = null,
    UnidadOrganizativaSegmentoListado Segmento = UnidadOrganizativaSegmentoListado.Activas)
{
    /// <summary>
    /// Default page size used when the caller does not specify one.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Minimum page size accepted by the service. Any value below this
    /// threshold is clamped up to it.
    /// </summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// Maximum page size accepted by the service. Any value above this
    /// threshold is clamped down to it; the upper bound also caps the
    /// response payload to prevent resource exhaustion from a single
    /// request (issue #278).
    /// </summary>
    public const int MaxPageSize = 100;
}
