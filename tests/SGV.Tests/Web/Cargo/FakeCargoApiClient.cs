using System.Net;
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

        return Task.FromResult(_getAllResult ?? Array.Empty<CargoDto>());
    }

    public Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<CargoDto?>(null);

    public Task<CargoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);
        return Task.FromResult(DeleteResult);
    }
}