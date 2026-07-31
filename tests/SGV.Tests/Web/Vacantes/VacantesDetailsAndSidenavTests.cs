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
    public async Task Get_Details_WhenNoHistory_ShowsEmptyState()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeVacanteApiClient
        {
            ObtenerPorIdResult = FakeVacanteApiClient.BuildDetail(id: id, historial: [])
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/organizacion/vacantes/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Historial de estados", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No hay historial previo.", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(id, Assert.Single(apiClient.ObtenerPorIdCalls));
    }

    [Fact]
    public async Task Get_Details_WhenVacanteDoesNotExist_ShowsRecoverableStateWithReturnLink()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeVacanteApiClient { ObtenerPorIdResult = null };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/organizacion/vacantes/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La vacante solicitada no está disponible.", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/vacantes", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(id, Assert.Single(apiClient.ObtenerPorIdCalls));
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

    [Fact]
    public async Task Get_Index_MarksVacantesSidenavGroupActive()
    {
        await using var lease = await _fixture.CreateVacanteLeaseAsync(new FakeVacanteApiClient());

        var response = await lease.Client.GetAsync("/organizacion/vacantes");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(SidenavGroupHasClass(content, "vacantes", "active"));
    }

    private static bool SidenavGroupHasClass(string content, string ariaControls, string expectedClass)
    {
        var controlsToken = $"aria-controls=\"{ariaControls}\"";
        var controlsIndex = content.IndexOf(controlsToken, StringComparison.OrdinalIgnoreCase);
        if (controlsIndex < 0)
        {
            return false;
        }

        var anchorStart = content.LastIndexOf("<a ", controlsIndex, StringComparison.OrdinalIgnoreCase);
        var anchorEnd = content.IndexOf('>', controlsIndex);
        if (anchorStart < 0 || anchorEnd < 0)
        {
            return false;
        }

        var anchor = content[anchorStart..(anchorEnd + 1)];
        const string classToken = "class=\"";
        var classStart = anchor.IndexOf(classToken, StringComparison.OrdinalIgnoreCase);
        if (classStart < 0)
        {
            return false;
        }

        classStart += classToken.Length;
        var classEnd = anchor.IndexOf('"', classStart);
        if (classEnd < 0)
        {
            return false;
        }

        return anchor[classStart..classEnd]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(expectedClass, StringComparer.OrdinalIgnoreCase);
    }
}
