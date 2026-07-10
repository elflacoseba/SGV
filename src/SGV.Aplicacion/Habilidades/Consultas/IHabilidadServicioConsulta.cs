using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Habilidades.Consultas.Dtos;

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

    /// <summary>
    /// Returns a paginated, segmented set of habilidades (active or deleted)
    /// using the application-layer <see cref="HabilidadListQuery"/>.
    /// <c>TotalCount</c> and pagination metadata come from the repository,
    /// not from a <c>GetAllAsync</c> in-memory snapshot.
    /// </summary>
    Task<PagedResult<HabilidadDto>> QueryAsync(HabilidadListQuery query, CancellationToken cancellationToken = default);
}
