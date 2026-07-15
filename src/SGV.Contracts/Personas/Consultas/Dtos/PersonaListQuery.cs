namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// Query parameters for paginated, filtered listing of personas. All filters are
/// optional; omitting them returns all active personas for the requested page.
/// Sort applies server-side BEFORE pagination so page boundaries stay consistent
/// with the visible ordering.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Search">Optional substring filter applied to Legajo|Nombres|Apellidos|Email|NumeroDocumento.</param>
/// <param name="Sort">Optional sort expression (e.g. <c>apellidos_asc</c>).</param>
/// <param name="Segmento">Active/deleted segment; defaults to <see cref="PersonaSegmentoListado.Activas"/>.</param>
public sealed record PersonaListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    PersonaSegmentoListado Segmento = PersonaSegmentoListado.Activas);