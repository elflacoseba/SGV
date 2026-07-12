using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using SGV.Tests.Web.Cargo;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Habilidad;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Tests RED (strict TDD) que validan la cadena de delegación de
/// <see cref="PuestoWebTestFixture"/> al composite
/// <see cref="WebIntegrationFixture"/>. Cada lease producido por el fixture
/// MUST cumplir tres invariantes observables:
///   1) proviene de una factory derivada, distinta de la raíz del fixture;
///   2) retiene exactamente un <see cref="TestSentinel"/> y lo libera al hacer
///      <see cref="WebClientLease.DisposeAsync"/> sin detener la raíz compartida;
///   3) expone los overrides configurados al resolver servicios en su factory.
/// Si <see cref="PuestoWebTestFixture"/> deja de delegar al composite, estos
/// tests rompen antes que los call sites de páginas, exponiendo drift durante
/// el refactor.
///
/// Política de dispose: ningún test invoca manualmente
/// <see cref="WebClientLease.DisposeAsync"/>. Cada lease nace dentro de un
/// <c>await using</c> y se libera exclusivamente cuando el scope cierra. Para
/// los tests que necesitan comprobar el contador global tras el dispose (los
/// cuatro de la familia "ReturnsLeaseWithDerivedFactoryAndOwnsSentinel") se
/// usa un bloque interno anidado: las aserciones de vida útil quedan dentro
/// del scope, y la verificación del sentinel liberado queda afuera, justo
/// después de la llave de cierre que dispara el <c>DisposeAsync</c> implícito.
/// Esto elimina el doble dispose y deja la cobertura del comportamiento al
/// <c>await using</c>, no a una llamada manual redundante.
/// </summary>
public sealed class PuestoWebTestFixtureLeaseContractTests
{
    // ── Contrato por firma: lease + sentinel + factory derivada ────────

    [Fact]
    public async Task CreateAuthenticatedClientAsync_ReturnsLeaseWithDerivedFactoryAndOwnsSentinel()
    {
        var baseline = TestSentinel.AliveCount;

        await using var fixture = new PuestoWebTestFixture();
        {
            await using var lease = await fixture.CreateAuthenticatedClientAsync(new FakePuestosApiClient());

            // La lease debe provenir de una factory distinta de la raíz del fixture.
            Assert.NotSame(fixture.RootFactory, lease.Factory);
            Assert.NotNull(lease.Client);
            // El lease debe haber retenido exactamente un sentinel durante su vida.
            Assert.Equal(baseline + 1, TestSentinel.AliveCount);
        }
        // Al cerrar el bloque interno, el `await using` invoca
        // `WebClientLease.DisposeAsync()` una sola vez: el sentinel baja
        // exactamente una vez al baseline.
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public async Task CreateAdminClientAsync_WithFakeOnly_ReturnsLeaseWithDerivedFactoryAndOwnsSentinel()
    {
        var baseline = TestSentinel.AliveCount;

        await using var fixture = new PuestoWebTestFixture();
        {
            await using var lease = await fixture.CreateAdminClientAsync(new FakePuestosApiClient());

            Assert.NotSame(fixture.RootFactory, lease.Factory);
            Assert.NotNull(lease.Client);
            Assert.Equal(baseline + 1, TestSentinel.AliveCount);
        }
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public async Task CreateAdminClientAsync_WithThreeFakes_ReturnsLeaseWithDerivedFactoryAndOwnsSentinel()
    {
        var baseline = TestSentinel.AliveCount;

        await using var fixture = new PuestoWebTestFixture();
        {
            await using var lease = await fixture.CreateAdminClientAsync(
                new FakeUnidadOrganizativaApiClient(),
                new FakeCargoApiClient(),
                new FakePuestosApiClient());

            Assert.NotSame(fixture.RootFactory, lease.Factory);
            Assert.NotNull(lease.Client);
            Assert.Equal(baseline + 1, TestSentinel.AliveCount);
        }
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    [Fact]
    public async Task CreateAuthenticatedClientAsync_FourArgOverload_ReturnsLeaseWithDerivedFactoryAndOwnsSentinel()
    {
        var baseline = TestSentinel.AliveCount;

        await using var fixture = new PuestoWebTestFixture();
        {
            await using var lease = await fixture.CreateAuthenticatedClientAsync(
                new FakeUnidadOrganizativaApiClient(),
                new FakeCargoApiClient(),
                new FakePuestosApiClient(),
                adminRole: false);

            Assert.NotSame(fixture.RootFactory, lease.Factory);
            Assert.NotNull(lease.Client);
            Assert.Equal(baseline + 1, TestSentinel.AliveCount);
        }
        Assert.Equal(baseline, TestSentinel.AliveCount);
    }

    // ── Aislamiento de dispose: la raíz compartida debe sobrevivir ─────

    [Fact]
    public async Task Lease_DisposeAsync_DoesNotDisposeSharedRoot()
    {
        await using var fixture = new PuestoWebTestFixture();

        // El primer lease debe liberarse ANTES de construir el segundo para
        // verificar que la raíz compartida sobrevive. Se usa un bloque
        // interno anidado en lugar de un `await firstLease.DisposeAsync()`
        // manual: el `await using` cierra al final del bloque y dispara el
        // dispose una sola vez, manteniendo la política "ningún dispose
        // manual" de este archivo.
        {
            await using var firstLease = await fixture.CreateAuthenticatedClientAsync(new FakePuestosApiClient());
        }

        // Después de disponer la primera lease, la raíz compartida debe seguir
        // operativa: una segunda lease construida a partir del MISMO fixture
        // debe producir un cliente capaz de resolver rutas autenticadas.
        await using var secondLease = await fixture.CreateAuthenticatedClientAsync(new FakePuestosApiClient());
        using var response = await secondLease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Override observable: el fake configurado llega a DI ───────────

    [Fact]
    public async Task Lease_ConfiguredOverride_IsObservableThroughFactoryServices()
    {
        var fakePuestos = FakePuestosApiClient.WithPuestoList(
            WebTestBuilders.BuildPuestoDto("P-001", "Analista", null, null));

        await using var fixture = new PuestoWebTestFixture();
        await using var lease = await fixture.CreateAdminClientAsync(fakePuestos);

        // La override configurada por el fixture debe quedar registrada en los
        // servicios de la factory derivada del lease: al resolver
        // IPuestosApiClient, recuperamos EXACTAMENTE el fake que pasamos y los
        // datos sembrados en él son los que verá la página que invoque la API.
        using var scope = lease.Factory.Services.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<IPuestosApiClient>();

        Assert.Same(fakePuestos, resolved);
        Assert.Single(fakePuestos.GetAllResult);
    }
}
