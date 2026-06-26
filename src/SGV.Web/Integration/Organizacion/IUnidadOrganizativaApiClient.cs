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
    /// Deletes a unidad organizativa.
    /// </summary>
    Task<UnidadOrganizativaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
