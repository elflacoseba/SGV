using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas.Dtos;

/// <summary>
/// GET-only detailed DTO for a Cargo-Habilidad association.
/// Preserves <c>skillId</c> and <c>nivelId</c> from the write contract
/// and adds nested <c>skill</c> and <c>nivel</c> catalog data.
/// </summary>
public sealed record CargoSkillDetailDto(
    Guid SkillId,
    Guid NivelId,
    HabilidadDto Skill,
    NivelHabilidadDto Nivel
);
