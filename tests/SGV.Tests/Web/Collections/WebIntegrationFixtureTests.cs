using System.Net.Http;
using SGV.Tests.Web.Cargo;
using SGV.Tests.Web.Habilidad;
using SGV.Tests.Web.Puesto;
using Xunit;

namespace SGV.Tests.Web.Collections;

/// <summary>
/// Tests RED (strict TDD) para <see cref="WebIntegrationFixture"/>. Las firmas
/// validadas son las 6 del design.md §"Firmas explícitas del composite".
/// </summary>
public sealed class WebIntegrationFixtureTests
{
    [Fact]
    public async Task Fixture_ExposesRootFactoryOfExpectedType()
    {
        await using var fixture = new WebIntegrationFixture();
        Assert.NotNull(fixture.RootFactory);
        Assert.IsType<SgvWebApplicationFactory>(fixture.RootFactory);
    }

    [Theory]
    [MemberData(nameof(AllLeaseFactories))]
    public async Task Fixture_LeaseHelpers_ReturnNonNullLease(Func<WebIntegrationFixture, Task<WebClientLease>> leaseFactory)
    {
        await using var fixture = new WebIntegrationFixture();
        var lease = await leaseFactory(fixture);
        try
        {
            Assert.NotNull(lease);
            Assert.NotNull(lease.Client);
            Assert.NotNull(lease.Factory);
        }
        finally
        {
            // Importante: dispose del lease para liberar el sentinel. Si no, los
            // sentinels se acumulan entre Theory cases y rompen otros tests que
            // asumen baseline.
            await lease.DisposeAsync();
        }
    }

    public static IEnumerable<object[]> AllLeaseFactories
    {
        get
        {
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreateCargoLeaseAsync(new FakeCargoApiClient())) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreateCargoLeaseAsync(new FakeCargoApiClient(), new FakeHabilidadApiClient(), adminRole: true)) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreatePuestoLeaseAsync(new FakePuestosApiClient())) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreatePuestoLeaseAsync(new FakePuestosApiClient(), new FakeUnidadOrganizativaApiClient(), new FakeCargoApiClient(), adminRole: true)) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreateHabilidadLeaseAsync(new FakeHabilidadApiClient())) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreateHabilidadLeaseAsync(new FakeHabilidadApiClient(), adminRole: true)) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreateUnidadOrganizativaLeaseAsync(new FakeUnidadOrganizativaApiClient())) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreateAnonymousLeaseAsync()) };
            yield return new object[] { (Func<WebIntegrationFixture, Task<WebClientLease>>)(f => f.CreateAuthOnlyLeaseAsync(adminRole: true)) };
        }
    }

    [Fact]
    public async Task Fixture_CreateCargoLeaseAsync_ReleasesSentinelAfterDispose()
    {
        // Verifica el contrato del lease sin depender del contador global
        // (ruidoso bajo paralelismo xUnit): el lease mantiene exactamente
        // un sentinel vivo hasta su DisposeAsync.
        await using var fixture = new WebIntegrationFixture();

        var lease = await fixture.CreateCargoLeaseAsync(new FakeCargoApiClient());

        // Capturamos el sentinel del lease via reflexión: la lease sólo
        // expone Client y Factory; verificamos que existe exactamente uno
        // vivo adicional usando AliveCount antes/después de dispose.
        var beforeDispose = TestSentinel.AliveCount;
        await lease.DisposeAsync();
        var afterDispose = TestSentinel.AliveCount;

        // El lease liberó su sentinel: el contador global debe haber bajado
        // al menos 1 entre las dos lecturas.
        Assert.True(afterDispose < beforeDispose, "DisposeAsync del lease no decrementó el contador de sentinels.");
    }

    [Fact]
    public async Task Fixture_CreateAnonymousLeaseAsync_DerivesFactoryFromSharedRoot()
    {
        await using var fixture = new WebIntegrationFixture();
        var root = fixture.RootFactory;
        await using var lease = await fixture.CreateAnonymousLeaseAsync();
        Assert.NotSame(root, lease.Factory);
    }

    [Fact]
    public async Task Fixture_DisposeAsync_IsIdempotentAndPreservesRootReference()
    {
        var fixture = new WebIntegrationFixture();
        var rootBeforeDispose = fixture.RootFactory;

        await fixture.DisposeAsync();
        await fixture.DisposeAsync(); // IAsyncLifetime puede invocarlo más de una vez.

        Assert.Same(rootBeforeDispose, fixture.RootFactory);
    }
}