using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Contracts.Vacantes.Enums;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Typed HTTP client for the Vacantes web module.
/// </summary>
public interface IVacanteApiClient
{
    /// <summary>Lists vacantes using the backend segment and server-side filters.</summary>
    Task<PagedResult<VacanteDto>> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a vacante detail, or <see langword="null"/> when it is unavailable.</summary>
    Task<VacanteDetailDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the read-only EstadoVacante catalog.</summary>
    Task<IReadOnlyList<EstadoVacanteDto>> ListarEstadosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active puestos available to populate the Create dropdown.
    /// Backed by <c>GET /api/v1/puestos</c>; declared here so the Vacante
    /// page does not depend on <see cref="SGV.Web.Integration.Organizacion.IPuestosApiClient"/>
    /// cross-module (issue #235).
    /// </summary>
    Task<IReadOnlyList<PuestoDto>> ListarPuestosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Creates a vacante and returns its persisted detail.</summary>
    Task<VacanteCommandResult> CrearAsync(
        CrearVacanteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Changes a vacante state and optionally updates observations.</summary>
    Task<VacanteCommandResult> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoVacanteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// T-7.1 / T-7.2 (change <c>vacante-ocupacion-flow-alignment</c>):
    /// devuelve <see langword="true"/> si el backend reporta al menos
    /// una Vacante ABIERTA (no terminal) para <paramref name="puestoId"/>.
    /// Consumido por <c>Ocupaciones/Create</c> (hint FORM-009) y por
    /// <c>PuestoOcupaciones</c> (botón NAV-007 "Abrir Vacante").
    /// </summary>
    Task<bool> ExisteVacanteAbiertaParaPuestoAsync(
        Guid puestoId,
        CancellationToken cancellationToken = default);
}
