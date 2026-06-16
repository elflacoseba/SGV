using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Read-only repository for TipoUnidadOrganizativa catalog.
/// Does NOT extend ReadOnlyRepository because TipoUnidadOrganizativa inherits
/// EntidadBase (not EntidadAuditable), so the generic constraint cannot be satisfied.
/// </summary>
public sealed class TipoUnidadOrganizativaRepository(SgvDbContext context)
    : ITipoUnidadOrganizativaRepository
{
    private readonly SgvDbContext _context = context;

    public async Task<TipoUnidadOrganizativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<TipoUnidadOrganizativaEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<TipoUnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<TipoUnidadOrganizativaEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }
}
