using SGV.Contracts.Setup;
using SGV.Web.Integration.Setup;

namespace SGV.Tests.Web.Auth.Setup;

/// <summary>
/// Fake de <see cref="ISetupApiClient"/> para los tests de integración
/// del shell web. Permite configurar el status, el catálogo de
/// <c>TipoDocumento</c>, el resultado de <c>CrearAsync</c> y, en
/// algunos escenarios, lanzar excepciones de transporte.
/// </summary>
internal sealed class FakeSetupApiClient : ISetupApiClient
{
    public SetupStatusResponse? Status { get; init; }
    public IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>? TiposDocumento { get; init; }
    public SetupHttpResult? CrearResult { get; init; }
    public Exception? CrearException { get; init; }
    public SetupRequest? LastCreateRequest { get; private set; }
    public int StatusCallCount { get; private set; }

    public Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
    {
        StatusCallCount++;
        return Task.FromResult(Status ?? new SetupStatusResponse(false));
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
