using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Consultas;

public sealed class UnidadOrganizativaServicioConsulta(IUnidadOrganizativaRepository repository)
    : IUnidadOrganizativaServicioConsulta
{
    public async Task<IReadOnlyList<UnidadOrganizativaDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAllAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is not null ? MapToDto(entity) : null;
    }

    public async Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(
        UnidadOrganizativaQuery query,
        CancellationToken cancellationToken = default)
    {
        // Issue #278: clamp page/pageSize to the contract's documented range
        // before invoking the repository, so `Skip((page - 1) * pageSize)`
        // cannot receive a negative count and `Take(pageSize)` cannot exceed
        // `UnidadOrganizativaQuery.MaxPageSize`. Without this guard, `page=0`
        // (or any negative page) and a huge `pageSize` (DoS-by-amplification)
        // reach the persistence layer and trigger runtime failures.
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < UnidadOrganizativaQuery.MinPageSize
            ? UnidadOrganizativaQuery.MinPageSize
            : (query.PageSize > UnidadOrganizativaQuery.MaxPageSize
                ? UnidadOrganizativaQuery.MaxPageSize
                : query.PageSize);

        var (items, totalCount) = await repository.QueryAsync(
            query.Search,
            query.TipoUnidadOrganizativaId,
            query.UnidadPadreId,
            query.VigenteEn,
            page,
            pageSize,
            query.Segmento,
            cancellationToken);

        return new PagedResult<UnidadOrganizativaDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize);
    }

    public async Task<UnidadOrganizativaArbolResponse> GetTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await repository.ListTreeAsync(cancellationToken);
        var arbolexclusion = DetectCiclosJerarquicos(all);
        var cyclicNodes = new HashSet<Guid>(arbolexclusion);
        var tree = BuildTree(all, null, ancestors: null, cyclicNodes);
        return new UnidadOrganizativaArbolResponse(tree, arbolexclusion);
    }

    /// <summary>
    /// Walks the padre chain from every active node and reports the set of
    /// ids that participate in at least one cycle. O(N²) worst-case on a
    /// graph with a single FK per row, acceptable for SGV tree sizes
    /// (≤10³ rows). Returns ids in stable order (sorted ascending) so the
    /// response is repeatable across runs.
    /// </summary>
    private static IReadOnlyList<Guid> DetectCiclosJerarquicos(
        IReadOnlyList<UnidadOrganizativa> all)
    {
        var byId = all.ToDictionary(u => u.Id);
        var cyclicNodes = new HashSet<Guid>();

        foreach (var node in all)
        {
            var path = new List<Guid> { node.Id };
            var current = node;
            while (current.UnidadPadreId.HasValue
                   && byId.TryGetValue(current.UnidadPadreId.Value, out var parent))
            {
                if (path.Contains(parent.Id))
                {
                    // Cycle: mark every node from the entry point onward.
                    var entryIdx = path.IndexOf(parent.Id);
                    for (var i = entryIdx; i < path.Count; i++)
                    {
                        cyclicNodes.Add(path[i]);
                    }

                    break;
                }

                path.Add(parent.Id);
                current = parent;
            }
        }

        return cyclicNodes.OrderBy(id => id).ToList();
    }

    /// <summary>
    /// Recursive tree builder that tracks the path from the current root to
    /// the active node via <paramref name="ancestors"/>. When the same id is
    /// about to be visited twice (i.e. a cycle exists in the data), the
    /// sub-tree is skipped instead of recursing forever.
    /// </summary>
    /// <remarks>
    /// Issue #277: a defensive <c>visited-set</c> over the current path
    /// bounds recursion depth regardless of cycles the BD might carry. The
    /// path is propagated by copy at every step so siblings do not falsely
    /// detect cycles between each other — a unit can appear multiple times
    /// in the dataset as the child of multiple parents without being a
    /// cycle, only a repeat on the current path counts. The optional
    /// <paramref name="cyclicNodes"/> filter further excludes nodes that
    /// <see cref="DetectCiclosJerarquicos"/> flagged so the API can present
    /// just the acyclic portion of the tree.
    /// </remarks>
    private static List<UnidadOrganizativaTreeNodeDto> BuildTree(
        IReadOnlyList<UnidadOrganizativa> all,
        Guid? parentId,
        HashSet<Guid>? ancestors,
        HashSet<Guid>? cyclicNodes = null)
    {
        var currentPath = ancestors ?? new HashSet<Guid>();
        var result = new List<UnidadOrganizativaTreeNodeDto>();
        foreach (var u in all.Where(x => x.UnidadPadreId == parentId))
        {
            if (cyclicNodes is not null && cyclicNodes.Contains(u.Id))
            {
                // Cycle-pre-detected: omit from the canonical tree so
                // the consumer can render the partial hierarchy without
                // emitting duplicate nodes from a closed loop.
                continue;
            }

            if (!currentPath.Add(u.Id))
            {
                // Cycle: id already present on the current path from root
                // to here. Skip the sub-tree rather than recurse.
                continue;
            }

            var childPath = new HashSet<Guid>(currentPath);
            result.Add(new UnidadOrganizativaTreeNodeDto(
                u.Id,
                u.Codigo,
                u.Nombre,
                u.TipoUnidadOrganizativaId,
                u.TipoUnidadOrganizativa?.Nombre ?? string.Empty,
                BuildTree(all, u.Id, childPath, cyclicNodes)));
        }

        return result;
    }

    private static UnidadOrganizativaDto MapToDto(UnidadOrganizativa entity)
    {
        return new UnidadOrganizativaDto(
            entity.Id,
            entity.Codigo,
            entity.Nombre,
            entity.TipoUnidadOrganizativaId,
            entity.TipoUnidadOrganizativa?.Nombre ?? string.Empty,
            entity.Descripcion,
            entity.VigenteDesde,
            entity.VigenteHasta,
            entity.UnidadPadreId,
            entity.UnidadPadre?.Codigo,
            entity.UnidadPadre?.Nombre
        );
    }
}
