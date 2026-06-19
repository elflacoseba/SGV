using SGV.Aplicacion.Comun.Persistencia;
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
}
