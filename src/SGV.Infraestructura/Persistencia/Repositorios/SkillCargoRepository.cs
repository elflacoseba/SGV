using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// EF Core repository for the readonly Habilidad → Cargos subresource. Does
/// NOT extend <see cref="ReadOnlyRepository{TPersistence, TDomain}"/> because
/// <see cref="CargoHabilidadEntity"/> inherits <see cref="EntityBase"/> (not
/// <see cref="AuditableEntityBase"/>), so the generic constraint cannot be
/// satisfied — same pattern as
/// <see cref="CargoSkillRepository"/>.
/// </summary>
public sealed class SkillCargoRepository(SgvDbContext context)
    : ISkillCargoRepository
{
    private readonly SgvDbContext _context = context;

    public async Task<(IReadOnlyList<SkillCargoDetailDto> Items, int TotalCount)> ListDetailedBySkillIdAsync(
        Guid habilidadId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default)
    {
        // PR-WU-A: la proyección del subrecurso popula explícitamente los
        // identificadores del vínculo y los flags del mismo (CargoId,
        // NivelRequeridoId, Ponderacion, EsObligatoria) además de los
        // catálogos anidados (cargo, nivel), en una única query sin N+1
        // (skill-cargo-query-contract Req 1).
        IQueryable<CargoHabilidadEntity> baseQuery = _context
            .Set<CargoHabilidadEntity>()
            .AsNoTracking()
            .Where(e => e.HabilidadId == habilidadId);

        // El filtro de segmento aplica sobre Cargo (que hereda IsDeleted de
        // AuditableEntityBase). CargoHabilidad no tiene soft-delete propio,
        // así que el segmento se materializa vía Cargo.IsDeleted /
        // Cargo.IsActive.
        baseQuery = query.Segmento == HabilidadSegmentoListado.Eliminadas
            ? baseQuery.Where(e => e.Cargo.IsDeleted && !e.Cargo.IsActive)
            : baseQuery.Where(e => !e.Cargo.IsDeleted && e.Cargo.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search;
            baseQuery = baseQuery.Where(e =>
                e.Cargo.Codigo.Contains(search) ||
                e.Cargo.Nombre.Contains(search));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Gotcha Pomelo: ordenar ANTES de proyectar al DTO posicional.
        // Pomelo no traduce OrderBy aplicado a records posicionales; el
        // orden debe aplicarse sobre la entidad (CargoEntity.Codigo /
        // CargoEntity.Nombre) y la proyección al DTO en un Select
        // posterior (skill-cargo-query-contract risk #1).
        var ordered = ApplySort(baseQuery, query.Sort);

        var items = await ordered
            .Select(e => new SkillCargoDetailDto(
                new CargoDto(
                    e.Cargo.Id,
                    e.Cargo.Codigo,
                    e.Cargo.Nombre,
                    e.Cargo.Descripcion,
                    e.Cargo.NivelId,
                    e.Cargo.NivelCargo != null ? e.Cargo.NivelCargo.Nombre : null),
                new NivelHabilidadDto(
                    e.NivelRequerido.Id,
                    e.NivelRequerido.Codigo,
                    e.NivelRequerido.Nombre,
                    e.NivelRequerido.ValorNumerico,
                    e.NivelRequerido.Orden))
            {
                CargoId = e.CargoId,
                NivelRequeridoId = e.NivelRequeridoId,
                Ponderacion = e.Ponderacion,
                EsObligatoria = e.EsObligatoria,
                CargoEliminado = e.Cargo.IsDeleted
            })
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    private static IOrderedQueryable<CargoHabilidadEntity> ApplySort(
        IQueryable<CargoHabilidadEntity> query,
        string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => query.OrderByDescending(e => e.Cargo.Codigo),
            "codigo_asc" => query.OrderBy(e => e.Cargo.Codigo),
            "nombre_desc" => query.OrderByDescending(e => e.Cargo.Nombre),
            "nombre_asc" => query.OrderBy(e => e.Cargo.Nombre),
            _ => query.OrderBy(e => e.Cargo.Codigo)
        };
    }

    // IReadOnlyRepository<CargoHabilidad> members — required by the
    // interface but unused for the readonly subresource; mirror the
    // CargoSkillRepository behavior.

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