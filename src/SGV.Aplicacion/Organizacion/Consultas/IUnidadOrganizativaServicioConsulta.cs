using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only query service for Unidades Organizativas.
/// </summary>
public interface IUnidadOrganizativaServicioConsulta
{
    /// <summary>
    /// Returns all active organizational units as DTOs.
    /// </summary>
    Task<IReadOnlyList<UnidadOrganizativaDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single organizational unit by its identifier, or null if not found.
    /// </summary>
    Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated, filtered list of active organizational units.
    /// </summary>
    Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(
        UnidadOrganizativaQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the hierarchical tree of active organizational units together
    /// with the list of node ids whose padre chain participates in at least
    /// one cycle (issue #277).
    /// </summary>
    /// <returns>
    /// A response with <c>Arbol</c> containing the non-cyclic sub-trees and
    /// <c>NodosConCiloDetectado</c> listing every node id that would be
    /// reached by a cyclic padre edge. Always non-null; the cycle list is
    /// empty when the dataset is acyclic.
    /// </returns>
    Task<UnidadOrganizativaArbolResponse> GetTreeAsync(
        CancellationToken cancellationToken = default);
}
