using SGV.Aplicacion.Personas.Consultas.Dtos;

namespace SGV.Aplicacion.Personas.Consultas;

/// <summary>
/// Read-only query service for Personas.
/// </summary>
public interface IPersonaServicioConsulta
{
    /// <summary>
    /// Returns all active personas as DTOs.
    /// </summary>
    Task<IReadOnlyList<PersonaDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single persona by its identifier, or null if not found or inactive.
    /// </summary>
    Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
