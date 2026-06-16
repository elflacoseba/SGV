using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only query service for TipoUnidadOrganizativa catalog.
/// </summary>
public interface ITipoUnidadOrganizativaServicioConsulta
{
    /// <summary>
    /// Returns all catalog rows as DTOs.
    /// </summary>
    Task<IReadOnlyList<TipoUnidadOrganizativaDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single catalog row by its identifier, or null if not found.
    /// </summary>
    Task<TipoUnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
