using SGV.Dominio.Vacantes;

namespace SGV.Aplicacion.Vacantes.Consultas;

/// <summary>
/// Read-only repository contract for the <c>EstadoVacante</c> catalog.
/// The catalog is immutable (4 seed rows in the <c>20000000-…</c> GUID
/// block — see <c>EstadoVacanteConstantes</c>); no write methods are
/// exposed. Used by <c>VacanteServicioComandos</c> to validate that the
/// target state of a <c>CambiarEstado</c> request exists and to read its
/// <c>EsTerminal</c> flag (drives the auto-set of <c>FechaCierre</c>).
/// </summary>
public interface IEstadoVacanteRepository
{
    /// <summary>
    /// Returns the <c>EstadoVacante</c> identified by <paramref name="id"/>,
    /// or <see langword="null"/> when no row matches.
    /// </summary>
    Task<EstadoVacante?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all catalog rows ordered by <c>Orden</c> ascending.
    /// </summary>
    Task<IReadOnlyList<EstadoVacante>> ListAllAsync(CancellationToken cancellationToken = default);
}