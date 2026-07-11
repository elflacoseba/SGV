using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

public sealed partial class UnidadOrganizativaWebTests
{
    [Fact]
    public async Task Get_Create_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/organizacion/unidades-organizativas/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/organizacion/unidades-organizativas/detalles/" + Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/organizacion/unidades-organizativas/editar/" + Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenAuthenticated_LoadsCatalogs()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(Guid.NewGuid(), "DIR", "Dirección")];
        apiClient.TreeResult = [new UnidadOrganizativaTreeNodeDto(Guid.NewGuid(), "RECT", "Rectorado", Guid.NewGuid(), "Institución", [])];

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/unidades-organizativas/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Crear unidad organizativa", content);
        Assert.Contains("Dirección", content);
        Assert.Contains("Rectorado", content);
        Assert.Contains("name=\"Input.Codigo\"", content);
        Assert.Contains("name=\"Input.Nombre\"", content);
        Assert.Contains("name=\"Input.Descripcion\"", content);
        Assert.Contains("name=\"Input.VigenteDesde\"", content);
        Assert.Contains("name=\"Input.VigenteHasta\"", content);
        Assert.Contains("name=\"Input.TipoUnidadOrganizativaId\"", content);
        Assert.Contains("name=\"Input.UnidadPadreId\"", content);
    }

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetailsWithVisibleConfirmation()
    {
        var newId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        var createdUnit = new UnidadOrganizativaDto(newId, "NEW01", "Nueva Unidad", tipoId, "Dirección", null, null, null, null, null, null);
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Success(createdUnit);
        apiClient.GetByIdResult = createdUnit;
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync("/organizacion/unidades-organizativas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "NEW01",
            ["Input.Nombre"] = "Nueva Unidad",
            ["Input.TipoUnidadOrganizativaId"] = Guid.NewGuid().ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Contains($"/organizacion/unidades-organizativas/detalles/{newId}", postResponse.Headers.Location?.OriginalString);

        var detailsResponse = await client.GetAsync(postResponse.Headers.Location!);
        var detailsContent = HttpUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("se creó correctamente", detailsContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nueva Unidad", detailsContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Create_WhenValidationFails_ReturnsPageWithFieldErrorsAndPreservesCatalogs()
    {
        var tipoId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, "ValidationError", "One or more fields are invalid."),
            new Dictionary<string, string[]> { ["Codigo"] = ["El código ya existe."] });
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];
        apiClient.TreeResult = [new UnidadOrganizativaTreeNodeDto(Guid.NewGuid(), "RECT", "Rectorado", Guid.NewGuid(), "Institución", [])];

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync("/organizacion/unidades-organizativas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "EXIST",
            ["Input.Nombre"] = "Unidad Existente",
            ["Input.TipoUnidadOrganizativaId"] = tipoId.ToString()
        }));

        var content = HttpUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("El código ya existe.", content);
        Assert.Contains("Dirección", content); // catalogs still loaded
        Assert.Contains("Rectorado", content); // tree still loaded
    }

    [Fact]
    public async Task Get_Details_WhenAuthenticated_ShowsUnitWithParent()
    {
        var unitId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", Guid.NewGuid(), "Departamento",
            "Descripción", DateOnly.Parse("2024-01-01"), null, parentId, "RECT", "Rectorado");

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/unidades-organizativas/detalles/{unitId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Departamento Test", content);
        Assert.Contains("DEPT01", content);
        Assert.Contains("RECT", content);
        Assert.Contains("Rectorado", content);
        Assert.Contains("Descripción", content);
        Assert.Contains("01/01/2024", content);
    }

    [Fact]
    public async Task Get_Details_WhenNotFound_ShowsNotAvailableState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = null;

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/unidades-organizativas/detalles/{Guid.NewGuid()}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content);
    }

    [Fact]
    public async Task Get_Details_WhenUnidadDeleted_ShowsRecoverableStateWithReactivateAction()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = null;

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/unidades-organizativas/detalles/{Guid.NewGuid()}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content);
    }

    [Fact]
    public async Task Get_Edit_WhenUnidadDeleted_ShowsRecoverableStateWithReactivateAction()
    {
        var deletedId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = null;

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{deletedId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content);
    }

    [Fact]
    public async Task Post_ReactivateFromDetails_WhenSuccessful_RedirectsToDetails()
    {
        var unitId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(unitId, "R01", "Unidad Reactivada", Guid.NewGuid(), "Dirección", null, null, null, null, null, null));
        apiClient.GetByIdResult = null; // Initially null (deleted)

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/detalles/{unitId}?returnPage=1&returnSearch=test&returnSort=nombre_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/unidades-organizativas/detalles/{unitId}?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["returnPage"] = "1",
            ["returnSearch"] = "test",
            ["returnSort"] = "nombre_asc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/organizacion/unidades-organizativas/detalles/{unitId}", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        apiClient.GetByIdResult = new UnidadOrganizativaDto(unitId, "R01", "Unidad Reactivada", Guid.NewGuid(), "Dirección", null, null, null, null, null, null);

        var detailsResponse = await client.GetAsync(response.Headers.Location!);
        var detailsContent = HttpUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("se reactivó correctamente", detailsContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unidad Reactivada", detailsContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ReactivateFromDetails_WhenConflict_ShowsFeedback()
    {
        var unitId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado",
                "Ya existe una unidad activa con el mismo código."));
        apiClient.GetByIdResult = null;

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/detalles/{unitId}");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/unidades-organizativas/detalles/{unitId}?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        }));

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("código", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content);
    }
}
