using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;

namespace SGV.Web.Integration.Ocupaciones;

/// <summary>
/// Cliente HTTP tipado del módulo web de Ocupaciones. Slice 3a del change
/// <c>2026-07-28-web-ocupaciones-issue-208</c>: agrega la superficie de
/// mutaciones (Crear/Actualizar/Finalizar/Eliminar/Reactivar) a los métodos
/// de consulta existentes (<see cref="ListarAsync"/>,
/// <see cref="ObtenerPorIdAsync"/>) introducidos en Slice 2.
/// </summary>
/// <remarks>
/// <para>
/// Espejo de <c>PuestosApiClient</c>: el cliente delega la composición de la
/// query URI en <see cref="OcupacionApiClient.BuildQueryUri"/> (mirror exacto
/// del patrón <c>StringBuilder + Uri.EscapeDataString</c>) y propaga
/// <see cref="HttpRequestException"/> y <see cref="TaskCanceledException"/>
/// nativas, sin traducirlas a <c>OcupacionCommandResult</c>, alineado con el
/// spec <c>web-apiclient-transport-contract</c>.
/// </para>
/// <para>
/// La rama no exitosa de los métodos de mutación delega en
/// <see cref="Common.CommandResultMapper"/> para preservar la taxonomía
/// <see cref="Common.ErrorCategoria"/> que consumen los PageModels
/// (<c>OcupacionError.Categoria</c> viene poblado por el mapper; los
/// PageModels ramifican por categoría, no por código HTTP).
/// </para>
/// </remarks>
public interface IOcupacionApiClient
{
    /// <summary>
    /// Lista ocupaciones paginadas server-side con segmento, búsqueda, orden
    /// y filtros contextuales (<see cref="OcupacionListQuery.PersonaId"/>,
    /// <see cref="OcupacionListQuery.PuestoId"/>) opcionales.
    /// </summary>
    Task<PagedResult<OcupacionDto>> ListarAsync(
        OcupacionListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una ocupación por id. Devuelve <see langword="null"/> si el
    /// backend responde <c>404 Not Found</c>; propaga excepciones nativas
    /// para fallos de transporte y timeout.
    /// </summary>
    Task<OcupacionDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una nueva ocupación vía <c>POST /api/v1/ocupaciones</c>. Devuelve
    /// éxito con el DTO persistido o un fallo tipado con
    /// <see cref="OcupacionError.Categoria"/> poblado por
    /// <c>CommandResultMapper</c>. Mapea <c>400</c> con
    /// <c>ValidationProblemDetails</c> a <c>FieldErrors</c> y <c>409</c> por
    /// colisión de unicidad (<c>PersonaYPuestoOcupados</c> /
    /// <c>PuestoOcupado</c>) preservando el código funcional en
    /// <see cref="OcupacionError.Code"/>.
    /// </summary>
    Task<OcupacionCommandResult> CrearAsync(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza una ocupación existente vía <c>PUT /api/v1/ocupaciones/{id}</c>.
    /// Misma matriz de errores que <see cref="CrearAsync"/>.
    /// </summary>
    Task<OcupacionCommandResult> ActualizarAsync(
        Guid id,
        ActualizarOcupacionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finaliza una ocupación vigente vía
    /// <c>PATCH /api/v1/ocupaciones/{id}/finalizar</c> con
    /// <paramref name="request"/> (FechaFin + Observaciones?). El backend
    /// valida que <c>FechaFin &gt;= FechaInicio</c> y responde 409 cuando la
    /// ocupación ya no es vigente.
    /// </summary>
    Task<OcupacionCommandResult> FinalizarAsync(
        Guid id,
        FinalizarOcupacionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta baja lógica vía <c>DELETE /api/v1/ocupaciones/{id}</c>.
    /// <c>204 No Content</c> se traduce a éxito con
    /// <see cref="OcupacionCommandResult.IsSuccess"/> en <c>true</c> y
    /// <see cref="OcupacionCommandResult.Value"/> en <c>null</c>;
    /// <c>404</c>/<c>409</c> se traducen a <c>Failure</c>.
    /// </summary>
    Task<OcupacionCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactiva una ocupación finalizada o eliminada vía
    /// <c>PATCH /api/v1/ocupaciones/{id}/reactivar</c>. Mapea
    /// <c>409</c> por colisión (<c>PersonaYPuestoOcupados</c> /
    /// <c>PuestoOcupado</c> / <c>OcupacionYaActiva</c>) preservando el
    /// código funcional en <see cref="OcupacionError.Code"/>.
    /// </summary>
    Task<OcupacionCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}