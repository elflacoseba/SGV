namespace SGV.Contracts.Habilidades.Categorias.Consultas;

/// <summary>
/// Consumer-safe DTO for the read-only <c>CategoriaHabilidad</c> catalog
/// (issue migrar-campo-categoria-habilidades-a-tabla). No audit fields —
/// the catalog is immutable at runtime.
/// </summary>
public sealed record CategoriaHabilidadDto(
    Guid Id,
    string Codigo,
    string Nombre);