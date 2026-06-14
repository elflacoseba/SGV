using SGV.Aplicacion.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only query service for Habilidades.
/// </summary>
public interface IHabilidadServicioConsulta
{
    /// <summary>
    /// Returns all active skills as DTOs.
    /// </summary>
    Task<IReadOnlyList<HabilidadDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single skill by its identifier, or null if not found.
    /// </summary>
    Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
