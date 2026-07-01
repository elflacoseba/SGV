using System.Net;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Fake en memoria de <see cref="ICargoApiClient"/> compartido por las pruebas
/// web de Cargos. Permite configurar el resultado de <c>GetAllAsync</c>,
/// forzar excepciones, registrar las invocaciones y devolver un
/// <see cref="CargoDeleteResult"/> configurable desde cada test.
/// </summary>
public sealed class FakeCargoApiClient : ICargoApiClient
{
    private readonly IReadOnlyList<CargoDto>? _getAllResult;
    private readonly Exception? _getAllException;
    private readonly HashSet<Guid> _deletedIds = new();

    /// <summary>
    /// Construye un fake vacío. Útil para los tests del seam que sólo necesitan
    /// confirmar el override del servicio en el contenedor.
    /// </summary>
    public FakeCargoApiClient()
        : this(Array.Empty<CargoDto>(), null)
    {
    }

    private FakeCargoApiClient(IReadOnlyList<CargoDto>? getAllResult, Exception? getAllException)
    {
        _getAllResult = getAllResult;
        _getAllException = getAllException;
    }

    /// <summary>
    /// Cantidad de veces que se invocó <see cref="GetAllAsync"/>.
    /// </summary>
    public List<int> GetAllCalls { get; } = new();

    /// <summary>
    /// Identificadores enviados a <see cref="DeleteAsync"/>.
    /// </summary>
    public List<Guid> DeleteCalls { get; } = new();

    /// <summary>
    /// Resultado fijo que devolverá <see cref="DeleteAsync"/>. Por defecto,
    /// éxito con 204 NoContent.
    /// </summary>
    public CargoDeleteResult DeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    /// <summary>
    /// Resultado fijo que devolverá <see cref="CreateAsync"/>. Por defecto,
    /// fallo de NotImplemented para forzar a los tests a configurarlo
    /// explícitamente cuando lo necesiten.
    /// </summary>
    public CargoCommandResult CreateResult { get; set; } = CargoCommandResult.Failure(
        new CargoError(CargoErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    /// <summary>
    /// Solicitudes recibidas por <see cref="CreateAsync"/>. Permite inspeccionar
    /// el payload enviado por la página al API en cada test.
    /// </summary>
    public List<CrearCargoRequest> CreateCalls { get; } = new();

    /// <summary>
    /// Excepción opcional que <see cref="CreateAsync"/> debe lanzar antes de
    /// devolver el resultado. Útil para simular errores de transporte.
    /// </summary>
    public Exception? CreateException { get; set; }

    /// <summary>
    /// Resultado fijo que devolverá <see cref="GetNivelesAsync"/>. Por defecto,
    /// lista vacía (el test debe configurarla cuando cargue la página Create).
    /// </summary>
    public IReadOnlyList<NivelCargoDto> NivelesResult { get; set; } = [];

    /// <summary>
    /// Excepción opcional que <see cref="GetNivelesAsync"/> debe lanzar. Útil
    /// para verificar el manejo de errores recuperables en OnGetAsync.
    /// </summary>
    public Exception? NivelesException { get; set; }

    /// <summary>
    /// Cantidad de veces que se invocó <see cref="GetNivelesAsync"/>.
    /// </summary>
    public int NivelesCalls { get; private set; }

    /// <summary>
    /// Construye un fake que devuelve la lista especificada en
    /// <see cref="GetAllAsync"/>.
    /// </summary>
    public static FakeCargoApiClient WithCargoList(params CargoDto[] cargos)
        => new(cargos, null);

    /// <summary>
    /// Construye un fake que arroja la excepción indicada en
    /// <see cref="GetAllAsync"/>.
    /// </summary>
    public static FakeCargoApiClient WithFailure(Exception exception)
        => new(null, exception);

    public Task<IReadOnlyList<CargoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls.Add(1);

        if (_getAllException is not null)
        {
            throw _getAllException;
        }

        // Las listas en memoria se devuelven filtradas según los ids
        // eliminados durante el test para reflejar el comportamiento real
        // de la API (baja lógica = el cargo ya no aparece como activo).
        IReadOnlyList<CargoDto> snapshot = _getAllResult ?? Array.Empty<CargoDto>();
        if (_deletedIds.Count > 0)
        {
            snapshot = snapshot.Where(c => !_deletedIds.Contains(c.Id)).ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_getAllResult is null)
            return Task.FromResult<CargoDto?>(null);

        if (_deletedIds.Contains(id))
            return Task.FromResult<CargoDto?>(null);

        var cargo = _getAllResult.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(cargo);
    }

    public Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);

        if (DeleteResult.Succeeded)
        {
            _deletedIds.Add(id);
        }

        return Task.FromResult(DeleteResult);
    }

    public Task<CargoCommandResult> CreateAsync(CrearCargoRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(CreateResult);
    }

    public Task<IReadOnlyList<NivelCargoDto>> GetNivelesAsync(CancellationToken cancellationToken = default)
    {
        NivelesCalls++;

        if (NivelesException is not null)
        {
            throw NivelesException;
        }

        return Task.FromResult(NivelesResult);
    }
}
