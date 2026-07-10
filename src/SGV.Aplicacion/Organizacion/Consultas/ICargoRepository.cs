using SGV.Aplicacion.Comun.Persistencia;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Repository contract for Cargo read and write operations.
/// </summary>
public interface ICargoRepository : IReadOnlyRepository<Cargo>
{
    /// <summary>
    /// Adds a new cargo.
    /// </summary>
    Task AddAsync(Cargo cargo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active, non-deleted cargo for update.
    /// </summary>
    Task<Cargo?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a cargo by id including soft-deleted ones for reactivation.
    /// </summary>
    Task<Cargo?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing cargo.
    /// </summary>
    Task UpdateAsync(Cargo cargo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a cargo.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted cargo.
    /// </summary>
    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active cargo already uses the given code.
    /// </summary>
    Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the specified cargo has any active (non-deleted) associated puestos.
    /// </summary>
    Task<bool> HasActivePuestosAsync(Guid cargoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a filtered, paginated set of cargos for the requested segment
    /// (active or soft-deleted) and the total count matching the filters.
    /// The optional <paramref name="sort"/> expression is applied server-side
    /// BEFORE pagination so page boundaries are consistent with the visible
    /// ordering (e.g. <c>nombre_desc</c> returns Z→A on every page).
    /// </summary>
    Task<(IReadOnlyList<Cargo> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        CargoSegmentoListado segmento = CargoSegmentoListado.Activas,
        CancellationToken cancellationToken = default);
}
