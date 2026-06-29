using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Typed API client abstraction used by the unidades organizativas web module.
/// </summary>
public interface IUnidadOrganizativaApiClient
{
    /// <summary>
    /// Queries a paged list of unidades organizativas.
    /// </summary>
    Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(UnidadOrganizativaListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single unidad organizativa by its identifier.
    /// </summary>
    Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the hierarchical tree of unidades organizativas.
    /// </summary>
    Task<IReadOnlyList<UnidadOrganizativaTreeNodeDto>> GetTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tipos de unidad organizativa.
    /// </summary>
    Task<IReadOnlyList<TipoUnidadOrganizativaDto>> GetTiposAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new unidad organizativa.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> CreateAsync(CrearUnidadOrganizativaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing unidad organizativa.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> UpdateAsync(Guid id, ActualizarUnidadOrganizativaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the parent of a unidad organizativa.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> ChangeParentAsync(Guid id, CambiarUnidadPadreRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a unidad organizativa.
    /// </summary>
    Task<UnidadOrganizativaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously deleted (soft-deleted) unidad organizativa.
    /// Returns success with the reactivated DTO, or a failure with NotFound/Conflict error.
    /// </summary>
    Task<UnidadOrganizativaCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
