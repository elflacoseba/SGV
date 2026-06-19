using SGV.Aplicacion.Comun.Persistencia;
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
}
