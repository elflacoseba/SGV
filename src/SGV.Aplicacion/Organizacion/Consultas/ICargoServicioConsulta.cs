using SGV.Contracts.Organizacion.Consultas.Dtos;

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

    /// <summary>
    /// Returns a paginated, segmented set of cargos (active or deleted) using
    /// the application-layer <see cref="CargoListQuery"/>. <c>TotalCount</c>
    /// and pagination metadata come from the repository, not from a
    /// <c>GetAllAsync</c> in-memory snapshot.
    /// </summary>
    Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken cancellationToken = default);
}
