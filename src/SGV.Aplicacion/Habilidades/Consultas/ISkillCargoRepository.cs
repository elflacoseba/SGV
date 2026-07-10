using SGV.Aplicacion.Comun.Persistencia;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Repository contract for the readonly Habilidad → Cargos subresource
/// (skill-cargo-query-contract). Returns the projection already materialized
/// as <see cref="SkillCargoDetailDto"/> so the application service does not
/// touch EF Core again.
/// </summary>
public interface ISkillCargoRepository : IReadOnlyRepository<CargoHabilidad>
{
    /// <summary>
    /// Returns a paginated, segmented set of cargos associated to the given
    /// habilidad, projected as <see cref="SkillCargoDetailDto"/> in a single
    /// query without N+1, plus the total count matching the filters.
    /// </summary>
    /// <remarks>
    /// Ordering is applied server-side BEFORE pagination so page boundaries
    /// stay consistent with the visible ordering. The segment filter is
    /// applied to the <c>Cargo</c> entity (which carries soft-delete via
    /// <c>IsDeleted</c>/<c>IsActive</c>); <c>CargoHabilidad</c> has no
    /// soft-delete of its own.
    /// </remarks>
    /// <param name="habilidadId">Identifier of the parent habilidad.</param>
    /// <param name="query">Pagination, search, sort and segment parameters.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    Task<(IReadOnlyList<SkillCargoDetailDto> Items, int TotalCount)> ListDetailedBySkillIdAsync(
        Guid habilidadId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default);
}