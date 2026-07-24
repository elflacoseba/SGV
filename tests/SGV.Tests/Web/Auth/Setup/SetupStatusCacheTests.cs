using System.Net;
using SGV.Contracts.Setup;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Auth.Setup;

/// <summary>
/// Tests de integración para el cache TTL 30s del status de setup
/// (issue #195 / WU-4 / design §2.3). Verifica que múltiples GETs a
/// <c>/auth/sign-in</c> dentro de la ventana NO generen round-trips
/// adicionales a la API.
/// </summary>
/// <remarks>
/// Estos tests usan <see cref="FakeSetupApiClient"/> que ahora
/// cachea la primera respuesta de <c>ObtenerEstadoAsync</c> con TTL
/// 30s (espejo del comportamiento del
/// <see cref="SGV.Web.Integration.Setup.SetupApiClient"/> real). Eso
/// permite verificar el efecto del cache sobre el flujo del shell
/// web sin tener que componer un <c>HttpMessageHandler</c> de bajo
/// nivel. La cobertura del cache real a nivel de cliente vive en
/// <see cref="SetupApiClientTests"/>.
/// </remarks>
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
    public async Task Get_SignIn_CacheHit_ReusaResultadoSinNuevaPeticion()
    {
        // Cache hit con RequiresSetup=true: tres GETs deben
        // redirigir a /auth/setup con UNA sola llamada a
        // ObtenerEstadoAsync (las dos siguientes son cache hit).
        var fake = new FakeSetupApiClient
        {
            Status = new SetupStatusResponse(true)
        };
        await using var lease = await _fixture.CreateSetupLeaseAsync(fake);

        var first = await lease.Client.GetAsync("/auth/sign-in");
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
}
