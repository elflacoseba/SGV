using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Personas.Consultas.Dtos;

/// <summary>
/// GET-only detailed DTO for a Persona-Habilidad association.
/// Provides nested <c>skill</c> and <c>nivel</c> catalog data with full details.
/// </summary>
public sealed record PersonaSkillDetailDto(
    HabilidadDto Skill,
    NivelHabilidadDto Nivel
);
