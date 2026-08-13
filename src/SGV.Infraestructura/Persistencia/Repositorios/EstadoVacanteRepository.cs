using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Dominio.Vacantes;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Read-only repository for the <c>EstadoVacante</c> catalog.
/// Does NOT extend <see cref="ReadOnlyRepository{TEntity, TDomain}"/>
/// because <c>EstadoVacante</c> inherits <see cref="EntidadBase"/>
/// (not <c>EntidadAuditable</c>), so the generic constraint of the base
/// class cannot be satisfied. Mirrors the pattern of
/// <see cref="NivelCargoRepository"/>.
/// </summary>
public sealed class EstadoVacanteRepository(SgvDbContext context) : IEstadoVacanteRepository
{
    public async Task<EstadoVacante?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context
            .Set<EstadoVacanteEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<EstadoVacante?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        var entity = await context
            .Set<EstadoVacanteEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Codigo == codigo, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<EstadoVacante>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await context
            .Set<EstadoVacanteEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Orden)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }
}