using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only query service for Puestos.
/// </summary>
public interface IPuestoServicioConsulta
{
    /// <summary>
    /// Returns all active positions as DTOs with related entity summaries.
    /// </summary>
    Task<IReadOnlyList<PuestoDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single position by its identifier with related entity summaries, or null if not found.
    /// </summary>
    Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
