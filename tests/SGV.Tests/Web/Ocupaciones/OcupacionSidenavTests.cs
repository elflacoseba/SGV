using System.Net;
using System.Web;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Puesto;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Smoke tests de presencia de la entrada Ocupaciones en el sidenav.
/// Issue #208 / Slice 2 / REQ-OCC-LST-005: el grupo padre OCUPACIONES
/// se muestra a todo autenticado; el subítem "Nueva" sólo para
/// Administradores. Los destinos "/organizacion/ocupaciones" y
/// "/organizacion/ocupaciones/crear" resuelven a partir de Slice 2
/// (Index) y Slice 3a (Create) respectivamente.
/// </summary>
[Collection("WebIntegration")]
public sealed class OcupacionSidenavTests
{
    private readonly WebIntegrationFixture _fixture;

    public OcupacionSidenavTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Sidenav_WhenAuthenticatedNonAdmin_RendersListadoButNotNueva()
    {
        // Renderiza una página autenticada cualquiera (no requiere Index
        // para existir; sólo necesita el sidenav). Usamos /organizacion/puestos
        // como host neutral con Puesto ya operativo.
        await using var lease = await _fixture.CreatePuestoLeaseAsync(
            new FakePuestosApiClient(), adminRole: false);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ocupaciones", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/ocupaciones\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/organizacion/ocupaciones/crear\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sidenav_WhenAdmin_RendersListadoAndNueva()
    {
        await using var lease = await _fixture.CreatePuestoLeaseAsync(
            new FakePuestosApiClient(), adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ocupaciones", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/ocupaciones\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/ocupaciones/crear\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sidenav_WhenOcupacionesIndexActive_MarksOcupacionesGroupActive()
    {
        // Renderiza una página autenticada con un Fake de Ocupaciones inyectado.
        // El Index debe renderizar el sidenav y marcar la ruta activa.
        await using var lease = await _fixture.CreateOcupacionLeaseAsync(
            new FakeOcupacionApiClient(), adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El helper RenderOcupacionesGroupActive evalúa StartsWithSegments
        // de /organizacion/ocupaciones; debe contener la clase active en el
        // toggle y en el subítem Listado.
        Assert.Contains("class=\"side-nav-link side-nav-link-toggle active\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/ocupaciones\"", content, StringComparison.OrdinalIgnoreCase);
    }
}