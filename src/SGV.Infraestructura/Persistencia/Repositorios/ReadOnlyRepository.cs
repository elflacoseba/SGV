using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Dominio.Comun;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Repositorio genérico de solo lectura que consulta entidades de persistencia
/// y devuelve modelos de Dominio sin seguimiento.
/// </summary>
/// <typeparam name="TPersistence">Tipo de entidad mapeada por EF Core.</typeparam>
/// <typeparam name="TDomain">Tipo de entidad del Dominio expuesto por el repositorio.</typeparam>
public abstract class ReadOnlyRepository<TPersistence, TDomain>(SgvDbContext context) : IReadOnlyRepository<TDomain>
    where TPersistence : AuditableEntityBase
    where TDomain : EntidadAuditable
{
    protected SgvDbContext Context => context;

    /// <summary>
    /// Consulta base con <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}(IQueryable{TEntity})"/>
    /// y filtro de borrado lógico.
    /// </summary>
    protected virtual IQueryable<TPersistence> Query => Context
        .Set<TPersistence>()
        .AsNoTracking()
        .Where(e => !e.IsDeleted);

    /// <summary>
    /// Convierte la entidad de persistencia en el modelo de Dominio equivalente.
    /// </summary>
    protected abstract TDomain MapToDomain(TPersistence entity);

    public virtual async Task<TDomain?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null ? null : MapToDomain(entity);
    }

    public virtual async Task<IReadOnlyList<TDomain>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query.ToListAsync(cancellationToken);
        return entities.Select(MapToDomain).ToArray();
    }
}
