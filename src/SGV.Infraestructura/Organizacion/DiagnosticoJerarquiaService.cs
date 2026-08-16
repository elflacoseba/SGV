using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;

namespace SGV.Infraestructura.Organizacion;

/// <summary>
/// MySQL/EFCore implementation of <see cref="IDiagnosticoJerarquiaService"/>.
/// Reports every back-edge observed in the padre-edge directed graph
/// formed by <see cref="UnidadOrganizativaEntity.UnidadPadreId"/>. The
/// method is read-only: no rows are mutated.
/// </summary>
/// <remarks>
/// Algorithm: walk up the padre chain from each unit that is not yet
/// globally marked as safe. If the walk revisits a node already on the
/// path, the segment from the first occurrence onward is a cycle. The
/// reported <see cref="CicloDetectado"/> path repeats the entry node at
/// the end so consumers can render <c>A → B → A</c>. Complexity is
/// O(N²) worst case but bounded by <see cref="ITestableHierarchies"/>
/// sizes in SGV (≤10³ filas activas).
/// </remarks>
public sealed class DiagnosticoJerarquiaService(SgvDbContext context) : IDiagnosticoJerarquiaService
{
    public async Task<IReadOnlyList<CicloDetectado>> DiagnosticarAsync(CancellationToken cancellationToken = default)
    {
        // Pull every active, non-deleted unit's id + padre id once. We keep
        // the projection small (two columns) to avoid materializing full
        // nav properties that the diagnostic does not need.
        var activeUnits = await context
            .Set<UnidadOrganizativaEntity>()
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted)
            .Select(u => new { u.Id, u.UnidadPadreId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = activeUnits
            .Where(u => u.Id != Guid.Empty)
            .GroupBy(u => u.Id)
            .ToDictionary(g => g.Key, g => g.First().UnidadPadreId);

        var detectedCycles = new List<CicloDetectado>();
        var alreadyGloballyChecked = new HashSet<Guid>();

        foreach (var u in activeUnits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (alreadyGloballyChecked.Contains(u.Id))
            {
                continue;
            }

            var path = new List<Guid>();
            var pathSet = new HashSet<Guid>();
            Guid? currentPadreId = u.UnidadPadreId;
            Guid currentId = u.Id;

            while (currentPadreId.HasValue && byId.TryGetValue(currentPadreId.Value, out _))
            {
                var padreId = currentPadreId.Value;

                if (pathSet.Contains(padreId))
                {
                    var cycleStartIdx = path.IndexOf(padreId);
                    var cyclePath = path.Skip(cycleStartIdx).Append(padreId).ToList();
                    detectedCycles.Add(new CicloDetectado(cyclePath));
                    break;
                }

                path.Add(padreId);
                pathSet.Add(padreId);
                alreadyGloballyChecked.Add(padreId);

                currentId = padreId;
                currentPadreId = byId[currentId];
            }
        }

        return detectedCycles;
    }
}
