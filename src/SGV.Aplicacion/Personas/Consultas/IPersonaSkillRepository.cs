using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Consultas;

/// <summary>
/// Repository contract for PersonaHabilidad (skill assignment to Persona)
/// read and write operations.
/// </summary>
public interface IPersonaSkillRepository : IReadOnlyRepository<PersonaHabilidad>
{
    /// <summary>
    /// Retrieves all skill assignments for a given persona.
    /// </summary>
    Task<IReadOnlyList<PersonaHabilidad>> ListByPersonaIdAsync(Guid personaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all skill assignments for a given persona with full nested
    /// habilidad and nivel data projected in a single query.
    /// </summary>
    Task<IReadOnlyList<PersonaSkillDetailDto>> ListDetailedByPersonaIdAsync(Guid personaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific skill assignment by persona and skill.
    /// Returns null when no assignment exists.
    /// </summary>
    Task<PersonaHabilidad?> GetByPersonaAndSkillAsync(Guid personaId, Guid skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new skill assignment.
    /// </summary>
    Task AddAsync(PersonaHabilidad personaHabilidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing skill assignment.
    /// </summary>
    Task UpdateAsync(PersonaHabilidad personaHabilidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically removes a skill assignment.
    /// </summary>
    Task DeleteAsync(PersonaHabilidad personaHabilidad, CancellationToken cancellationToken = default);
}
