using SGV.Aplicacion.Comun.Persistencia;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Dominio.Vacantes;

namespace SGV.Aplicacion.Vacantes.Consultas;

/// <summary>
/// Repository contract for Vacante read and write operations.
/// El segmento del listado se evalúa contra <c>EstadoVacante.EsTerminal</c>
/// (no contra <c>FechaCierre</c>) para mantener fidelidad con la spec
/// (<c>design.md</c> §D-2). El método <c>GetByIdForUpdateAsync</c>
/// devuelve la entidad rastreada por EF Core para que el cambio de estado
/// (vacante + historial) se persista en una única transacción.
/// </summary>
public interface IVacanteRepository : IReadOnlyRepository<Vacante>
{
    /// <summary>
    /// Adds a new vacante to the persistence context. The caller is
    /// responsible for invoking <see cref="IUnitOfWork.SaveChangesAsync"/>
    /// to commit the change.
    /// </summary>
    Task AddAsync(Vacante vacante, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a vacante by id, tracked by EF Core, with eager loading of
    /// <c>Puesto</c> + <c>EstadoVacante</c> + <c>HistorialEstados</c>
    /// (and their inner navigation properties) so the caller can mutate the
    /// aggregate and persist vacante + historial atomically.
    /// </summary>
    Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists vacantes filtered by <see cref="VacanteListQuery.Segmento"/>
    /// (and optional filters). Returns a tuple <c>(Items, TotalCount)</c>
    /// for server-side pagination. The segmento filter is applied as a
    /// join against <c>EstadoVacante.EsTerminal</c>; never mixed.
    /// </summary>
    Task<(IReadOnlyList<Vacante> Items, int TotalCount)> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> when at least one non-terminal
    /// (no cerrada) vacante already exists for the given <paramref name="puestoId"/>.
    /// Used by the service layer to enforce the "one open vacante per puesto"
    /// rule before creating a new one.
    /// </summary>
    Task<bool> ExistsAbiertaByPuestoAsync(Guid puestoId, CancellationToken cancellationToken = default);
}