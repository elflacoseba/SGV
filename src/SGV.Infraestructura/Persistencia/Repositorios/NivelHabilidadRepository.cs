using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Read-only repository for NivelHabilidad catalog.
/// Does NOT extend ReadOnlyRepository because NivelHabilidadEntity inherits
/// EntityBase (not AuditableEntityBase), so the generic constraint cannot be satisfied.
/// </summary>
public sealed class NivelHabilidadRepository(SgvDbContext context)
    : INivelHabilidadRepository
{
    private readonly SgvDbContext _context = context;

    public async Task<NivelHabilidad?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<NivelHabilidadEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<NivelHabilidad>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<NivelHabilidadEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }
}
