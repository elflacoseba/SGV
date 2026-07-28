using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Ocupaciones;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Fake en memoria de <see cref="IOcupacionApiClient"/> compartido por las
/// pruebas web de Ocupaciones. Slice 2 del change #208: implementa los 2
/// métodos de lectura (Listar/ObtenerPorId) con respuestas programadas
/// (<c>ListarResult</c>, <c>ObtenerPorIdResult</c>), captura de invocaciones
/// (<c>ListarCalls</c>, <c>ObtenerPorIdCalls</c>) y excepciones inyectables
/// (<c>ListarException</c>, <c>ObtenerPorIdException</c>). Las firmas de las
/// mutaciones (<c>Crear/Actualizar/Finalizar/Eliminar/Reactivar</c>) NO
/// existen todavía en <see cref="IOcupacionApiClient"/>; se agregan en Slice
/// 3a junto con la cobertura fina de errores 401/403/404/409/transport.
/// </summary>
public sealed class FakeOcupacionApiClient : IOcupacionApiClient
{
    // ── Respuestas programadas ──────────────────────────────────

    /// <summary>
    /// Resultado de <see cref="ListarAsync"/>. Cuando se setea, el fake ignora
    /// segmento/búsqueda/orden/paginación y devuelve este paged result tal
    /// cual. Útil para tests determinísticos que sólo verifican que el
    /// query se propaga al cliente.
    /// </summary>
    public PagedResult<OcupacionDto> ListarResult { get; set; } =
        new([], 0, 1, 20);

    /// <summary>
    /// Permite personalizar el resultado de cada consulta paginada en base al
    /// query recibido. Cuando no se configura, el fake devuelve
    /// <see cref="ListarResult"/>.
    /// </summary>
    public Func<OcupacionListQuery, PagedResult<OcupacionDto>>? ListarHandler { get; set; }

    /// <summary>Resultado de <see cref="ObtenerPorIdAsync"/> cuando no hay override por id.</summary>
    public OcupacionDto? ObtenerPorIdResult { get; set; }

    /// <summary>Permite personalizar el resultado por id concreto (sobrescribe <see cref="ObtenerPorIdResult"/>).</summary>
    public Func<Guid, OcupacionDto?>? ObtenerPorIdHandler { get; set; }

    // ── Excepciones inyectables ─────────────────────────────────

    public Exception? ListarException { get; set; }
    public Exception? ObtenerPorIdException { get; set; }

    // ── Captura de invocaciones ─────────────────────────────────

    public List<OcupacionListQuery> ListarCalls { get; } = [];
    public List<Guid> ObtenerPorIdCalls { get; } = [];

    // ── Métodos ─────────────────────────────────────────────────

    public Task<PagedResult<OcupacionDto>> ListarAsync(
        OcupacionListQuery query,
        CancellationToken cancellationToken = default)
    {
        ListarCalls.Add(query);

        if (ListarException is not null)
        {
            throw ListarException;
        }

        if (ListarHandler is not null)
        {
            return Task.FromResult(ListarHandler(query));
        }

        return Task.FromResult(ListarResult);
    }

    public Task<OcupacionDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ObtenerPorIdCalls.Add(id);

        if (ObtenerPorIdException is not null)
        {
            throw ObtenerPorIdException;
        }

        if (ObtenerPorIdHandler is not null)
        {
            return Task.FromResult(ObtenerPorIdHandler(id));
        }

        return Task.FromResult(ObtenerPorIdResult);
    }

    /// <summary>Helper para construir un DTO de ocupación en los tests.</summary>
    public static OcupacionDto BuildDto(
        Guid? id = null,
        string personaNombre = "Juan Perez",
        string puestoNombre = "Analista",
        DateOnly? fechaInicio = null,
        DateOnly? fechaFin = null,
        OcupacionTipoAsignacion tipo = OcupacionTipoAsignacion.Permanente,
        string? observaciones = null,
        OcupacionEstado estado = OcupacionEstado.Vigente)
        => new(
            id ?? Guid.NewGuid(),
            PersonaId: Guid.NewGuid(),
            PersonaNombre: personaNombre,
            PuestoId: Guid.NewGuid(),
            PuestoNombre: puestoNombre,
            FechaInicio: fechaInicio ?? new DateOnly(2026, 1, 1),
            FechaFin: fechaFin,
            TipoAsignacion: tipo,
            Observaciones: observaciones,
            Estado: estado);
}