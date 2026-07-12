using System.Net;
using System.Web;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web;

public sealed partial class UnidadOrganizativaWebTests
{
    [Fact]
    public async Task Get_Organigrama_WhenTreeHasNodes_RendersHierarchyAndUsesTreeEndpoint()
    {
        var facultyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult =
        [
            new UnidadOrganizativaTreeNodeDto(
                facultyId,
                "RECT",
                "Rectorado",
                Guid.NewGuid(),
                "Institución",
                [
                    new UnidadOrganizativaTreeNodeDto(
                        departmentId,
                        "FI",
                        "Facultad de Ingeniería",
                        Guid.NewGuid(),
                        "Facultad",
                        [])
                ])
        ];

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Organigrama", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"orgchart\"", content, StringComparison.OrdinalIgnoreCase);
        // El árbol se hidrata server-side con el JWT bridged (window.__sgvTreeData),
        // evitando el fetch browser-side que rebotaba con 401. Se valida por
        // identificadores ASCII para no depender del encoding de no-ASCII
        // (los nombres con acentos los serializa el JSON pero la aserción
        // debe ser estable independiente del transporte).
        Assert.Contains("window.__sgvTreeData", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(facultyId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(departmentId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"codigo\":\"RECT\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"codigo\":\"FI\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.TreeCalls);
        Assert.Empty(apiClient.QueryCalls);
    }

    [Fact]
    public async Task Get_Organigrama_WhenTreeIsEmpty_ShowsEmptyState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = [];

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No hay unidades organizativas para mostrar en el organigrama", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<table", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.TreeCalls);
    }

    [Fact]
    public async Task Get_Organigrama_WhenTreeFails_ShowsVisibleErrorAndFallbackActions()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeException = new HttpRequestException("tree-boom");

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el organigrama", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/organizacion/unidades-organizativas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.TreeCalls);
        Assert.Empty(apiClient.QueryCalls);
    }
}
