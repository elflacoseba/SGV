using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Ocupaciones;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Fake en memoria de <see cref="IOcupacionApiClient"/> compartido por las
/// pruebas web de Ocupaciones. Slice 3a del change #208: agrega los stubs de
/// las mutaciones (<see cref="CrearAsync"/>, <see cref="ActualizarAsync"/>,
/// <see cref="FinalizarAsync"/>, <see cref="EliminarAsync"/>,
/// <see cref="ReactivarAsync"/>) con respuestas programadas, captura de
/// invocaciones y excepciones inyectables por método. Los métodos de lectura
/// (Listar/ObtenerPorId) introducidos en Slice 2 se preservan sin cambios.
/// </summary>
public sealed class FakeOcupacionApiClient : IOcupacionApiClient
{
    // ── Respuestas programadas (lectura — Slice 2) ──────────────────

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

    // ── Respuestas programadas (mutaciones — Slice 3a) ──────────────

    /// <summary>Resultado de <see cref="CrearAsync"/>. Default: éxito que refleja el request.</summary>
    public OcupacionCommandResult CrearResult { get; set; } = default!;

    /// <summary>Resultado de <see cref="ActualizarAsync"/>. Default: éxito que refleja el request.</summary>
    public OcupacionCommandResult ActualizarResult { get; set; } = default!;

    /// <summary>Resultado de <see cref="FinalizarAsync"/>. Default: éxito que refleja el request.</summary>
    public OcupacionCommandResult FinalizarResult { get; set; } = default!;

    /// <summary>Resultado de <see cref="EliminarAsync"/>. Default: éxito (204).</summary>
    public OcupacionCommandResult EliminarResult { get; set; } =
        new(true, Value: null, Error: null);

    /// <summary>Resultado de <see cref="ReactivarAsync"/>. Default: éxito que refleja el id.</summary>
    public OcupacionCommandResult ReactivarResult { get; set; } = default!;

    // ── Excepciones inyectables ─────────────────────────────────────

    public Exception? ListarException { get; set; }
    public Exception? ObtenerPorIdException { get; set; }
    public Exception? CrearException { get; set; }
    public Exception? ActualizarException { get; set; }
    public Exception? FinalizarException { get; set; }
    public Exception? EliminarException { get; set; }
    public Exception? ReactivarException { get; set; }

    // ── Captura de invocaciones (lectura — Slice 2) ─────────────────

    public List<OcupacionListQuery> ListarCalls { get; } = [];
    public List<Guid> ObtenerPorIdCalls { get; } = [];

    // ── Captura de invocaciones (mutaciones — Slice 3a) ──────────────

    public List<CrearOcupacionRequest> CrearCalls { get; } = [];
    public List<(Guid Id, ActualizarOcupacionRequest Request)> ActualizarCalls { get; } = [];
    public List<(Guid Id, FinalizarOcupacionRequest Request)> FinalizarCalls { get; } = [];
    public List<Guid> EliminarCalls { get; } = [];
    public List<Guid> ReactivarCalls { get; } = [];

    // ── Métodos de lectura (Slice 2) ─────────────────────────────────

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

    // ── Métodos de mutación (Slice 3a) ───────────────────────────────

    public Task<OcupacionCommandResult> CrearAsync(
        CrearOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        CrearCalls.Add(request);

        if (CrearException is not null)
        {
            throw CrearException;
        }

        return Task.FromResult(CrearResult);
    }

    public Task<OcupacionCommandResult> ActualizarAsync(
        Guid id,
        ActualizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        ActualizarCalls.Add((id, request));

        if (ActualizarException is not null)
        {
            throw ActualizarException;
        }

        return Task.FromResult(ActualizarResult);
    }

    public Task<OcupacionCommandResult> FinalizarAsync(
        Guid id,
        FinalizarOcupacionRequest request,
        CancellationToken cancellationToken = default)
    {
        FinalizarCalls.Add((id, request));

        if (FinalizarException is not null)
        {
            throw FinalizarException;
        }

        return Task.FromResult(FinalizarResult);
    }

    public Task<OcupacionCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EliminarCalls.Add(id);

        if (EliminarException is not null)
        {
            throw EliminarException;
        }

        return Task.FromResult(EliminarResult);
    }

    public Task<OcupacionCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ReactivarCalls.Add(id);

        if (ReactivarException is not null)
        {
            throw ReactivarException;
        }

        return Task.FromResult(ReactivarResult);
    }

    /// <summary>Helper para construir un DTO de ocupación en los tests.</summary>
    public static OcupacionDto BuildDto(
        Guid? id = null,
        Guid? personaId = null,
        string personaNombre = "Juan Perez",
        Guid? puestoId = null,
        string puestoNombre = "Analista",
        DateOnly? fechaInicio = null,
        DateOnly? fechaFin = null,
        OcupacionTipoAsignacion tipo = OcupacionTipoAsignacion.Permanente,
        string? observaciones = null,
        OcupacionEstado estado = OcupacionEstado.Vigente)
        => new(
            id ?? Guid.NewGuid(),
            PersonaId: personaId ?? Guid.NewGuid(),
            PersonaNombre: personaNombre,
            PuestoId: puestoId ?? Guid.NewGuid(),
            PuestoNombre: puestoNombre,
            FechaInicio: fechaInicio ?? new DateOnly(2026, 1, 1),
            FechaFin: fechaFin,
            TipoAsignacion: tipo,
            Observaciones: observaciones,
            Estado: estado);
}