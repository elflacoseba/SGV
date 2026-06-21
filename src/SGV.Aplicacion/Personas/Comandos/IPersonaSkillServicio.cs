using SGV.Aplicacion.Personas.Consultas.Dtos;

namespace SGV.Aplicacion.Personas.Comandos;

/// <summary>
/// Application service for managing skill assignments on Persona.
/// </summary>
public interface IPersonaSkillServicio
{
    /// <summary>
    /// Lists all skills assigned to the specified persona with full nested data.
    /// </summary>
    Task<IReadOnlyList<PersonaSkillDetailDto>> ListAsync(Guid personaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a persona-skill assignment.
    /// Validates that the persona, habilidad, and nivel exist before persisting.
    /// </summary>
    Task<PersonaSkillCommandResult> UpsertAsync(
        Guid personaId,
        Guid skillId,
        AsignarPersonaSkillRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically removes a persona-skill assignment.
    /// </summary>
    Task<PersonaSkillCommandResult> DeleteAsync(
        Guid personaId,
        Guid skillId,
        CancellationToken cancellationToken = default);
}
