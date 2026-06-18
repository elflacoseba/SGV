using SGV.Aplicacion.Organizacion.Consultas.Dtos;

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
/// Returns the hierarchical tree of active organizational units.
/// </summary>
Task<IReadOnlyList<UnidadOrganizativaTreeNodeDto>> GetTreeAsync(
    CancellationToken cancellationToken = default);
}
