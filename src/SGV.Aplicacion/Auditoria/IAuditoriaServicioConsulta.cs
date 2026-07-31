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
    /// <see cref="AuditoriaDto"/> aplicando los filtros del query.
    /// El orden es siempre <c>OccurredAt DESC, Id DESC</c>.
    /// </summary>
    /// <param name="query">Filtros + paginación.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <exception cref="ArgumentException">
    /// Si <c>query.DateFrom &gt; query.DateTo</c>.
    /// </exception>
    Task<PagedResult<AuditoriaDto>> QueryAsync(
        AuditoriaListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el <see cref="AuditoriaDto"/> por identificador, o
    /// <c>null</c> si no existe.
    /// </summary>
    Task<AuditoriaDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}