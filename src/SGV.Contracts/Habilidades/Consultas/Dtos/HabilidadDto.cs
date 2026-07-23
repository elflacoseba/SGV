namespace SGV.Contracts.Habilidades.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for Habilidad. Excludes audit and internal tracking fields.
///
/// <b>Breaking change (issue migrar-categoria-habilidades-a-tabla):</b>
/// the legacy <c>string? Categoria</c> field is replaced by the FK
/// (<see cref="CategoriaId"/>) plus the denormalized
/// (<see cref="CategoriaNombre"/>) projection. Clients consuming the JSON
/// payload MUST migrate from <c>Categoria</c> to either of the new fields.
/// </summary>
public sealed record HabilidadDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    Guid? CategoriaId,
    string? CategoriaNombre);