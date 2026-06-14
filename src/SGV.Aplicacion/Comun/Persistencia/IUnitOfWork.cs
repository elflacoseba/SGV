namespace SGV.Aplicacion.Comun.Persistencia;

/// <summary>
/// Unit of Work abstraction over EF Core's SaveChanges.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
