using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;

namespace SGV.Aplicacion.Habilidades.Consultas;

/// <summary>
/// Repository contract for Habilidad read and write operations.
/// </summary>
public interface IHabilidadRepository : IReadOnlyRepository<Habilidad>
{
    /// <summary>
    /// Adds a new habilidad.
    /// </summary>
    Task AddAsync(Habilidad habilidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an active, non-deleted habilidad for update.
    /// </summary>
    Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a habilidad by id including soft-deleted ones for reactivation.
    /// </summary>
    Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing habilidad.
    /// </summary>
    Task UpdateAsync(Habilidad habilidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes (deactivates) a habilidad.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a previously soft-deleted habilidad.
    /// </summary>
    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an active habilidad already uses the given code.
    /// </summary>
    Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a filtered, paginated set of habilidades for the requested
    /// segment (active or soft-deleted) and the total count matching the
    /// filters. The optional <paramref name="sort"/> expression is applied
    /// server-side BEFORE pagination so page boundaries are consistent with
    /// the visible ordering (e.g. <c>nombre_desc</c> returns Z→A on every
    /// page).
    /// </summary>
    Task<(IReadOnlyList<Habilidad> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        HabilidadSegmentoListado segmento = HabilidadSegmentoListado.Activas,
        CancellationToken cancellationToken = default);
}
