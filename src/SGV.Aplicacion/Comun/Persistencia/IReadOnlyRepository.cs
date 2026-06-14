namespace SGV.Aplicacion.Comun.Persistencia;

/// <summary>
/// Generic read-only repository contract for querying domain entities.
/// Implementations should use AsNoTracking and apply soft-delete/active filters.
/// </summary>
/// <typeparam name="T">Domain entity type.</typeparam>
public interface IReadOnlyRepository<T> where T : class
{
    /// <summary>
    /// Retrieves an entity by its identifier.
    /// Returns null when no matching entity is found.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities, applying default filters (IsDeleted=false, IsActive=true where applicable).
    /// </summary>
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default);
}
