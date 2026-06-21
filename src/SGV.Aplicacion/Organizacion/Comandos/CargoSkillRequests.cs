namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Request to assign or update a required skill (Habilidad) for a Cargo.
/// The <c>skillId</c> is passed as a route parameter; this payload carries
/// only the level reference.
/// </summary>
public sealed record AsignarCargoSkillRequest(
    Guid NivelId
);
