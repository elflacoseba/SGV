using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Read-only query service for cargos associated to a habilidad
/// (GET-only subresource of <c>SkillsController</c>). Powers the
/// <c>GET /api/v1/skills/{skillId}/cargos</c> endpoint required by
/// <c>skill-cargo-query-contract</c>.
/// </summary>
public interface ISkillCargoServicioConsulta
{
    /// <summary>
    /// Returns a paginated, segmented set of cargos associated to the given
    /// habilidad. <c>TotalCount</c> and pagination metadata come from the
    /// repository, not from an in-memory snapshot.
    /// </summary>
    /// <param name="habilidadId">Identifier of the parent habilidad.</param>
    /// <param name="query">Pagination, search, sort and segment parameters
    /// (already normalized by the controller).</param>
    /// <param name="cancellationToken">Token de cancelación de la
    /// solicitud.</param>
    Task<PagedResult<SkillCargoDetailDto>> ListarCargosAsync(
        Guid habilidadId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default);
}