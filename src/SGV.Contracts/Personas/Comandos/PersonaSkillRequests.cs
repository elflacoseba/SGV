namespace SGV.Contracts.Personas.Comandos;

/// <summary>
/// Request to assign or update a skill (Habilidad) for a Persona.
/// El <c>skillId</c> viaja en la ruta; este payload lleva únicamente la
/// referencia al nivel. Wire shape: <c>{ "nivelId": "&lt;guid&gt;" }</c>.
/// </summary>
public sealed record AsignarPersonaSkillRequest(
    Guid NivelId
);
