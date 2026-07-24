using SGV.Contracts.Setup;
using SGV.Web.Integration.Setup;

namespace SGV.Tests.Web.Auth.Setup;

/// <summary>
/// Fake de <see cref="ISetupApiClient"/> para los tests de integración
/// del shell web. Permite configurar el status, el catálogo de
/// <c>TipoDocumento</c>, el resultado de <c>CrearAsync</c> y, en
/// algunos escenarios, lanzar excepciones de transporte.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cache de status simulada:</b> el fake cachea la primera respuesta
/// de <see cref="ObtenerEstadoAsync"/> con TTL 30s (espejo del
/// comportamiento de <see cref="SetupApiClient"/>) para que los
/// tests que verifican el fail-open post-cache hit (por ejemplo
/// <c>SetupStatusCacheTests.Get_SignIn_ApiCaeEnSegundaLlamada_FailOpenConCacheDeLaPrimera</c>)
/// no tengan que reemplazar el typed client por un <c>HttpMessageHandler</c>
/// de bajo nivel. Tests que necesitan observar el cache real deben
/// usar <see cref="SetupApiClient"/> directamente con un
/// <c>RecordingHttpMessageHandler</c>.
/// </para>
/// </remarks>
internal sealed class FakeSetupApiClient : ISetupApiClient
{
    private static readonly TimeSpan StatusTtl = TimeSpan.FromSeconds(30);
    private readonly object _cacheLock = new();
    private SetupStatusResponse? _cachedStatus;
    private DateTime _cacheExpiresAt = DateTime.MinValue;

    public SetupStatusResponse? Status { get; init; }
    public IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>? TiposDocumento { get; init; }
    public SetupHttpResult? CrearResult { get; init; }
    public Exception? CrearException { get; init; }
    public SetupRequest? LastCreateRequest { get; private set; }
    public int StatusCallCount { get; private set; }

    public Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        lock (_cacheLock)
        {
            if (_cachedStatus is not null && DateTime.UtcNow < _cacheExpiresAt)
            {
                return Task.FromResult(_cachedStatus);
            }

            // Cache miss: incrementar contador y cachear.
            StatusCallCount++;
            _cachedStatus = Status ?? new SetupStatusResponse(false);
            _cacheExpiresAt = DateTime.UtcNow + StatusTtl;
            return Task.FromResult(_cachedStatus);
        }
    }

    public Task<IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>> GetTiposDocumentoAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(TiposDocumento ?? Array.Empty<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>());

    public Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        if (CrearException is not null)
        {
            return Task.FromException<SetupHttpResult>(CrearException);
        }

        return Task.FromResult(CrearResult ?? SetupHttpResult.Success(
            new SetupResult(Guid.NewGuid(), "user", request.UserName)));
    }
}
