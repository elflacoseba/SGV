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

    /// <summary>
    /// Devuelve los Puestos efectivamente disponibles para poblar el
    /// dropdown de <c>Vacantes/Create</c> — aquellos sin Ocupación vigente
    /// ni Vacante abierta (REQ-PTO-DISP-001, defense-in-depth UX). La
    /// validación N1 y el constraint <c>ActivePuestoIdUnique</c> siguen
    /// siendo la fuente de verdad en el backend.
    /// Backed by <c>GET /api/v1/puestos/disponibles</c>.
    /// <see cref="ListarPuestosAsync"/> permanece intacto para preservar el
    /// contrato existente y otros consumidores potenciales.
    /// </summary>
    Task<IReadOnlyList<PuestoDto>> ListarPuestosDisponiblesAsync(
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

    /// <summary>
    /// T2.13 (change <c>invertir-flujo-cubrir</c> / S2): obtiene la Vacante
    /// abierta para <paramref name="puestoId"/>, si existe. Consumido por
    /// <c>PuestoOcupaciones</c> para alimentar <c>?vacanteId=</c> en el botón
    /// "Cubrir Vacante" (REQ-OCC-NAV-006 invertido): el alta contextual va
    /// al Create de Ocupación con el id de la Vacante abierta (no con el
    /// <c>PuestoId</c>), de modo que el POST sea transaccional con la
    /// transición a Cubierta. <see langword="null"/> cuando el Puesto no
    /// tiene Vacante abierta (incluye 404 defensivo).
    /// </summary>
    Task<VacanteDto?> ObtenerAbiertaPorPuestoAsync(
        Guid puestoId,
        CancellationToken cancellationToken = default);
}
