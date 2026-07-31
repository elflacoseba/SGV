using System.Net;
using System.Web;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Puesto;
using Xunit;

namespace SGV.Tests.Web.Vacantes;

[Collection("WebIntegration")]
public sealed class VacantesDetailsAndSidenavTests
{
    private readonly WebIntegrationFixture _fixture;

    public VacantesDetailsAndSidenavTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Details_RendersChronologicalHistory()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeVacanteApiClient
        {
            ObtenerPorIdResult = FakeVacanteApiClient.BuildDetail(
                id: id,
                historial:
                [
                    new("En selección", "Cubierta", new DateTime(2026, 2, 10, 10, 0, 0), "user-2", "Cerrada"),
                    new(null, "En selección", new DateTime(2026, 1, 20, 10, 0, 0), "user-1", "Inicio")
                ])
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/vacantes/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Historial de estados", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Inicio", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cerrada", content, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            content.IndexOf("Inicio", StringComparison.OrdinalIgnoreCase)
            < content.IndexOf("Cerrada", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sidenav_WhenAuthenticatedNonMutator_RendersListadoButNotNueva()
    {
        await using var lease = await _fixture.CreatePuestoLeaseAsync(
            new FakePuestosApiClient(), adminRole: false);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Vacantes", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/vacantes\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/organizacion/vacantes/crear\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sidenav_WhenAdministrator_RendersListadoAndNueva()
    {
        await using var lease = await _fixture.CreatePuestoLeaseAsync(
            new FakePuestosApiClient(), adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/organizacion/vacantes\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/vacantes/crear\"", content, StringComparison.OrdinalIgnoreCase);
    }
}
