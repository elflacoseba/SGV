using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// GET-only detailed DTO for a Persona-Habilidad association.
/// Provides nested <c>skill</c> and <c>nivel</c> catalog data with full
/// details; el wire shape observable es <c>{skill: {...}, nivel: {...}}</c>
/// y NO introduce campos planos <c>skillId</c>/<c>nivelId</c> en la raíz.
/// </summary>
public sealed record PersonaSkillDetailDto(
    HabilidadDto Skill,
    NivelHabilidadDto Nivel
);
