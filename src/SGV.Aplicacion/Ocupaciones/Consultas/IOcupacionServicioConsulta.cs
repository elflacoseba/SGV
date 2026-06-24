using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;

namespace SGV.Aplicacion.Ocupaciones.Consultas;

/// <summary>
/// Read-only query service for Ocupaciones.
/// </summary>
public interface IOcupacionServicioConsulta
{
    /// <summary>
    /// Returns active occupations by default, or all non-physically-deleted
    /// occupations when <paramref name="includeHistory"/> is <see langword="true"/>.
    /// </summary>
    Task<IReadOnlyList<OcupacionDto>> ListAsync(
        bool includeHistory = false,
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
