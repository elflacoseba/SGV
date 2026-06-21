using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// EF Core repository for PersonaHabilidad read and write operations.
/// Does NOT extend ReadOnlyRepository because PersonaHabilidadEntity inherits
/// EntityBase (not AuditableEntityBase), so the generic constraint cannot be satisfied.
/// </summary>
public sealed class PersonaSkillRepository(SgvDbContext context)
    : IPersonaSkillRepository
{
    private readonly SgvDbContext _context = context;

    public async Task<IReadOnlyList<PersonaHabilidad>> ListByPersonaIdAsync(
        Guid personaId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<PersonaHabilidadEntity>()
            .AsNoTracking()
            .Where(e => e.PersonaId == personaId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<PersonaSkillDetailDto>> ListDetailedByPersonaIdAsync(
        Guid personaId,
        CancellationToken cancellationToken = default)
    {
        return await _context
            .Set<PersonaHabilidadEntity>()
            .AsNoTracking()
            .Where(e => e.PersonaId == personaId)
            .Select(e => new PersonaSkillDetailDto(
                new HabilidadDto(
                    e.Habilidad.Id,
                    e.Habilidad.Codigo,
                    e.Habilidad.Nombre,
                    e.Habilidad.Descripcion,
                    e.Habilidad.Categoria),
                new NivelHabilidadDto(
                    e.NivelHabilidad.Id,
                    e.NivelHabilidad.Codigo,
                    e.NivelHabilidad.Nombre,
                    e.NivelHabilidad.ValorNumerico,
                    e.NivelHabilidad.Orden)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PersonaHabilidad?> GetByPersonaAndSkillAsync(
        Guid personaId,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<PersonaHabilidadEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.PersonaId == personaId && e.HabilidadId == skillId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task AddAsync(
        PersonaHabilidad personaHabilidad,
        CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(personaHabilidad);
        await _context.Set<PersonaHabilidadEntity>()
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(
        PersonaHabilidad personaHabilidad,
        CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(personaHabilidad);
        _context.Set<PersonaHabilidadEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(
        PersonaHabilidad personaHabilidad,
        CancellationToken cancellationToken = default)
    {
        var tracked = await _context.Set<PersonaHabilidadEntity>()
            .FindAsync(new object[] { personaHabilidad.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (tracked is not null)
        {
            _context.Set<PersonaHabilidadEntity>().Remove(tracked);
        }
    }

    // IReadOnlyRepository<PersonaHabilidad> members

    public async Task<PersonaHabilidad?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Set<PersonaHabilidadEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : PersistenceToDomainMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<PersonaHabilidad>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _context
            .Set<PersonaHabilidadEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(PersistenceToDomainMapper.ToDomain).ToArray();
    }
}
