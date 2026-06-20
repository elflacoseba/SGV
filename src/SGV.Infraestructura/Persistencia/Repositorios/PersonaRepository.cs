using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class PersonaRepository(SgvDbContext context)
    : ReadOnlyRepository<PersonaEntity, Persona>(context), IPersonaRepository
{
    protected override IQueryable<PersonaEntity> Query => base
        .Query
        .Where(p => p.IsActive);

    protected override Persona MapToDomain(PersonaEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    public override async Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Query
            .OrderBy(p => p.Apellidos)
            .ThenBy(p => p.Nombres)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }

    public async Task AddAsync(Persona persona, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(persona);
        await Context.Set<PersonaEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PersonaEntity>()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && !p.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PersonaEntity>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(Persona persona, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PersonaEntity>()
            .FirstOrDefaultAsync(p => p.Id == persona.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(PersonaEntity)} con id {persona.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, persona);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<PersonaEntity>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
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
            .Set<PersonaEntity>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.IsActive = true;
        entity.DeletedAt = null;
        entity.IsDeleted = false;
    }

    public async Task<bool> ExistsActiveLegajoAsync(
        string legajo,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<PersonaEntity>()
            .AnyAsync(p =>
                p.Legajo == legajo &&
                p.IsActive &&
                !p.IsDeleted &&
                p.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsActiveEmailAsync(
        string email,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<PersonaEntity>()
            .AnyAsync(p =>
                p.Email == email &&
                p.IsActive &&
                !p.IsDeleted &&
                p.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsActiveDocumentoAsync(
        string tipoDocumento,
        string numeroDocumento,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<PersonaEntity>()
            .AnyAsync(p =>
                p.TipoDocumento == tipoDocumento &&
                p.NumeroDocumento == numeroDocumento &&
                p.IsActive &&
                !p.IsDeleted &&
                p.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
