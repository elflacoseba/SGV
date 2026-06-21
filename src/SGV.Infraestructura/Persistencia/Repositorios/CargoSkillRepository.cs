using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// EF Core repository for CargoHabilidad read and write operations.
/// Does NOT extend ReadOnlyRepository because CargoHabilidadEntity inherits
/// EntityBase (not AuditableEntityBase), so the generic constraint cannot be satisfied.
/// </summary>
public sealed class CargoSkillRepository(SgvDbContext context)
    : ICargoSkillRepository
{
    private readonly SgvDbContext _context = context;

    public async Task<IReadOnlyList<CargoHabilidad>> ListByCargoIdAsync(
        Guid cargoId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<CargoHabilidadEntity>()
            .AsNoTracking()
            .Where(e => e.CargoId == cargoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<CargoSkillDetailDto>> ListDetailedByCargoIdAsync(
        Guid cargoId,
        CancellationToken cancellationToken = default)
    {
        return await _context
            .Set<CargoHabilidadEntity>()
            .AsNoTracking()
            .Where(e => e.CargoId == cargoId)
            .Select(e => new CargoSkillDetailDto(
                e.HabilidadId,
                e.NivelRequeridoId,
                new HabilidadDto(
                    e.Habilidad.Id,
                    e.Habilidad.Codigo,
                    e.Habilidad.Nombre,
                    e.Habilidad.Descripcion,
                    e.Habilidad.Categoria),
                new NivelHabilidadDto(
                    e.NivelRequerido.Id,
                    e.NivelRequerido.Codigo,
                    e.NivelRequerido.Nombre,
                    e.NivelRequerido.ValorNumerico,
                    e.NivelRequerido.Orden)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CargoHabilidad?> GetByCargoAndSkillAsync(
        Guid cargoId,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<CargoHabilidadEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.CargoId == cargoId && e.HabilidadId == skillId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task AddAsync(
        CargoHabilidad cargoHabilidad,
        CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(cargoHabilidad);
        await _context.Set<CargoHabilidadEntity>()
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(
        CargoHabilidad cargoHabilidad,
        CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(cargoHabilidad);
        _context.Set<CargoHabilidadEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(
        CargoHabilidad cargoHabilidad,
        CancellationToken cancellationToken = default)
    {
        var tracked = await _context.Set<CargoHabilidadEntity>()
            .FindAsync(new object[] { cargoHabilidad.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (tracked is not null)
        {
            _context.Set<CargoHabilidadEntity>().Remove(tracked);
        }
    }

    // IReadOnlyRepository<CargoHabilidad> members

    public async Task<CargoHabilidad?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<CargoHabilidadEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<CargoHabilidad>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<CargoHabilidadEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }
}
