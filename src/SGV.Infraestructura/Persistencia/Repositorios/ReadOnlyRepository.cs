using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Comun;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Generic read-only repository with AsNoTracking and soft-delete filtering.
/// </summary>
/// <typeparam name="T">Domain entity type. Must inherit from EntidadAuditable for soft-delete support.</typeparam>
public class ReadOnlyRepository<T>(SgvDbContext context) : IReadOnlyRepository<T>
    where T : EntidadAuditable
{
    protected SgvDbContext Context => context;

    /// <summary>
    /// Base query with AsNoTracking and IsDeleted filter.
    /// Entity-specific repositories can add Include/ThenInclude via <see cref="AgregarIncludes"/>.
    /// </summary>
    protected virtual IQueryable<T> Query => Context
        .Set<T>()
        .AsNoTracking()
        .Where(e => !e.IsDeleted);

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await Query.ToListAsync(cancellationToken);
    }
}
