using SGV.Aplicacion.Comun.Persistencia;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Repository contract for Puesto read and write operations.
/// </summary>
public interface IPuestoRepository : IReadOnlyRepository<Puesto>
{
    /// <summary>
    /// Adds a new puesto.
    /// </summary>
    Task AddAsync(Puesto puesto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active, non-deleted puesto for update.
    /// Includes UnidadOrganizativa and Cargo navigation properties.
    /// </summary>
    Task<Puesto?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a puesto by id including soft-deleted ones for reactivation.
    /// </summary>
    Task<Puesto?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing puesto.
    /// </summary>
    Task UpdateAsync(Puesto puesto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a puesto.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted puesto.
    /// </summary>
    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active puesto already uses the given code.
    /// </summary>
    Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a filtered, paginated set of puestos for the requested segment
    /// (active or soft-deleted) and the total count matching the filters.
    /// The optional <paramref name="sort"/> expression is applied server-side
    /// BEFORE pagination so page boundaries are consistent with the visible
    /// ordering (e.g. <c>nombre_desc</c> returns Z→A on every page).
    /// </summary>
    Task<(IReadOnlyList<Puesto> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        PuestoSegmentoListado segmento = PuestoSegmentoListado.Activas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active, non-deleted puestos that have NO active Ocupacion
    /// (<c>IsDeleted = 0 AND FechaFin IS NULL</c>) AND NO open Vacante
    /// (<c>IsDeleted = 0 AND FechaCierre IS NULL</c>). Defense-in-depth
    /// query used by <c>GET /api/v1/puestos/disponibles</c>.
    /// </summary>
    Task<IReadOnlyList<Puesto>> ListarDisponiblesAsync(CancellationToken cancellationToken = default);
}
