using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Aplicacion.Auditoria;

/// <summary>
/// Puerto de lectura para el módulo transversal de auditoría.
/// Permite consultar el listado paginado y el detalle de un
/// registro sin necesidad de tocar la capa de persistencia.
/// </summary>
public interface IAuditoriaServicioConsulta
{
    /// <summary>
    /// Devuelve un <see cref="PagedResult{T}"/> de
    /// <see cref="AuditoriaDto"/> aplicando los filtros y el orden
    /// del query. El orden es siempre server-side (controlado por
    /// <see cref="AuditoriaListQuery.Sort"/>) y <c>ThenByDescending(Id)</c>
    /// como tiebreak determinista.
    /// </summary>
    /// <param name="query">Filtros + paginación + orden.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <exception cref="ArgumentException">
    /// Si <c>query.DateFrom &gt; query.DateTo</c>.
    /// </exception>
    Task<PagedResult<AuditoriaDto>> QueryAsync(
        AuditoriaListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el <see cref="AuditoriaDetalleDto"/> enriquecido por
    /// identificador (con <c>EntityId</c>, <c>OldValuesJson</c>,
    /// <c>NewValuesJson</c> y <c>UserName</c> vía LEFT JOIN con
    /// <c>AspNetUsers</c>), o <c>null</c> si no existe.
    /// </summary>
    /// <remarks>
    /// Único punto del sistema que expone old/new values; el caller
    /// (controller admin-only) es responsable de la autorización.
    /// </remarks>
    Task<AuditoriaDetalleDto?> GetDetalleDtoAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
