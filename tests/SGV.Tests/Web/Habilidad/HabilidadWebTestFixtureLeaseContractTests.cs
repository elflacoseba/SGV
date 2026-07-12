using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests RED (strict TDD) que validan la cadena de delegación de
/// <see cref="HabilidadWebTestFixture"/> al composite
/// <see cref="WebIntegrationFixture"/>. Cada lease producido por el fixture
/// MUST cumplir tres invariantes observables:
///   1) proviene de una factory derivada, distinta de la raíz del fixture;
///   2) retiene exactamente un <see cref="TestSentinel"/> y lo libera al hacer
///      <see cref="WebClientLease.DisposeAsync"/> sin detener la raíz compartida;
///   3) expone los overrides configurados al resolver servicios en su factory.
/// Si <see cref="HabilidadWebTestFixture"/> deja de delegar al composite, estos
/// tests rompen antes que los call sites de páginas, exponiendo drift durante
/// el refactor. Espejo de <c>PuestoWebTestFixtureLeaseContractTests</c>.
///
/// Política de dispose: ningún test invoca manualmente
/// <see cref="WebClientLease.DisposeAsync"/>. Cada lease nace dentro de un
/// <c>await using</c> y se libera exclusivamente cuando el scope cierra. Para
/// el test que necesita comprobar el contador global tras el dispose se usa un
/// bloque interno anidado: las aserciones de vida útil quedan dentro del scope,
/// y la verificación del sentinel liberado queda afuera, justo después de la
/// llave de cierre que dispara el <c>DisposeAsync</c> implícito.
///
/// La clase se une a <c>[Collection("WebIntegration")]</c> para serializarse
/// frente a los PageTests del mismo grupo: la aserción de balance sobre el
/// contador global <see cref="TestSentinel.AliveCount"/> sería no determinista
/// si un PageTest creara/liberara su lease en paralelo entre la captura del
/// baseline y la aserción. La serialización elimina esa carrera sin recurrir a
/// aserciones triviales de no-nulidad.
/// </summary>
[Collection("WebIntegration")]
public sealed class HabilidadWebTestFixtureLeaseContractTests
{
    // ── Contrato de firma: lease + sentinel + factory derivada ─────────

    [Fact]
    public async Task CreateAuthenticatedClientAsync_ReturnsLeaseWithDerivedFactoryAndOwnsSentinel()
    {
        var baseline = TestSentinel.AliveCount;

        await using var fixture = new HabilidadWebTestFixture();
        {
            await using var lease = await fixture.CreateAuthenticatedClientAsync(new FakeHabilidadApiClient());

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

    // ── Aislamiento de dispose: la raíz compartida debe sobrevivir ─────

    [Fact]
    public async Task Lease_DisposeAsync_DoesNotDisposeSharedRoot()
    {
        await using var fixture = new HabilidadWebTestFixture();

        // El primer lease debe liberarse ANTES de construir el segundo para
        // verificar que la raíz compartida sobrevive. Se usa un bloque interno
        // anidado en lugar de un dispose manual: el `await using` cierra al
        // final del bloque manteniendo la política "ningún dispose manual".
        {
            await using var firstLease = await fixture.CreateAuthenticatedClientAsync(new FakeHabilidadApiClient());
        }

        // Después de disponer la primera lease, la raíz compartida debe seguir
        // operativa: una segunda lease construida a partir del MISMO fixture
        // debe producir un cliente capaz de resolver rutas web.
        await using var secondLease = await fixture.CreateAuthenticatedClientAsync(new FakeHabilidadApiClient());
        using var response = await secondLease.Client.GetAsync("/auth/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Override observable: el fake configurado llega a DI ───────────

    [Fact]
    public async Task Lease_ConfiguredOverride_IsObservableThroughFactoryServices()
    {
        var fakeHabilidad = FakeHabilidadApiClient.WithHabilidadList(
            WebTestBuilders.BuildHabilidadDto("H-001", "Liderazgo", null, "Conductual"));

        await using var fixture = new HabilidadWebTestFixture();
        await using var lease = await fixture.CreateAuthenticatedClientAsync(fakeHabilidad);

        // La override configurada por el fixture debe quedar registrada en los
        // servicios de la factory derivada del lease: al resolver
        // IHabilidadApiClient recuperamos EXACTAMENTE el fake que pasamos, cuyo
        // estado sembrado es el que verá la página que invoque la API.
        using var scope = lease.Factory.Services.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<IHabilidadApiClient>();

        Assert.Same(fakeHabilidad, resolved);
    }
}
