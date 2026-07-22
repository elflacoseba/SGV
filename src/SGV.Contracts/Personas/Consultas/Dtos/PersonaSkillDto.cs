namespace SGV.Contracts.Personas.Consultas.Dtos;

/// <summary>
/// Consumer-safe DTO for a Persona-Habilidad association (write contract).
/// Expone <c>skillId</c> and <c>nivelId</c> per the active wire shape.
/// Vive en <c>SGV.Contracts</c> para que <c>SGV.Api</c> y <c>SGV.Web</c>
/// lo compartan sin duplicar DTOs en <c>SGV.Aplicacion</c>.
/// </summary>
public sealed record PersonaSkillDto(
    Guid SkillId,
    Guid NivelId
);
