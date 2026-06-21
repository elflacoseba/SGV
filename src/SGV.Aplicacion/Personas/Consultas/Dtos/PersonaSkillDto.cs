namespace SGV.Aplicacion.Personas.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for a Persona-Habilidad association.
/// Exposes <c>skillId</c> and <c>nivelId</c> per the consumer contract,
/// hiding internal property names (NivelHabilidadId).
/// </summary>
public sealed record PersonaSkillDto(
    Guid SkillId,
    Guid NivelId
);
