using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Read-only repository for the <c>CategoriaHabilidad</c> catalog
/// (issue migrar-campo-categoria-habilidades-a-tabla).
///
/// No <c>IsActive</c> filter (catalog is immutable per
/// REQ-SPA-EVOLUTION-001 condición #1). No <c>IsDeleted</c> filter
/// (catalog does not have that column).
/// </summary>
public sealed class CategoriaHabilidadRepository(SgvDbContext context)
    : ICategoriaHabilidadRepository
{
    public async Task<CategoriaHabilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context
            .Set<CategoriaHabilidadEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<CategoriaHabilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await context
            .Set<CategoriaHabilidadEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Nombre)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }
}