using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class CargoRepository(SgvDbContext context)
    : ReadOnlyRepository<CargoEntity, Cargo>(context), ICargoRepository
{
    protected override IQueryable<CargoEntity> Query => base
        .Query
        .Include(c => c.NivelCargo)
        .Where(c => c.IsActive);

    protected override Cargo MapToDomain(CargoEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    public override async Task<IReadOnlyList<Cargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .OrderBy(c => c.Codigo)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }

    public async Task AddAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(cargo);
        await Context.Set<CargoEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Cargo?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .Include(c => c.NivelCargo)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && !c.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Cargo?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .Include(c => c.NivelCargo)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .FirstOrDefaultAsync(c => c.Id == cargo.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(CargoEntity)} con id {cargo.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, cargo);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.IsActive = false;
        entity.DeletedAt = DateTime.UtcNow;
        entity.IsDeleted = true;
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<CargoEntity>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.IsActive = true;
        entity.DeletedAt = null;
        entity.IsDeleted = false;
    }

    public async Task<bool> ExistsActiveCodeAsync(
        string codigo,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<CargoEntity>()
            .AnyAsync(c =>
                c.Codigo == codigo &&
                c.IsActive &&
                !c.IsDeleted &&
                c.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasActivePuestosAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<PuestoEntity>()
            .AnyAsync(
                p => p.CargoId == cargoId && p.IsActive && !p.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<Cargo> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        CargoSegmentoListado segmento = CargoSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CargoEntity> query = Context
            .Set<CargoEntity>()
            .AsNoTracking()
            .Where(c => segmento == CargoSegmentoListado.Activas
                ? (c.IsActive && !c.IsDeleted)
                : (!c.IsActive && c.IsDeleted))
            .Include(c => c.NivelCargo);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.Codigo.Contains(search) ||
                c.Nombre.Contains(search) ||
                (c.Descripcion != null && c.Descripcion.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // El sort se aplica ANTES del Skip/Take para que la paginación respete
        // el orden visible (REQ-CM-01). Valores soportados:
        // codigo_asc / codigo_desc / nombre_asc / nombre_desc /
        // nivel_asc / nivel_desc. Cualquier otro valor cae al orden por defecto
        // por Codigo asc para preservar contratos existentes.
        var ordered = ApplySort(query, sort);

        var entities = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    private static IOrderedQueryable<CargoEntity> ApplySort(IQueryable<CargoEntity> query, string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => query.OrderByDescending(c => c.Codigo),
            "codigo_asc" => query.OrderBy(c => c.Codigo),
            "nombre_desc" => query.OrderByDescending(c => c.Nombre),
            "nombre_asc" => query.OrderBy(c => c.Nombre),
            "nivel_desc" => query.OrderByDescending(c => c.NivelCargo != null ? c.NivelCargo.Nombre : string.Empty),
            "nivel_asc" => query.OrderBy(c => c.NivelCargo != null ? c.NivelCargo.Nombre : string.Empty),
            _ => query.OrderBy(c => c.Codigo)
        };
    }
}
