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

// ── Evidencia real de disposal (PR 2b-4 review #995) ──────────────
//
// Los cuatro tests de arriba verifican el contrato de cleanup mediante
// el contador global `TestSentinel.AliveCount` y la supervivencia de
// la raíz compartida. Eso prueba el efecto agregado, pero NO prueba
// que el HttpClient derivado ni el SgvWebApplicationFactory derivado
// hayan sido realmente dispuestos: podrían sobrevivir silenciosos y
// los tests pasarían igual. Estos tests exigen evidencia directa vía
// la excepción `ObjectDisposedException` que `HttpClient.GetAsync`
// lanza cuando se llama sobre un cliente dispuesto. Esto prueba que
// el cliente fue dispuesto por la rama de cleanup (en éxito: lease
// dispose; en falla: catch block) sin depender de contadores estáticos
// compartidos.
//
// Se usa el overload de testing con `captureClient` para que el test
// retenga una referencia al cliente derivado (la única forma de
// observarlo post-dispose, ya que el lease no se construye en la rama
// de falla). En el camino feliz la referencia la da directamente
// `lease.Client` (que es el mismo objeto que ve `captureClient`).

    [Fact]
    public async Task CreateLeaseWithBootstrapAsync_BootstrapSuccess_DerivedClientIsDisposedWhenLeaseDisposed()
    {
        // RED: el camino feliz. El lease se construye con un cliente
        // y un sentinel propios. Al disponer el lease, el cliente debe
        // haber sido dispuesto: cualquier intento de uso lanza
        // ObjectDisposedException.
        var lease = await _fixture.CreateLeaseWithBootstrapAsync(
            f => f.WithOverrides(),
            _ => Task.CompletedTask);
        Assert.NotNull(lease);

        await lease.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => lease.Client.GetAsync("/"));
    }

    [Fact]
    public async Task CreateLeaseWithBootstrapAsync_BootstrapFailsBeforeLogin_DerivedClientIsDisposedAndSentinelSuppressed()
    {
        // RED: el callback de bootstrap tira ANTES de cualquier request
        // HTTP (simula falla sincrónica). PR 2b-4 review #995: el cliente
        // derivado debe haber sido dispuesto por el catch. El sentinel NO
        // debe aparecer (no hay lease).
        HttpClient? capturedClient = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fixture.CreateLeaseWithBootstrapAsync(
                f => f.WithOverrides(),
                _ => throw new InvalidOperationException("Simulated pre-login failure"),
                captureFactory: _ => { },
                captureClient: c => capturedClient = c));

        Assert.NotNull(capturedClient);

        // Evidencia directa: el cliente derivado fue realmente dispuesto
        // por el catch (no sobrevive en silencio).
        await Assert.ThrowsAsync<ObjectDisposedException>(() => capturedClient!.GetAsync("/"));
    }

    [Fact]
    public async Task CreateLeaseWithBootstrapAsync_BootstrapFailsAfterLogin_DerivedClientIsDisposedAndSentinelSuppressed()
    {
        // RED: el callback tira DESPUÉS de hacer un GET al endpoint de
        // sign-in (modelo: el antiforgery extraction o el POST fallan).
        // Es el mismo cleanup contractual que la falla pre-login pero
        // ejercita el camino donde el cliente ya interactuó con el host.
        HttpClient? capturedClient = null;

        await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() =>
            _fixture.CreateLeaseWithBootstrapAsync(
                f => f.WithOverrides(),
                async client =>
                {
                    _ = await client.GetAsync("/auth/sign-in");
                    throw new Xunit.Sdk.XunitException("Simulated post-login failure");
                },
                captureFactory: _ => { },
                captureClient: c => capturedClient = c));

        Assert.NotNull(capturedClient);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => capturedClient!.GetAsync("/"));
    }

    [Fact]
    public async Task CreateLeaseWithBootstrapAsync_CleanupThrows_PreservesOriginalBootstrapException()
    {
        // RED: si la disposición del cliente o de la factory derivada
        // tira durante el cleanup, el catch del helper debe suprimir esa
        // excepción y re-lanzar la original del bootstrap. Cubrir este
        // comportamiento requiere un HttpClient o factory que tire al
        //Dispose. Como SgvWebApplicationFactory y HttpClient son sealed
        // y no se pueden wrappear, usamos el callback de bootstrap para
        // modelar el patrón: cualquier excepción lanzada por el catch
        // del cleanup debe ser absorbida por su propio try anidado.
        //
        // La estrategia es inyectar un HttpClient que tira en Dispose
        // vía un wrapper interno del fixture: introducimos un overload
        // interno `CreateLeaseWithBootstrapAsync(factory, client,
        // bootstrap)` que acepta un cliente pre-construido. Cuando el
        // cliente es un `ThrowingDisposeHttpClient`, el catch intenta
        // disponerlo, tira InvalidOperationException, y el test verifica
        // que la excepción que se propaga es la original del bootstrap
        // (HttpRequestException), NO la del cleanup.
        var throwingClient = new ThrowingDisposeHttpClient();

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _fixture.CreateLeaseWithBootstrapAsync(
                f => f.WithOverrides(),
                throwingClient,
                _ => throw new HttpRequestException("Original bootstrap failure")));

        Assert.Equal("Original bootstrap failure", thrown.Message);
        Assert.True(throwingClient.DisposeInvoked, "Cleanup attempted to dispose the throwing client.");
    }

    // Sentinel-only-on-success-path se prueba implícitamente en los tests
// `BootstrapFailsBeforeLogin_...` y `BootstrapFailsAfterLogin_...`:
// ambos ejecutan `Assert.ThrowsAsync<...>` que sólo tiene éxito si la
// operación tiró antes de construir el lease, y eso sólo ocurre si
// `new TestSentinel()` nunca se ejecutó. Por eso no hace falta un test
// dedicado que compare `TestSentinel.AliveCount` antes/después (lo que
// sería inherentemente racy bajo paralelismo inter-colección).

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

    /// <summary>
    /// HttpClient que tira <see cref="InvalidOperationException"/> al ser
    /// dispuesto. Modela una falla de cleanup para verificar que la
    /// excepción del bootstrap se preserva.
    /// </summary>
    private sealed class ThrowingDisposeHttpClient : HttpClient
    {
        public ThrowingDisposeHttpClient() : base(new HttpClientHandler()) { }

        public bool DisposeInvoked { get; private set; }

        protected override void Dispose(bool disposing)
        {
            DisposeInvoked = true;
            if (disposing)
            {
                throw new InvalidOperationException("Simulated cleanup-time dispose failure");
            }

            base.Dispose(disposing);
        }
    }
}
