using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Ocupaciones.Consultas;

/// <summary>
/// Read-only query service for Ocupaciones.
/// </summary>
public interface IOcupacionServicioConsulta
{
    /// <summary>
    /// Returns a filtered, segmented page of occupations.
    /// </summary>
    Task<PagedResult<OcupacionDto>> QueryAsync(
        OcupacionListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single occupation by its identifier, or null if not found.
    /// Detail reads MUST bypass soft-delete filters — the underlying
    /// repository call uses a dedicated method that ignores <see cref="IsDeleted"/>
    /// to include historical (finalized/deleted) rows.
    /// </summary>
    Task<OcupacionDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
