using System.Net;
using System.Web;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web;

public sealed partial class UnidadOrganizativaWebTests
{
    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersShellMenuAndInitialTable()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 24, CreateItem("A01", "Rectorado", "Institución"), CreateItem("B01", "Dirección de Talento", "Dirección")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unidades Organizativas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span class=\"menu-text\">Home</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span class=\"menu-text\">Unidades Organizativas</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Postulantes</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Catálogos</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Reclutamiento</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Página 1 de 3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rectorado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dirección de Talento", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-uo-delete-button", content, StringComparison.OrdinalIgnoreCase);
        var initialQuery = Assert.Single(apiClient.QueryCalls);
        Assert.Null(initialQuery.Search);
        Assert.Null(initialQuery.Sort);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_DefaultsToActivasAndShowsDeletedToggle()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, CreateItem("A01", "Rectorado", "Institución")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=1\">Activas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=1&status=eliminadas\">Eliminadas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aria-label=\"Reactivar", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Null(query.Status);
    }

    [Fact]
    public async Task Get_Index_WhenSearchHasNoResults_ShowsEmptyState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?search=zzz");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron unidades organizativas", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rectorado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("zzz", apiClient.QueryCalls[0].Search);
    }

    [Fact]
    public async Task Get_Index_WhenQueryFails_ShowsVisibleErrorAndKeepsSearchAvailable()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithFailure(new HttpRequestException("boom"));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenDeletedQueryFails_KeepsDeletedSegmentForRetry()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithFailure(new HttpRequestException("boom"));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas&search=dep");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"status\" type=\"hidden\" value=\"eliminadas\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=1&search=dep&status=eliminadas\">Eliminadas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Crear<", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal("eliminadas", query.Status);
    }

    [Fact]
    public async Task Get_Index_WhenChangingPage_ShowsRequestedPageAndCurrentIndicator()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25, CreateItem("C01", "Facultad de Ingeniería", "Facultad"), CreateItem("C02", "Facultad de Ciencias", "Facultad")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?p=2");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Página 2 de 3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Facultad de Ingeniería", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Facultad de Ciencias", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, apiClient.QueryCalls[0].Page);
    }

    [Fact]
    public async Task Get_Index_WhenSortingVisiblePage_ReordersRowsAndKeepsCurrentPage()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25,
                CreateItem("C03", "Beta", "Facultad"),
                CreateItem("C01", "Ágora", "Facultad"),
                CreateItem("C02", "Gamma", "Facultad")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?p=2&sort=nombre_desc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, apiClient.QueryCalls[0].Page);
        Assert.Equal("nombre_desc", apiClient.QueryCalls[0].Sort);

        var gammaIndex = content.IndexOf("Gamma", StringComparison.OrdinalIgnoreCase);
        var betaIndex = content.IndexOf("Beta", StringComparison.OrdinalIgnoreCase);
        var agoraIndex = content.IndexOf("Ágora", StringComparison.OrdinalIgnoreCase);

        Assert.True(gammaIndex >= 0 && betaIndex >= 0 && agoraIndex >= 0, "Expected sorted rows to be rendered.");
        Assert.True(gammaIndex < betaIndex && betaIndex < agoraIndex, "Rows were not rendered in descending name order.");
        Assert.Contains("Página 2 de 3", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_RendersDeleteConfirmationHookWithoutExecutingDelete()
    {
        var item = CreateItem("D01", "Secretaría General", "Secretaría");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 1, item));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-uo-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(item.Id.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.DeleteCalls);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersCreateButtonLink()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, CreateItem("A01", "Rectorado", "Institución")));
        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Crear", content);
        Assert.Contains("href=\"/organizacion/unidades-organizativas/crear", content);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersDetailAndEditPerRow()
    {
        var item = CreateItem("A01", "Rectorado", "Institución");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, item));
        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/organizacion/unidades-organizativas/detalles/{item.Id}", content);
        Assert.Contains($"/organizacion/unidades-organizativas/editar/{item.Id}", content);
    }

    [Fact]
    public async Task Get_Index_WhenNavigatingToDetailOrEdit_PreservesPageSearchSort()
    {
        var item = CreateItem("A01", "Rectorado", "Institución");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25, item));
        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?p=2&search=test&sort=nombre_desc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Detail and Edit links render with the item ID in the URL
        var itemIdStr = item.Id.ToString();
        Assert.Contains($"/organizacion/unidades-organizativas/detalles/{itemIdStr}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/organizacion/unidades-organizativas/editar/{itemIdStr}", content, StringComparison.OrdinalIgnoreCase);

        // Context preservation is visible: search input shows current search term
        Assert.Contains("value=\"test\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenStatusDeleted_RendersDeletedUnitsWithContextualEmptyState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 2,
                CreateItem("DEL01", "Unidad Eliminada A", "Dirección"),
                CreateItem("DEL02", "Unidad Eliminada B", "Secretaría")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unidad Eliminada A", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unidad Eliminada B", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-uo-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("eliminadas", apiClient.QueryCalls[0].Status);
    }

    [Fact]
    public async Task Get_Index_WhenStatusDeletedAndNoResults_ShowsContextualEmptyState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron unidades organizativas eliminadas", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-uo-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("eliminadas", apiClient.QueryCalls[0].Status);
    }

    [Fact]
    public async Task Get_Index_WhenStatusDeleted_ShowsToggleBetweenActivasAndDeleted()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 1, CreateItem("DEL01", "Eliminada", "Dirección")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Activas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Eliminadas", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenStatusDeleted_KeptInPaginationLinks()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25,
                CreateItem("DEL11", "Unidad Eliminada 11", "Dirección"),
                CreateItem("DEL12", "Unidad Eliminada 12", "Secretaría")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas&p=2&search=del");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Página 2 de 3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=1&search=del&status=eliminadas\">Anterior</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=3&search=del&status=eliminadas\">Siguiente</a>", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(2, query.Page);
        Assert.Equal("del", query.Search);
        Assert.Equal("eliminadas", query.Status);
    }

    [Fact]
    public async Task Get_Index_WhenStatusDeleted_KeptInSortLinksAndCurrentPage()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25,
                CreateItem("DEL21", "Unidad Eliminada B", "Dirección"),
                CreateItem("DEL22", "Unidad Eliminada A", "Secretaría")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas&p=2&search=del&sort=nombre_desc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Página 2 de 3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=2&search=del&sort=codigo_asc&status=eliminadas\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=2&search=del&sort=nombre_asc&status=eliminadas\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=2&search=del&sort=tipo_asc&status=eliminadas\"", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(2, query.Page);
        Assert.Equal("nombre_desc", query.Sort);
        Assert.Equal("eliminadas", query.Status);
    }

    [Fact]
    public async Task Get_Index_WhenStatusDeleted_ShowsReactivateButtonPerRow()
    {
        var item = CreateItem("DEL01", "Unidad para Reactivar", "Dirección");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, item));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unidad para Reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-uo-delete-button", content, StringComparison.OrdinalIgnoreCase);
        // The reactivate button should be present per row in this view
        Assert.Contains("Reactivar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_SwitchSegment_ResetsPageToOne()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25,
                CreateItem("DEL11", "Unidad Eliminada 11", "Dirección")));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas?p=2&status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The toggle link to activas must reset to page 1
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=1", content, StringComparison.OrdinalIgnoreCase);
        // The toggle link to eliminadas must also reset to page 1
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=1", content, StringComparison.OrdinalIgnoreCase);
    }
}
