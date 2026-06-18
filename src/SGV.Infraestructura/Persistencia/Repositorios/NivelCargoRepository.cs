using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Read-only repository for NivelCargo catalog.
/// Does NOT extend ReadOnlyRepository because NivelCargo inherits
/// EntidadBase (not EntidadAuditable), so the generic constraint cannot be satisfied.
/// </summary>
public sealed class NivelCargoRepository(SgvDbContext context)
    : INivelCargoRepository
{
    private readonly SgvDbContext _context = context;

    public async Task<NivelCargo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<NivelCargoEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<NivelCargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<NivelCargoEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Codigo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }

    public async Task<NivelCargo?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<NivelCargoEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Codigo == codigo, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }
}
