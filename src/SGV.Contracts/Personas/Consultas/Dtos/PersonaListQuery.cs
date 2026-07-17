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
/// <param name="SoloSinUsuario">
/// When <c>true</c>, restricts Activas to personas that do NOT have a
/// <c>AspNetUsers.PersonaId</c> pointing at them (anti-join). When
/// <c>false</c> or <c>null</c> the flag is ignored — back-compat with
/// every existing consumer (Index Personas, typeahead, etc.). Combined
/// with <see cref="PersonaSegmentoListado.Eliminadas"/> the contract MUST
/// return an empty result without invoking the anti-join.
/// </param>
public sealed record PersonaListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    PersonaSegmentoListado Segmento = PersonaSegmentoListado.Activas,
    bool? SoloSinUsuario = null);