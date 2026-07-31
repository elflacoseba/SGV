using SGV.Contracts.Vacantes.Consultas.Dtos;

namespace SGV.Aplicacion.Vacantes.Consultas;

/// <summary>
/// Read-only query service for the <c>EstadoVacante</c> catalog.
/// The catalog is immutable (4 seed rows in the <c>20000000-…</c> GUID
/// block — see <c>EstadoVacanteConstantes</c>); no write methods are
/// exposed. The DTO <see cref="EstadoVacanteDto"/> includes
/// <c>EsTerminal</c> as a wire-level flag so consumers can decide how
/// to render terminal states without making a second round-trip to a
/// per-vacante endpoint.
/// </summary>
public interface IEstadoVacanteServicioConsulta
{
    /// <summary>
    /// Returns all catalog rows ordered by <c>Orden</c> ascending.
    /// </summary>
    Task<IReadOnlyList<EstadoVacanteDto>> ListarAsync(
        CancellationToken cancellationToken = default);
}