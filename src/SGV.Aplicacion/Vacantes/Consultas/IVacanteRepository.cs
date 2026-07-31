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
/// (vacante + historial) se persista en una única transacción; el bridge
/// explícito entre la colección de dominio y la colección EF se hace vía
/// <see cref="RegistrarCambioEstadoAsync"/> para preservar la atomicidad
/// sin filtrar tipos de infraestructura al application layer.
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
    /// aggregate via the domain methods. The persisted entity remains
    /// tracked; the caller MUST follow with
    /// <see cref="RegistrarCambioEstadoAsync"/> (for state transitions) or
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> (for plain field updates)
    /// to commit changes inside a single EF transaction.
    /// </summary>
    Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a state transition in a single EF transaction. The caller
    /// invokes <see cref="GetByIdForUpdateAsync"/> to load the tracked
    /// domain aggregate, calls <c>vacante.CambiarEstado(...)</c> to mutate
    /// it, and then forwards the returned <see cref="HistorialEstadoVacante"/>
    /// through this method so the infrastructure layer can:
    /// (a) re-fetch the tracked <c>VacanteEntity</c> and apply
    /// <c>UpdateEntity</c> with the domain state; and
    /// (b) map the new history entry to <c>HistorialEstadoVacanteEntity</c>
    /// and add it to <c>entity.HistorialEstados</c>.
    /// EF wraps both writes in one transaction at
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> time — see
    /// <c>design.md</c> §D-5 (atomicidad).
    /// </summary>
    Task RegistrarCambioEstadoAsync(
        Vacante vacante,
        HistorialEstadoVacante historial,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a plain field update (e.g. <c>ActualizarObservaciones</c>)
    /// by re-fetching the tracked entity and applying
    /// <c>UpdateEntity</c> with the mutated domain. No history row is
    /// added; EF wraps the single UPDATE in its own transaction.
    /// </summary>
    Task UpdateAsync(Vacante vacante, CancellationToken cancellationToken = default);

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