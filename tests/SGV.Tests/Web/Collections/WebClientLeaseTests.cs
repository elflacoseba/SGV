using System.Net;
using System.Net.Http;
using Xunit;

namespace SGV.Tests.Web.Collections;

/// <summary>
/// Tests RED (strict TDD) para <see cref="WebClientLease"/> y <see cref="TestSentinel"/>.
/// El orden <c>client → sentinel → factory</c> se valida con un
/// <see cref="OrderRecordingHandler"/> que captura <see cref="TestSentinel.AliveCount"/>
/// en su <c>Dispose(bool)</c>: como <see cref="HttpClient"/> se construye con
/// <c>disposeHandler: true</c> por defecto, ese callback se ejecuta
/// sincrónicamente mientras la pila está dentro de <c>client.Dispose()</c>.
/// </summary>
public sealed class WebClientLeaseTests
{
    [Fact]
    public void TestSentinel_CtorAndDispose_BalanceAliveCount()
    {
        var baseline = TestSentinel.AliveCount;
        using (var s = new TestSentinel())
        {
            Assert.Equal(baseline + 1, TestSentinel.AliveCount);
        }
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public void TestSentinel_MultipleInstances_BalanceOutOfOrderDispose()
    {
        // Interlocked garantiza baseline aunque se dispongan en orden inverso.
        var baseline = TestSentinel.AliveCount;
        var a = new TestSentinel();
        var b = new TestSentinel();
        var c = new TestSentinel();
        Assert.Equal(baseline + 3, TestSentinel.AliveCount);
        c.Dispose();
        a.Dispose();
        b.Dispose();
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public async Task FixtureAnonymousLease_DisposeAsync_DoesNotStopSiblingLeaseOrSharedRoot()
    {
        await using var fixture = new WebIntegrationFixture();
        var firstLease = await fixture.CreateAnonymousLeaseAsync();
        await using var secondLease = await fixture.CreateAnonymousLeaseAsync();

        await firstLease.DisposeAsync();

        using var siblingResponse = await secondLease.Client.GetAsync("/auth/sign-in");
        using var rootClient = fixture.RootFactory.CreateClient();
        using var rootResponse = await rootClient.GetAsync("/auth/sign-in");
        Assert.Equal(HttpStatusCode.OK, siblingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
    }

    [Fact]
    public async Task FixtureDerivedLease_DisposeAsync_DoesNotStopSharedRoot()
    {
        await using var fixture = new WebIntegrationFixture();
        var derivedLease = await fixture.CreateAuthOnlyLeaseAsync();

        await derivedLease.DisposeAsync();

        using var rootClient = fixture.RootFactory.CreateClient();
        using var response = await rootClient.GetAsync("/auth/sign-in");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Lease_DisposeAsync_ReleasesSentinelAndDisposesClient()
    {
        var baseline = TestSentinel.AliveCount;
        var lease = await CreateLeaseAsync();
        var client = lease.Client;
        Assert.Equal(baseline + 1, TestSentinel.AliveCount);

        await lease.DisposeAsync();

        Assert.Equal(baseline, TestSentinel.AliveCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetAsync("/"));
    }

    [Fact]
    public async Task Lease_DisposeAsync_DisposesClientBeforeSentinel()
    {
        // Riesgo medido en design.md §"Riesgos": si el host se detiene antes
        // de cerrar el HttpClient, el socket queda colgado. Factory va última
        // por contrato de diseño (SgvWebApplicationFactory es sealed).
        var lease = await CreateObservableLeaseAsync();
        await lease.DisposeAsync();

        Assert.True(LeaseOrderProbe.ClientDisposedObserved);
        Assert.Equal("client_first", LeaseOrderProbe.OrderingDetected);
    }

    [Fact]
    public async Task Lease_DisposeAsync_MultipleLeasesAllSentinelsReleased()
    {
        var baseline = TestSentinel.AliveCount;
        var lease1 = await CreateLeaseAsync();
        var lease2 = await CreateLeaseAsync();
        var lease3 = await CreateLeaseAsync();
        Assert.Equal(baseline + 3, TestSentinel.AliveCount);

        await lease1.DisposeAsync();
        Assert.Equal(baseline + 2, TestSentinel.AliveCount);
        await lease3.DisposeAsync();
        Assert.Equal(baseline + 1, TestSentinel.AliveCount);
        await lease2.DisposeAsync();
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    // ── Idempotencia de dispose ────────────────────────────────────
    //
    // RED: estos tests prueban que el dispose doble (manual + scope `await
    // using`, o llamado dos veces explícitamente) no debe degradar
    // `TestSentinel.AliveCount`. El bug histórico: `WebClientLease` y
    // `TestSentinel` no eran idempotentes, así que cada contrato de Puesto
    // podía decrementar el contador global dos veces y contaminar el estado
    // compartido de los demás tests de la colección `WebIntegration`.

    [Fact]
    public async Task Lease_DisposeAsync_CalledTwice_KeepsAliveCountStable()
    {
        var baseline = TestSentinel.AliveCount;
        var lease = await CreateLeaseAsync();
        Assert.Equal(baseline + 1, TestSentinel.AliveCount);

        await lease.DisposeAsync();
        Assert.Equal(baseline, TestSentinel.AliveCount);

        // Segunda llamada de dispose: NO debe volver a decrementar el
        // contador global. Si lo hace, otros tests ven `AliveCount` falsamente
        // bajo y la siguiente suite arranca con estado contaminado.
        await lease.DisposeAsync();
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public void TestSentinel_Dispose_CalledTwice_KeepsAliveCountStable()
    {
        var baseline = TestSentinel.AliveCount;
        var sentinel = new TestSentinel();
        Assert.Equal(baseline + 1, TestSentinel.AliveCount);

        sentinel.Dispose();
        Assert.Equal(baseline, TestSentinel.AliveCount);

        // Segunda llamada de dispose: NO debe volver a decrementar.
        sentinel.Dispose();
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    private static Task<WebClientLease> CreateLeaseAsync()
        => Task.FromResult(new WebClientLease(
            new SgvWebApplicationFactory(),
            new HttpClient(new HttpClientHandler()),
            new TestSentinel()));

    private static Task<WebClientLease> CreateObservableLeaseAsync()
    {
        LeaseOrderProbe.Reset();
        return Task.FromResult(new WebClientLease(
            new SgvWebApplicationFactory(),
            new HttpClient(new OrderRecordingHandler()),
            new TestSentinel()));
    }
}

internal static class LeaseOrderProbe
{
    private static int _aliveAtDispose = -1;
    private static bool _observed;

    public static bool ClientDisposedObserved => _observed;

    // Cuando el handler se dispone con el sentinel aún vivo ⇒ client primero.
    public static string OrderingDetected
        => _aliveAtDispose > TestSentinel.AliveCount ? "client_first" : "sentinel_first_or_tied";

    public static void Reset() { _aliveAtDispose = -1; _observed = false; }

    public static void Record()
    {
        _aliveAtDispose = TestSentinel.AliveCount;
        _observed = true;
    }
}

internal sealed class OrderRecordingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

    protected override void Dispose(bool disposing)
    {
        if (disposing) LeaseOrderProbe.Record();
        base.Dispose(disposing);
    }
}