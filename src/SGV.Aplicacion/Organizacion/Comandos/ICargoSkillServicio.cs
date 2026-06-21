using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Application service for managing skill assignments on Cargo.
/// </summary>
public interface ICargoSkillServicio
{
    /// <summary>
    /// Lists all skills assigned to the specified cargo.
    /// </summary>
    Task<IReadOnlyList<CargoSkillDto>> ListAsync(Guid cargoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a cargo-skill assignment.
    /// Validates that the cargo, habilidad, and nivel exist before persisting.
    /// </summary>
    Task<CargoSkillCommandResult> UpsertAsync(
        Guid cargoId,
        Guid skillId,
        AsignarCargoSkillRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically removes a cargo-skill assignment.
    /// </summary>
    Task<CargoSkillCommandResult> DeleteAsync(
        Guid cargoId,
        Guid skillId,
        CancellationToken cancellationToken = default);
}
