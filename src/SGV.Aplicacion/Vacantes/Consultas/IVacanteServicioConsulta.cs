using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;

namespace SGV.Aplicacion.Vacantes.Consultas;

/// <summary>
/// Read-only query service for Vacantes. Bridges the persistence-layer
/// aggregate (<see cref="SGV.Dominio.Vacantes.Vacante"/>) to the
/// consumer-safe wire-type (<see cref="VacanteDto"/> /
/// <see cref="VacanteDetailDto"/>) and centralises denormalisation of
/// <c>Puesto.Nombre</c> and <c>EstadoVacante.Nombre</c>.
/// </summary>
public interface IVacanteServicioConsulta
{
    /// <summary>
    /// Returns a filtered, paginated set of vacantes for the requested
    /// segmento (<c>abiertas | cerradas | todas</c>). The segmento
    /// filter is applied server-side by the repository as a join against
    /// <c>EstadoVacante.EsTerminal</c> — never mixed.
    /// </summary>
    Task<PagedResult<VacanteDto>> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the detail view of a single vacante including its
    /// <c>HistorialEstadoVacante</c> in chronological order, or
    /// <see langword="null"/> when not found.
    /// </summary>
    Task<VacanteDetailDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}