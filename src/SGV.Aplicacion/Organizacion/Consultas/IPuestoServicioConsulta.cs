using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Organizacion.Consultas;

/// <summary>
/// Read-only query service for Puestos.
/// </summary>
public interface IPuestoServicioConsulta
{
    /// <summary>
    /// Returns all active positions as DTOs with related entity summaries.
    /// </summary>
    Task<IReadOnlyList<PuestoDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single position by its identifier with related entity summaries, or null if not found.
    /// </summary>
    Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side paginated, filtered, sorted query for puestos.
    /// Returns a <see cref="PagedResult{T}"/> with the items for the
    /// requested page and the total count matching the filters.
    /// </summary>
    Task<PagedResult<PuestoDto>> QueryAsync(PuestoListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active puestos without active Ocupacion nor open Vacante.
    /// Defense-in-depth query used by <c>GET /api/v1/puestos/disponibles</c>.
    /// </summary>
    Task<IReadOnlyList<PuestoDto>> ListarDisponiblesAsync(CancellationToken cancellationToken = default);
}
