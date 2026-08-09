using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Contracts.Vacantes.Enums;
using SGV.Web.Integration.Vacantes;

namespace SGV.Tests.Web.Vacantes;

internal sealed class FakeVacanteApiClient : IVacanteApiClient
{
    public PagedResult<VacanteDto> ListarResult { get; set; } = new([], 0, 1, 20);
    public Func<VacanteListQuery, PagedResult<VacanteDto>>? ListarHandler { get; set; }
    public VacanteDetailDto? ObtenerPorIdResult { get; set; }
    public IReadOnlyList<EstadoVacanteDto> ListarEstadosResult { get; set; } = [];
    public IReadOnlyList<PuestoDto> ListarPuestosResult { get; set; } = [];
    public VacanteCommandResult CrearResult { get; set; } = new(false, null, null);
    public VacanteCommandResult CambiarEstadoResult { get; set; } = new(false, null, null);

    public Exception? ListarException { get; set; }
    public Exception? ObtenerPorIdException { get; set; }
    public Exception? ListarEstadosException { get; set; }
    public Exception? ListarPuestosException { get; set; }
    public Exception? CrearException { get; set; }
    public Exception? CambiarEstadoException { get; set; }

    public List<VacanteListQuery> ListarCalls { get; } = [];
    public List<Guid> ObtenerPorIdCalls { get; } = [];
    public List<CrearVacanteRequest> CrearCalls { get; } = [];
    public List<(Guid Id, CambiarEstadoVacanteRequest Request)> CambiarEstadoCalls { get; } = [];
    public List<int> ListarPuestosCalls { get; } = [];

    public Task<PagedResult<VacanteDto>> ListarAsync(
        VacanteListQuery query,
        CancellationToken cancellationToken = default)
    {
        ListarCalls.Add(query);
        if (ListarException is not null)
        {
            throw ListarException;
        }

        return Task.FromResult(ListarHandler?.Invoke(query) ?? ListarResult);
    }

    public Task<VacanteDetailDto?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ObtenerPorIdCalls.Add(id);
        if (ObtenerPorIdException is not null)
        {
            throw ObtenerPorIdException;
        }

        return Task.FromResult(ObtenerPorIdResult);
    }

    public Task<IReadOnlyList<EstadoVacanteDto>> ListarEstadosAsync(
        CancellationToken cancellationToken = default)
    {
        if (ListarEstadosException is not null)
        {
            throw ListarEstadosException;
        }

        return Task.FromResult(ListarEstadosResult);
    }

    public Task<IReadOnlyList<PuestoDto>> ListarPuestosAsync(
        CancellationToken cancellationToken = default)
    {
        ListarPuestosCalls.Add(1);

        if (ListarPuestosException is not null)
        {
            throw ListarPuestosException;
        }

        return Task.FromResult(ListarPuestosResult);
    }

    public Task<VacanteCommandResult> CrearAsync(
        CrearVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        CrearCalls.Add(request);
        if (CrearException is not null)
        {
            throw CrearException;
        }

        return Task.FromResult(CrearResult);
    }

    public Task<VacanteCommandResult> CambiarEstadoAsync(
        Guid id,
        CambiarEstadoVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        CambiarEstadoCalls.Add((id, request));
        if (CambiarEstadoException is not null)
        {
            throw CambiarEstadoException;
        }

        return Task.FromResult(CambiarEstadoResult);
    }

    /// <summary>
    /// T-7.1 / T-7.2: tests pueden setear el resultado del helper que
    /// consulta si un Puesto tiene Vacante abierta. Default = false.
    /// </summary>
    public bool ExisteVacanteAbiertaParaPuestoResult { get; set; }

    public Task<bool> ExisteVacanteAbiertaParaPuestoAsync(
        Guid puestoId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ExisteVacanteAbiertaParaPuestoResult);

    public static VacanteDto BuildDto(
        Guid? id = null,
        Guid? puestoId = null,
        string puestoNombre = "Analista",
        Guid? estadoVacanteId = null,
        string estadoVacanteNombre = "Abierta",
        DateTime? fechaApertura = null,
        DateTime? fechaCierre = null,
        string motivo = "Cobertura de puesto",
        string? observaciones = null)
        => new(
            id ?? Guid.NewGuid(),
            puestoId ?? Guid.NewGuid(),
            puestoNombre,
            estadoVacanteId ?? Guid.NewGuid(),
            estadoVacanteNombre,
            fechaApertura ?? new DateTime(2026, 1, 15),
            fechaCierre,
            motivo,
            observaciones);

    public static VacanteDetailDto BuildDetail(
        Guid? id = null,
        Guid? puestoId = null,
        string puestoNombre = "Analista",
        Guid? estadoVacanteId = null,
        string estadoVacanteNombre = "Abierta",
        DateTime? fechaApertura = null,
        DateTime? fechaCierre = null,
        string motivo = "Cobertura de puesto",
        string? observaciones = null,
        IReadOnlyList<HistorialEstadoVacanteDto>? historial = null)
        => new(
            id ?? Guid.NewGuid(),
            puestoId ?? Guid.NewGuid(),
            puestoNombre,
            estadoVacanteId ?? Guid.NewGuid(),
            estadoVacanteNombre,
            fechaApertura ?? new DateTime(2026, 1, 15),
            fechaCierre,
            motivo,
            observaciones,
            historial ?? []);

    public static IReadOnlyList<EstadoVacanteDto> BuildStates() =>
    [
        new(Guid.NewGuid(), "ABIERTA", "Abierta", 1, false, false),
        new(Guid.NewGuid(), "EN_SELECCION", "En selección", 2, false, false),
        new(Guid.NewGuid(), "CUBIERTA", "Cubierta", 3, true, true),
        new(Guid.NewGuid(), "CANCELADA", "Cancelada", 4, true, false)
    ];
}
