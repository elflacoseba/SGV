using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Enums;
using SGV.Dominio.Vacantes;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Mapeos;

namespace SGV.Infraestructura.Persistencia.Repositorios;

/// <summary>
/// Repository for Vacante read and write operations using EF Core.
/// Eager loads <c>Puesto</c> + <c>EstadoVacante</c> on read queries, and
/// also <c>HistorialEstados</c> (with its inner <c>EstadoAnterior</c> /
/// <c>EstadoNuevo</c>) on the tracked write path so the service layer can
/// mutate vacante + add a new history row and persist both in a single
/// transaction (see <c>design.md</c> §D-5 — atomicidad provista por EF
/// en una transacción).
/// </summary>
public sealed class VacanteRepository(SgvDbContext context)
    : ReadOnlyRepository<VacanteEntity, Vacante>(context), IVacanteRepository
{
    protected override IQueryable<VacanteEntity> Query => base
        .Query
        .Include(v => v.Puesto)
        .Include(v => v.EstadoVacante);

    protected override Vacante MapToDomain(VacanteEntity entity) => PersistenceToDomainMapper.ToDomain(entity);

    /// <summary>
    /// Adds a new vacante to the persistence context. The caller is
    /// responsible for invoking <c>SaveChangesAsync</c> to commit.
    /// </summary>
    public async Task AddAsync(Vacante vacante, CancellationToken cancellationToken = default)
    {
        var entity = DomainToPersistenceMapper.ToEntity(vacante);
        await Context.Set<VacanteEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tracks a vacante for update with full eager loading of its
    /// navegación <c>HistorialEstados</c> so the service layer can add a
    /// new history row alongside the vacante mutation; EF wraps both
    /// inserts in a single transaction at
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(CancellationToken)"/>
    /// time (atomicidad: si la FK del historial falla, el cambio de estado
    /// de la vacante también se revierte).
    /// </summary>
    public async Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await Context
            .Set<VacanteEntity>()
            .Include(v => v.Puesto)
            .Include(v => v.EstadoVacante)
            .Include(v => v.HistorialEstados)
                .ThenInclude(h => h.EstadoAnterior)
            .Include(v => v.HistorialEstados)
                .ThenInclude(h => h.EstadoNuevo)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    /// <summary>
    /// Lists vacantes filtered by <see cref="VacanteListQuery.Segmento"/>.
    /// <list type="bullet">
    /// <item><description><see cref="VacanteSegmentoListado.Abiertas"/> →
    ///   <c>EstadoVacante.EsTerminal == false</c> (excluye terminales;
    ///   spec mgmt "Segmento cerradas no mezcla abiertas").</description></item>
    /// <item><description><see cref="VacanteSegmentoListado.Cerradas"/> →
    ///   <c>EstadoVacante.EsTerminal == true</c> (excluye abiertas;
    ///   spec mgmt homólogo).</description></item>
    /// <item><description><see cref="VacanteSegmentoListado.Todas"/> → sin
    ///   filtro de segmento.</description></item>
    /// </list>
    /// El join contra <c>EstadoVacante</c> garantiza fidelidad con el
    /// dominio (un estado es terminal por catálogo, no por convención de
    /// <c>FechaCierre</c>); <c>design.md</c> §D-2.
    /// </summary>
    public async Task<(IReadOnlyList<Vacante> Items, int TotalCount)> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<VacanteEntity> baseQuery = Context
            .Set<VacanteEntity>()
            .AsNoTracking()
            .Include(v => v.Puesto)
            .Include(v => v.EstadoVacante);

        IQueryable<VacanteEntity> segmentada = query.Segmento switch
        {
            VacanteSegmentoListado.Abiertas => baseQuery.Where(v => !v.EstadoVacante.EsTerminal),
            VacanteSegmentoListado.Cerradas => baseQuery.Where(v => v.EstadoVacante.EsTerminal),
            VacanteSegmentoListado.Todas => baseQuery,
            _ => baseQuery.Where(v => !v.EstadoVacante.EsTerminal)
        };

        if (query.PuestoId is { } puestoId)
        {
            segmentada = segmentada.Where(v => v.PuestoId == puestoId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var likePattern = $"%{EscapeLikePattern(search)}%";
            segmentada = segmentada.Where(v =>
                EF.Functions.Like(v.Puesto.Nombre, likePattern, "\\") ||
                EF.Functions.Like(v.Motivo, likePattern, "\\") ||
                (v.Observaciones != null && EF.Functions.Like(v.Observaciones, likePattern, "\\")));
        }

        var totalCount = await segmentada.CountAsync(cancellationToken).ConfigureAwait(false);

        var ordered = ApplySort(segmentada, query.Sort);

        var entities = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToArray(), totalCount);
    }

    /// <summary>
    /// Server-side pagination + sort. The sort applies BEFORE Skip/Take
    /// so the visible order is preserved. Whitelisted values: fecha
    /// apertura asc/desc and puesto asc. Any other value falls back to
    /// FechaApertura desc (most recent first) for stable pagination.
    /// </summary>
    private static IOrderedQueryable<VacanteEntity> ApplySort(IQueryable<VacanteEntity> source, string? sort)
    {
        return sort?.ToLowerInvariant() switch
        {
            SGV.Contracts.Vacantes.VacanteApiRoutes.SortFechaAperturaAsc => source.OrderBy(v => v.FechaApertura),
            SGV.Contracts.Vacantes.VacanteApiRoutes.SortFechaAperturaDesc => source.OrderByDescending(v => v.FechaApertura),
            SGV.Contracts.Vacantes.VacanteApiRoutes.SortPuestoAsc => source.OrderBy(v => v.Puesto.Nombre),
            _ => source.OrderByDescending(v => v.FechaApertura)
        };
    }

    /// <summary>
    /// Returns <see langword="true"/> when at least one non-terminal
    /// (no cerrada) vacante exists for <paramref name="puestoId"/>.
    /// The query joins against <c>EstadoVacante</c> to honor
    /// <c>EsTerminal</c>; the filter is applied before any join to the
    /// catálogo so it scales with the <c>IX_Vacantes_PuestoId</c> index.
    /// </summary>
    public async Task<bool> ExistsAbiertaByPuestoAsync(Guid puestoId, CancellationToken cancellationToken = default)
    {
        return await Context
            .Set<VacanteEntity>()
            .Include(v => v.EstadoVacante)
            .AnyAsync(v =>
                v.PuestoId == puestoId &&
                !v.IsDeleted &&
                !v.EstadoVacante.EsTerminal,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Escapes LIKE wildcard characters so user-supplied '%' or '_' are
    /// matched literally. MySQL uses backslash as the escape character by
    /// default. Mirrors <see cref="OcupacionRepository.EscapeLikePattern"/>.
    /// </summary>
    private static string EscapeLikePattern(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}