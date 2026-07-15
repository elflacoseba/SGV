namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// Paginated result wrapper for persona queries. Mirrors the shape used by other
/// subdomains (Cargos) so the web shell can apply a single pager component.
/// </summary>
/// <param name="Items">Page slice of personas in the requested sort order.</param>
/// <param name="TotalCount">Total matching personas in the segment (across all pages).</param>
/// <param name="Page">1-based page number echoed from the request.</param>
/// <param name="PageSize">Page size echoed from the request.</param>
public sealed record PersonaListadoDto(
    IReadOnlyList<PersonaDto> Items,
    int TotalCount,
    int Page,
    int PageSize);