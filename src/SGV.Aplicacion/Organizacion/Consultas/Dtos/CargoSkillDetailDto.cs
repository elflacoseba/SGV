using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// GET-only detailed DTO for a Cargo-Habilidad association.
/// Provides nested <c>skill</c> and <c>nivel</c> catalog data with full details.
/// </summary>
public sealed record CargoSkillDetailDto(
    HabilidadDto Skill,
    NivelHabilidadDto Nivel
);
