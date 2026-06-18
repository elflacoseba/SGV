namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// Generic paginated result wrapper used for query endpoints.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
