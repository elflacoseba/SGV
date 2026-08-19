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

        // Issue #282: normalizar `Search` antes de invocar al repo es más
        // robusto que confiar en que cada caller lo haya trimeado/clampado
        // (la web shell lo hace vía `Normalize`, pero un cliente API directo
        // podría saltarse esa guardia y enviar whitespace o 10kb de texto).
        // El repo además vuelve a trimear como defensa en profundidad.
        var search = NormalizeSearch(query.Search);

        var (items, totalCount) = await repository.QueryAsync(
            search,
            query.TipoUnidadOrganizativaId,
            query.UnidadPadreId,
            query.VigenteEn,
            page,
            pageSize,
            query.Sort,
            query.Segmento,
            cancellationToken);

        return new PagedResult<UnidadOrganizativaDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize);
    }

    /// <summary>
    /// Issue #282: trim + clamp del término de búsqueda. Null/whitespace
    /// colapsan a <c>null</c> para que el repo no genere un <c>LIKE '%%'</c>
    /// inútil. Valores más largos que <see cref="UnidadOrganizativaQuery.MaxSearchLength"/>
    /// se truncan para acotar el coste del <c>LIKE '%texto%'</c> en MySQL.
    /// </summary>
    private static string? NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > UnidadOrganizativaQuery.MaxSearchLength
            ? trimmed[..UnidadOrganizativaQuery.MaxSearchLength]
            : trimmed;
    }

    public async Task<UnidadOrganizativaArbolResponse> GetTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await repository.ListTreeAsync(cancellationToken);
        var arbolexclusion = DetectCiclosJerarquicos(all);
        var cyclicNodes = new HashSet<Guid>(arbolexclusion);

        // H-A4 (housekeeping release-readiness UO+Organigrama): agrupar
        // por padre UNA vez antes de la recursion. Antes, BuildTree hacía
        // `all.Where(x => x.UnidadPadreId == parentId)` por cada nodo, lo
        // que daba O(N²) para construir el árbol completo. Con el lookup
        // pasamos a O(N) en la fase de agrupamiento + O(N) en la recursion.
        var porPadre = all.ToLookup(u => u.UnidadPadreId);

        var tree = BuildTree(porPadre, null, ancestors: null, cyclicNodes);
        return new UnidadOrganizativaArbolResponse(tree, arbolexclusion);
    }

    /// <summary>
    /// Walks the padre chain from every active node and reports the set of
    /// ids that participate in at least one cycle. H-A4: usa <c>HashSet</c>
    /// para el path (chequeo O(1)) en lugar de <c>List.Contains</c> (O(d))
    /// — antes era O(N·depth), ahora O(N) en la práctica.
    /// </summary>
    private static IReadOnlyList<Guid> DetectCiclosJerarquicos(
        IReadOnlyList<UnidadOrganizativa> all)
    {
        var byId = all.ToDictionary(u => u.Id);
        var cyclicNodes = new HashSet<Guid>();

        foreach (var node in all)
        {
            var path = new HashSet<Guid> { node.Id };
            var current = node;
            while (current.UnidadPadreId.HasValue
                   && byId.TryGetValue(current.UnidadPadreId.Value, out var parent))
            {
                if (!path.Add(parent.Id))
                {
                    // Cycle: el padre ya esta en el path. Marcamos el
                    // padre y todos los descendientes hasta la revisit
                    // como parte del ciclo.
                    cyclicNodes.Add(parent.Id);
                    // Reconstruimos la cadena para marcar el sub-path.
                    // (El padre ya esta en path; lo agregamos arriba.)
                    var walker = node;
                    while (walker.UnidadPadreId.HasValue
                           && byId.TryGetValue(walker.UnidadPadreId.Value, out var p)
                           && p.Id != parent.Id)
                    {
                        cyclicNodes.Add(walker.Id);
                        walker = p;
                    }
                    cyclicNodes.Add(walker.Id);

                    break;
                }

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
    /// H-A4 (housekeeping release-readiness UO+Organigrama): antes
    /// recibía <c>IReadOnlyList&lt;UnidadOrganizativa&gt;</c> y hacía
    /// <c>all.Where(x => x.UnidadPadreId == parentId)</c> en cada nivel
    /// de recursion, dando O(N²) para el peor caso. Ahora recibe un
    /// <see cref="ILookup{TKey,TElement}"/> precomputado en
    /// <see cref="GetTreeAsync"/> y consume <c>porPadre[parentId]</c>
    /// en O(1) por nivel, totalizando O(N).
    /// </remarks>
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
        ILookup<Guid?, UnidadOrganizativa> porPadre,
        Guid? parentId,
        HashSet<Guid>? ancestors,
        HashSet<Guid>? cyclicNodes = null)
    {
        var currentPath = ancestors ?? new HashSet<Guid>();
        var result = new List<UnidadOrganizativaTreeNodeDto>();
        foreach (var u in porPadre[parentId])
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
            // Issue #286: propagamos VigenteDesde/VigenteHasta al wire para que
            // el shell web pueda filtrar visualmente las unidades cuya ventana
            // de vigencia ya cerró. La semántica de vigencia sigue viviendo en
            // el dominio (`UnidadOrganizativa.EsVigente`); acá solo exponemos
            // los datos persistidos.
            result.Add(new UnidadOrganizativaTreeNodeDto(
                u.Id,
                u.Codigo,
                u.Nombre,
                u.TipoUnidadOrganizativaId,
                u.TipoUnidadOrganizativa?.Nombre ?? string.Empty,
                BuildTree(porPadre, u.Id, childPath, cyclicNodes),
                u.VigenteDesde,
                u.VigenteHasta));
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
