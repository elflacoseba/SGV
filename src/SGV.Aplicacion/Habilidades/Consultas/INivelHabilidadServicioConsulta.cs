using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Habilidades.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only query service for the NivelHabilidad catalog. Mirrors the
/// pattern of <see cref="INivelCargoServicioConsulta"/>: list + get-by-id
/// returning <see cref="NivelHabilidadDto"/>.
/// </summary>
public interface INivelHabilidadServicioConsulta
{
    /// <summary>
    /// Returns all catalog rows as DTOs.
    /// </summary>
    Task<IReadOnlyList<NivelHabilidadDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single catalog row by its identifier, or null if not found.
    /// </summary>
    Task<NivelHabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}