using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Tests RED (strict TDD) del cleanup del bootstrap autenticado del helper
/// privado <c>CreateAuthenticatedClientAsync</c> que vive en los archivos
/// parciales de <see cref="UnidadOrganizativaWebTests"/>. Cubre el defecto
/// encontrado en PR 2b-4 (commit <c>c9e3fc59</c>): el helper creaba
/// factory derivada + HttpClient, esperaba el bootstrap autenticado y sólo
/// al final construía el <see cref="WebClientLease"/>. Una excepción en
/// el bootstrap dejaba la factory y el cliente sin disposición.
///
/// La verificación del contrato del cleanup se hace en dos frentes:
/// <list type="bullet">
///   <item>Directo: invoca <c>WebIntegrationFixture.CreateLeaseWithBootstrapAsync</c>
///   con la misma configuración de factory que usa el helper de UO (auth
///   handler + fake UO + base URL) y un callback que tira, demostrando
///   que el contrato del composite se sostiene para el camino de UO.</item>
///   <item>Indirecto: tras la falla, una nueva lease anónima desde la
///   misma raíz compartida confirma que la raíz no fue afectada.</item>
/// </list>
/// </summary>
public sealed partial class UnidadOrganizativaWebTests
{
    [Fact]
    public async Task HelperBootstrap_BootstrapCallbackThrows_DisposesDerivedFactoryAndClientAndPreservesSentinelBaseline()
    {
        // RED: configura el helper de UO con un auth handler válido y un
        // fake de API vacío, pero reemplaza el callback de bootstrap por
        // uno que tira InvalidOperationException. La factory derivada y el
        // cliente deben disponerse; el contador global no debe incrementarse.
        // Evidencia directa vía el cliente capturado (que debe lanzar
        // ObjectDisposedException al usarse post-dispose).
        var authHandler = new WebTestBuilders.RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(AdminJwtTestHelper.BuildUserJwt(), DateTimeOffset.UtcNow.AddHours(1)))
            });
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages();
        var baseline = TestSentinel.AliveCount;
        HttpClient? capturedClient = null;
        SgvWebApplicationFactory? capturedFactory = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fixture.CreateLeaseWithBootstrapAsync(
                f => f.WithOverrides(
                    configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
                    authApiHandler: authHandler,
                    unidadOrganizativaApiClient: apiClient),
                _ => throw new InvalidOperationException("Simulated UO bootstrap failure"),
                captureFactory: f => capturedFactory = f,
                captureClient: c => capturedClient = c));

        Assert.Equal(baseline, TestSentinel.AliveCount);
        Assert.NotNull(capturedClient);
        Assert.NotNull(capturedFactory);

        // Evidencia directa: el cliente derivado fue dispuesto por el catch
        // (no sobrevive en silencio). `GetAsync` lanza ObjectDisposedException.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => capturedClient!.GetAsync("/"));

        // Una lease posterior desde la misma raíz compartida debe poder
        // crearse: la raíz del fixture no fue dispuesta por la falla.
        await using var nextLease = await _fixture.CreateAnonymousLeaseAsync();
        Assert.NotNull(nextLease.Client);
        Assert.NotNull(nextLease.Factory);
    }

    [Fact]
    public async Task HelperBootstrap_BootstrapSuccess_DisposesDerivedClientFactoryAndSentinelWhenLeaseDisposed()
    {
        // RED: triangulación del camino feliz específico del helper de UO.
        // El auth handler válido y el fake de API configurados como en el
        // helper privado. El lease se construye con esos overrides; al
        // disponerlo, los recursos derivados deben quedar liberados. Esto
        // prueba que la configuración concreta del helper de UO (auth
        // handler + fake de UO + base URL) cierra correctamente el
        // contrato de cleanup.
        var authHandler = new WebTestBuilders.RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(AdminJwtTestHelper.BuildUserJwt(), DateTimeOffset.UtcNow.AddHours(1)))
            });
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages();
        var sentinelBaseline = TestSentinel.AliveCount;
        HttpClient? capturedClient = null;

        var lease = await _fixture.CreateLeaseWithBootstrapAsync(
            f => f.WithOverrides(
                configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
                authApiHandler: authHandler,
                unidadOrganizativaApiClient: apiClient),
            _ => Task.CompletedTask,
            captureFactory: _ => { },
            captureClient: c => capturedClient = c);

        Assert.NotNull(lease);
        Assert.NotNull(capturedClient);
        Assert.Equal(sentinelBaseline + 1, TestSentinel.AliveCount);

        await lease.DisposeAsync();

        Assert.Equal(sentinelBaseline, TestSentinel.AliveCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => capturedClient!.GetAsync("/"));
    }
}
