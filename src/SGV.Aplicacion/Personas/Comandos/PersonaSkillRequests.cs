namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Request to assign or update a skill (Habilidad) for a Persona.
/// The <c>skillId</c> is passed as a route parameter; this payload carries
/// only the level reference.
/// </summary>
public sealed record AsignarPersonaSkillRequest(
    Guid NivelId
);
