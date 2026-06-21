using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Repository contract for CargoHabilidad (skill assignment to Cargo)
/// read and write operations.
/// </summary>
public interface ICargoSkillRepository : IReadOnlyRepository<CargoHabilidad>
{
    /// <summary>
    /// Retrieves all skill assignments for a given cargo.
    /// </summary>
    Task<IReadOnlyList<CargoHabilidad>> ListByCargoIdAsync(Guid cargoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific skill assignment by cargo and skill.
    /// Returns null when no assignment exists.
    /// </summary>
    Task<CargoHabilidad?> GetByCargoAndSkillAsync(Guid cargoId, Guid skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new skill assignment.
    /// </summary>
    Task AddAsync(CargoHabilidad cargoHabilidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing skill assignment.
    /// </summary>
    Task UpdateAsync(CargoHabilidad cargoHabilidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically removes a skill assignment.
    /// </summary>
    Task DeleteAsync(CargoHabilidad cargoHabilidad, CancellationToken cancellationToken = default);
}
