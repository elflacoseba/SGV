using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Setup;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Setup;
using Xunit;

namespace SGV.Tests.Web.Auth.Setup;

/// <summary>
/// Tests de integración para el cache TTL 30s del status de setup
/// (issue #195 / WU-4 / design §2.3). Verifica que múltiples GETs a
/// <c>/auth/sign-in</c> dentro de la ventana NO generen round-trips
/// adicionales a la API.
/// </summary>
[Collection("WebIntegration")]
public sealed class SetupStatusCacheTests
{
    private readonly WebIntegrationFixture _fixture;

    public SetupStatusCacheTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_SignIn_TresLlamadasEnVentana_UnaSolaPeticionAlApi()
    {
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(false)
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        await lease.Client.GetAsync("/auth/sign-in");
        await lease.Client.GetAsync("/auth/sign-in");
        await lease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(1, fake.StatusCallCount);
    }

    [Fact]
    public async Task Get_SignIn_ApiCaeEnSegundaLlamada_FailOpenConCacheDeLaPrimera()
    {
        // La primera llamada trae RequiresSetup=true (cache miss).
        // La segunda llamada el fake tira HttpRequestException pero
        // el cache hit (primer miss ya cacheado) debe evitar el
        // segundo round-trip y devolver el valor real.
        var fake = new SequenceFakeSetupApiClient();
        fake.NextStatus = new SetupStatusResponse(true);
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var first = await lease.Client.GetAsync("/auth/sign-in");

        // El fake ahora tira HttpRequestException para futuros
        // status calls, pero el cache hit debería ganar.
        fake.ThrowOnNextStatus = true;
        var second = await lease.Client.GetAsync("/auth/sign-in");
        var third = await lease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal("/auth/setup", first.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal("/auth/setup", second.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, third.StatusCode);
        Assert.Equal("/auth/setup", third.Headers.Location!.OriginalString);
        Assert.Equal(1, fake.StatusCallCount);
    }

    private sealed class SequenceFakeSetupApiClient : ISetupApiClient
    {
        public SetupStatusResponse? NextStatus { get; set; }
        public bool ThrowOnNextStatus { get; set; }
        public int StatusCallCount { get; private set; }

        public Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken cancellationToken = default)
        {
            StatusCallCount++;
            if (ThrowOnNextStatus)
            {
                throw new HttpRequestException("simulated API outage");
            }

            return Task.FromResult(NextStatus ?? new SetupStatusResponse(false));
        }

        public Task<IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>> GetTiposDocumentoAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>>(
                Array.Empty<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>());

        public Task<SetupHttpResult> CrearAsync(SetupRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(SetupHttpResult.Success(new SetupResult(Guid.NewGuid(), "u", "u")));
    }
}
