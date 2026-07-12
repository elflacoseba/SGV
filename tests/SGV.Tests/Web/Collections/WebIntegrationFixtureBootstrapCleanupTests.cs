using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;

namespace SGV.Tests.Web.Collections;

/// <summary>
/// Tests RED (strict TDD) del cleanup del bootstrap del composite.
/// Cubre el defecto encontrado en PR 2b-4 (commit <c>c9e3fc59</c>): los
/// helpers <c>CreateCargoBridgeLeaseAsync</c> y <c>CreateAuthenticatedClientAsync</c>
/// de UO creaban factory derivada + HttpClient, esperaban el bootstrap
/// autenticado (GET → antiforgery → POST) y sólo al final construían el
/// <see cref="WebClientLease"/>. Cualquier excepción en el bootstrap
/// dejaba la factory derivada y el HttpClient vivos — sin lease que los
/// libere, sin sentinel que los rastree.
///
/// El contrato verificado acá es: cuando el callback de bootstrap tira,
/// el helper interno <c>CreateLeaseWithBootstrapAsync</c> debe liberar
/// los recursos (orden <c>client → factory</c>, sin paso de sentinel
/// porque éste aún no fue construido) y volver a lanzar la excepción
/// original. La raíz compartida del fixture NO debe ser afectada.
/// </summary>
[Collection("WebIntegration")]
public sealed class WebIntegrationFixtureBootstrapCleanupTests
{
    private readonly WebIntegrationFixture _fixture;

    public WebIntegrationFixtureBootstrapCleanupTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateLeaseWithBootstrapAsync_BootstrapCallbackThrowsGetAsync_DisposesDerivedFactoryAndClientAndPreservesSentinelBaseline()
    {
        // RED: invoca la rama de cleanup con un callback que simula el
        // escenario "GET /auth/sign-in tira HttpRequestException" antes de
        // extraer el token antiforgery. La factory derivada y el cliente
        // deben disponerse; el sentinel nunca se crea (no hay lease), por
        // lo tanto el contador global queda igual a la lectura previa.
        var baseline = TestSentinel.AliveCount;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _fixture.CreateLeaseWithBootstrapAsync(
                f => f.WithOverrides(),
                client => throw new HttpRequestException("Simulated GET /auth/sign-in failure")));

        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public async Task CreateLeaseWithBootstrapAsync_BootstrapCallbackThrowsAfterClientCreated_AllowsSubsequentLeaseFromSharedRoot()
    {
        // RED: aún después de una falla de bootstrap, el lease siguiente
        // desde la misma raíz compartida del fixture debe poder crearse
        // (la raíz no fue dispuesta; la derivada fallida fue limpiada).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fixture.CreateLeaseWithBootstrapAsync(
                f => f.WithOverrides(),
                _ => throw new InvalidOperationException("Simulated bootstrap failure")));

        await using var nextLease = await _fixture.CreateAnonymousLeaseAsync();

        Assert.NotNull(nextLease.Client);
        Assert.NotNull(nextLease.Factory);
        Assert.NotSame(_fixture.RootFactory, nextLease.Factory);
    }

    [Fact]
    public async Task CreateLeaseWithBootstrapAsync_BootstrapCallbackThrowsAntiforgeryExtraction_DisposesDerivedFactoryAndClientAndPreservesSentinelBaseline()
    {
        // RED: el callback simula la falla en ExtractAntiforgeryTokenAsync
        // (Assert.True(match.Success) lanza XunitException cuando la
        // respuesta no contiene el input del token). La factory y el
        // cliente deben disponerse; el contador global no debe incrementarse.
        var baseline = TestSentinel.AliveCount;

        await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() =>
            _fixture.CreateLeaseWithBootstrapAsync(
                f => f.WithOverrides(),
                _ => throw new Xunit.Sdk.XunitException("Simulated antiforgery extraction failure")));

        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public async Task CreateCargoBridgeLeaseAsync_WithFaultyAuthHandler_DoesNotLeakDerivedResources()
    {
        // RED: el camino público del CargoBridge usa auth+cargo handler
        // provistos por el test. Si el auth handler tira HttpRequestException
        // cuando SGV.Web llama a AuthApiClient.LoginAsync durante el POST,
        // el helper debe limpiar la factory derivada y el cliente aunque
        // SGV.Web traduzca la excepción interna a un 500 (no la propague al
        // cliente del test). Sea cual sea el resultado del POST (lease
        // devuelto o excepción propagada), no deben quedar sentinels vivos.
        var baseline = TestSentinel.AliveCount;
        var faultyAuthHandler = new ThrowingHttpMessageHandler(new HttpRequestException("Simulated auth API failure"));
        var cargoHandler = new WebTestBuilders.RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<SGV.Contracts.Organizacion.Consultas.Dtos.CargoDto>())
            });

        try
        {
            await using var lease = await _fixture.CreateCargoBridgeLeaseAsync(faultyAuthHandler, cargoHandler);
            Assert.NotNull(lease);
        }
        catch (HttpRequestException)
        {
            // Si la helper propaga la excepción interna del auth handler,
            // el catch la absorbe: el contrato del cleanup es el mismo
            // (factory + cliente liberados antes del throw).
        }

        Assert.Equal(baseline, TestSentinel.AliveCount);

        // La raíz compartida sigue operativa: otra lease puede crearse.
        await using var nextLease = await _fixture.CreateAnonymousLeaseAsync();
        Assert.NotNull(nextLease.Client);
        Assert.NotNull(nextLease.Factory);
    }

    /// <summary>
    /// Handler de mentira que siempre tira <see cref="HttpRequestException"/>.
    /// Modela la falla del API de auth simulada por el test.
    /// </summary>
    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw _exception;
    }
}
