using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

public sealed class HabilidadRepository(SgvDbContext context)
    : ReadOnlyRepository<HabilidadEntity, Habilidad>(context), IHabilidadRepository
{
    /// <summary>
    /// Contrato de exposición de la navegación <see cref="HabilidadEntity.Categoria"/>
    /// (issue #311 — asimetría residual del tech debt cleanup #298).
    ///
    /// <para>
    /// Todo path público que materializa un <see cref="Habilidad"/> de
    /// dominio para consumo aguas arriba (servicios de consulta, comandos,
    /// UI, Web) debe devolver la navegación <c>Categoria</c> hidratada
    /// (LEFT JOIN contra el catálogo <c>CategoriasHabilidad</c>), de modo
    /// que cualquier consumidor que proyecte <c>CategoriaNombre</c> — ver
    /// por ejemplo <c>HabilidadServicioConsulta.MapToDto</c> o
    /// <c>HabilidadServicioComandos</c> cuando reporta
    /// <c>CategoriaInexistente</c> — reciba la navegación ya poblada y
    /// opere sobre un contrato uniforme. Si la habilidad no tiene
    /// categoría asignada, la navegación es <c>null</c> y
    /// <c>CategoriaNombre</c> queda en <c>null</c> — coherente con
    /// <see cref="Habilidad.CategoriaId"/> opcional.
    /// </para>
    ///
    /// <para>
    /// Excepción explícita: <see cref="ExistsCategoriaAsync"/> no carga
    /// <c>Categoria</c> porque su contrato es verificar la existencia de
    /// un id de catálogo, no devolver un agregado.
    /// </para>
    /// </summary>
    protected override IQueryable<HabilidadEntity> Query => base
        .Query
        .Where(h => h.IsActive);

    protected override Habilidad MapToDomain(HabilidadEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    /// <summary>
    /// Override del base <c>ReadOnlyRepository.GetByIdAsync</c> para garantizar
    /// que la navegación <see cref="HabilidadEntity.Categoria"/> se carga con un
    /// LEFT JOIN y el mapper de dominio puede proyectar <c>CategoriaNombre</c>
    /// en <c>HabilidadDto</c> (issue
    /// migrar-campo-categoria-habilidades-a-tabla). Sin esta carga,
    /// <c>GET /api/v1/skills/{id}</c> devolvía <c>CategoriaNombre = null</c>
    /// aunque la FK existiera, rompiendo REQ-CAT-07.
    /// </summary>
    public override async Task<Habilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Query
            .Include(h => h.Categoria)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public override async Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        // LEFT JOIN CategoriasHabilidad para proyectar CategoriaNombre y
        // popular la navegación Categoria (issue
        // migrar-campo-categoria-habilidades-a-tabla). Sin categoría: Categoria = null,
        // CategoriaNombre = null — coherente con CategoriaId nullable.
        var entities = await Query
            .Include(h => h.Categoria)
            .OrderBy(h => h.Codigo)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToArray();
    }

    public async Task AddAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(habilidad);
        await Context.Set<HabilidadEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Devuelve una <see cref="Habilidad"/> activa y no borrada para edición,
    /// hidratando la navegación <see cref="HabilidadEntity.Categoria"/> (ver
    /// el comentario de clase sobre el contrato de exposición de la navegación).
    /// Filtra por <c>IsActive &amp;&amp; !IsDeleted</c>; retorna <c>null</c>
    /// cuando no hay coincidencia.
    /// </summary>
    public async Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .Include(h => h.Categoria)
            .FirstOrDefaultAsync(h => h.Id == id && h.IsActive && !h.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    /// <summary>
    /// Devuelve una <see cref="Habilidad"/> incluyendo soft-deleted, hidratando
    /// la navegación <see cref="HabilidadEntity.Categoria"/> (ver el comentario
    /// de clase sobre el contrato de exposición de la navegación). No filtra
    /// por <c>IsActive</c> ni <c>IsDeleted</c>; se usa para flujos de
    /// reactivación desde la UI administrativa.
    /// </summary>
    public async Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .Include(h => h.Categoria)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    /// <summary>
    /// Persiste los scalar fields editables de una <see cref="Habilidad"/>
    /// existente. La carga del <see cref="HabilidadEntity"/> tracked incluye
    /// <c>Include(h =&gt; h.Categoria)</c> para preservar el contrato de
    /// exposición de la navegación declarado a nivel de clase (issue #311):
    /// el patch opera sobre scalar fields vía
    /// <c>DomainToPersistenceMapper.UpdateEntity</c>, pero EF Core adjunta la
    /// entidad completa para que posteriores lecturas vía ese mismo
    /// <see cref="SgvDbContext"/> encuentren la navegación hidratada — un
    /// consumidor que proyecte <c>CategoriaNombre</c> aguas arriba no tiene
    /// que asumir un contrato distinto al de los demás paths.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se lanza si no existe la <see cref="HabilidadEntity"/> para el id
    /// indicado (mismo contrato que las versiones previas; el cambio de
    /// esta firma es aditivo).
    /// </exception>
    public async Task UpdateAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .Include(h => h.Categoria)
            .FirstOrDefaultAsync(h => h.Id == habilidad.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException($"No se encontró la entidad {nameof(HabilidadEntity)} con id {habilidad.Id}.");
        }

        DomainToPersistenceMapper.UpdateEntity(entity, habilidad);
    }

    /// <summary>
    /// Soft-delete: marca la habilidad como inactiva y borrada lógicamente
    /// sin eliminar la fila. El registro permanece en la tabla y puede ser
    /// reactivado vía <see cref="ReactivateAsync"/>. Las asignaciones a
    /// cargos y personas no se ven afectadas (la FK sigue siendo válida).
    /// </summary>
    /// <remarks>
    /// El nombre <c>DeleteAsync</c> se mantiene por simetría con
    /// <see cref="EntityFrameworkCore.EntityState.Deleted"/> y con la
    /// convención de los demás repositorios del proyecto (Cargo, Persona,
    /// Puesto, UnidadOrganizativa). NO confundir con borrado físico:
    /// <c>EF Core</c> nunca emite <c>DELETE FROM Habilidades</c>.
    /// </remarks>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<HabilidadEntity>()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
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
            .Set<HabilidadEntity>()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
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
            .Set<HabilidadEntity>()
            .AnyAsync(h =>
                h.Codigo == codigo &&
                h.IsActive &&
                !h.IsDeleted &&
                h.Id != excludingId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Verifica si la categoría con el id indicado existe en el catálogo
    /// inmutable <c>CategoriasHabilidad</c>. No respeta <c>IsActive</c>
    /// (el catálogo es inmutable y no tiene soft-delete).
    /// </summary>
    public async Task<bool> ExistsCategoriaAsync(Guid categoriaId, CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<CategoriaHabilidadEntity>()
            .AsNoTracking()
            .AnyAsync(c => c.Id == categoriaId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<Habilidad> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        HabilidadSegmentoListado segmento = HabilidadSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        IQueryable<HabilidadEntity> query = Context
            .Set<HabilidadEntity>()
            .AsNoTracking()
            .Where(h => segmento == HabilidadSegmentoListado.Activas
                ? (h.IsActive && !h.IsDeleted)
                : (!h.IsActive && h.IsDeleted));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(h =>
                h.Codigo.Contains(search) ||
                h.Nombre.Contains(search) ||
                (h.Categoria != null && h.Categoria.Nombre.Contains(search)) ||
                (h.Descripcion != null && h.Descripcion.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // El sort se aplica ANTES del Skip/Take para que la paginación respete
        // el orden visible. Valores soportados: codigo_asc / codigo_desc /
        // nombre_asc / nombre_desc / categoria_asc / categoria_desc.
        // Cualquier otro valor cae al orden por defecto por Codigo asc.
        var ordered = ApplySort(query, sort);

        var entities = await ordered
            .Include(h => h.Categoria)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    private static IOrderedQueryable<HabilidadEntity> ApplySort(IQueryable<HabilidadEntity> query, string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => query.OrderByDescending(h => h.Codigo),
            "codigo_asc" => query.OrderBy(h => h.Codigo),
            "nombre_desc" => query.OrderByDescending(h => h.Nombre),
            "nombre_asc" => query.OrderBy(h => h.Nombre),
            "categoria_desc" => query.OrderByDescending(h => h.Categoria != null ? h.Categoria.Nombre : string.Empty),
            "categoria_asc" => query.OrderBy(h => h.Categoria != null ? h.Categoria.Nombre : string.Empty),
            _ => query.OrderBy(h => h.Codigo)
        };
    }
}