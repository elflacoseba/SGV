using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only query service for NivelCargo catalog.
/// </summary>
public interface INivelCargoServicioConsulta
{
    /// <summary>
    /// Returns all catalog rows as DTOs.
    /// </summary>
    Task<IReadOnlyList<NivelCargoDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single catalog row by its identifier, or null if not found.
    /// </summary>
    Task<NivelCargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
