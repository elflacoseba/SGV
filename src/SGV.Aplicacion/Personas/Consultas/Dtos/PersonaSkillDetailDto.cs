using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Personas.Consultas.Dtos;

/// <summary>
/// GET-only detailed DTO for a Persona-Habilidad association.
/// Preserves <c>skillId</c> and <c>nivelId</c> from the write contract
/// and adds nested <c>skill</c> and <c>nivel</c> catalog data.
/// </summary>
public sealed record PersonaSkillDetailDto(
    Guid SkillId,
    Guid NivelId,
    HabilidadDto Skill,
    NivelHabilidadDto Nivel
);
