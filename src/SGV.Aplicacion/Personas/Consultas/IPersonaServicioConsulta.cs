using SGV.Contracts.Personas.Consultas.Dtos;

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

    /// <summary>
    /// Returns a paginated, segmented set of personas (active or deleted) using
    /// the application-layer <see cref="PersonaListQuery"/>. <c>TotalCount</c>
    /// and pagination metadata come from the repository, not from a
    /// <c>ListAllAsync</c> in-memory snapshot.
    /// </summary>
    Task<PersonaListadoDto> ListarAsync(PersonaListQuery query, CancellationToken cancellationToken = default);
}
