using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only query service for Cargos.
/// </summary>
public interface ICargoServicioConsulta
{
    /// <summary>
    /// Returns all active roles as DTOs.
    /// </summary>
    Task<IReadOnlyList<CargoDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single role by its identifier, or null if not found.
    /// </summary>
    Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
