using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;
using SGV.Infraestructura.Seguridad;

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
        Guid tipoDocumentoId,
        string numeroDocumento,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<PersonaEntity>()
            .AnyAsync(p =>
                p.TipoDocumentoId == tipoDocumentoId &&
                p.NumeroDocumento == numeroDocumento &&
                p.IsActive &&
                !p.IsDeleted &&
                p.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<Persona> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        PersonaSegmentoListado segmento = PersonaSegmentoListado.Activas,
        CancellationToken cancellationToken = default,
        bool? soloSinUsuario = null)
    {
        IQueryable<PersonaEntity> query = Context
            .Set<PersonaEntity>()
            .AsNoTracking()
            .Where(p => segmento == PersonaSegmentoListado.Activas
                ? (p.IsActive && !p.IsDeleted)
                : (!p.IsActive && p.IsDeleted));

        // soloSinUsuario=true + Eliminadas → cortocircuito (no anti-join) por
        // contrato de REQ-PM-01. Mantenemos el conteo 0 sin tocar la query
        // ni invocar joins adicionales.
        if (soloSinUsuario == true && segmento == PersonaSegmentoListado.Eliminadas)
        {
            return (Array.Empty<Persona>(), 0);
        }

        if (soloSinUsuario == true)
        {
            // Anti-join semántico contra AspNetUsers.PersonaId. Una persona
            // sólo califica si NO existe ningún Identity user apuntando a ella.
            // EF traduce esto a WHERE NOT EXISTS (…), que usa el índice UNIQUE
            // IX_AspNetUsers_PersonaId — sin sort ni temp table.
            query = query.Where(p => !Context
                .Set<SgvIdentityUser>()
                .Any(u => u.PersonaId == p.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                (p.Legajo != null && p.Legajo.Contains(search)) ||
                p.Nombres.Contains(search) ||
                p.Apellidos.Contains(search) ||
                (p.Email != null && p.Email.Contains(search)) ||
                (p.NumeroDocumento != null && p.NumeroDocumento.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // El sort se aplica ANTES del Skip/Take para que la paginación respete
        // el orden visible (REQ-CM-01). Valores soportados (8): legajo_asc/desc,
        // apellidos_asc/desc, nombres_asc/desc, email_asc/desc. Cualquier otro
        // valor cae al orden por defecto (apellidos_asc) para preservar
        // contratos existentes y mantener consistencia con Cargos.
        var ordered = ApplySort(query, sort);

        var entities = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    private static IOrderedQueryable<PersonaEntity> ApplySort(IQueryable<PersonaEntity> query, string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "legajo_asc" => query.OrderBy(p => p.Legajo),
            "legajo_desc" => query.OrderByDescending(p => p.Legajo),
            "apellidos_asc" => query.OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres),
            "apellidos_desc" => query.OrderByDescending(p => p.Apellidos).ThenByDescending(p => p.Nombres),
            "nombres_asc" => query.OrderBy(p => p.Nombres),
            "nombres_desc" => query.OrderByDescending(p => p.Nombres),
            "email_asc" => query.OrderBy(p => p.Email),
            "email_desc" => query.OrderByDescending(p => p.Email),
            "documento_asc" => query.OrderBy(p => p.NumeroDocumento),
            "documento_desc" => query.OrderByDescending(p => p.NumeroDocumento),
            _ => query.OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres)
        };
    }
}
