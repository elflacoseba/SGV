using System.Net;
using System.Web;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

public sealed partial class UnidadOrganizativaWebTests
{
    [Fact]
    public async Task DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecuteDeleteConfirmationScriptAsync(false);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.True(result.ShowCancelButton);
        Assert.Equal("Cancelar", result.CancelButtonText);
    }

    [Fact]
    public async Task DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce()
    {
        var result = await ExecuteDeleteConfirmationScriptAsync(true);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Sí, eliminar", result.ConfirmButtonText);
    }

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndRefreshRemovesRow()
    {
        var itemToDelete = CreateItem("E01", "Dirección Académica", "Dirección");
        var remainingItem = CreateItem("E02", "Dirección Financiera", "Dirección");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 11, itemToDelete),
            CreatePage(2, 10, 10),
            CreatePage(1, 10, 10, remainingItem));
        apiClient.DeleteResult = new UnidadOrganizativaDeleteResult(true, HttpStatusCode.NoContent, null, null);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?p=2&search=dir&sort=nombre_desc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = itemToDelete.Id.ToString(),
            ["page"] = "2",
            ["search"] = "dir",
            ["sort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(itemToDelete.Id, Assert.Single(apiClient.DeleteCalls));
        Assert.StartsWith("/organizacion/unidades-organizativas?p=1&search=dir&sort=nombre_desc", response.Headers.Location?.OriginalString);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("La unidad organizativa se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dirección Académica", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dirección Financiera", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible()
    {
        var item = CreateItem("F01", "Departamento Legal", "Departamento");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, item),
            CreatePage(1, 10, 1, item));
        apiClient.DeleteResult = new UnidadOrganizativaDeleteResult(false, HttpStatusCode.Conflict, "unidad-organizativa-en-uso", "La unidad organizativa tiene dependencias activas.");

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?search=dep&sort=nombre_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = item.Id.ToString(),
            ["page"] = "1",
            ["search"] = "dep",
            ["sort"] = "nombre_asc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/organizacion/unidades-organizativas?p=1&search=dep&sort=nombre_asc", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo eliminar la unidad organizativa", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("La unidad organizativa tiene dependencias activas.", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Departamento Legal", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ReactivateFromDeletedList_WhenSuccessful_RedirectsToActivasWithConfirmation()
    {
        var reactivatedId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, CreateItem("DEL01", "Unidad a Reactivar", "Dirección")));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(reactivatedId, "R01", "Unidad Reactivada", Guid.NewGuid(), "Dirección", null, null, null, null, null, null));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas&p=1&search=test&sort=nombre_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = reactivatedId.ToString(),
            ["page"] = "1",
            ["search"] = "test",
            ["sort"] = "nombre_asc",
            ["status"] = "eliminadas"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // After success, redirect to activas (no status param = default activas)
        var location = response.Headers.Location?.OriginalString;
        Assert.StartsWith("/organizacion/unidades-organizativas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=1", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=test", location, StringComparison.OrdinalIgnoreCase);
        // Status should be activas (not eliminated anymore)
        Assert.DoesNotContain("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se reactivó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ReactivateFromDeletedList_WhenConflict_StaysInDeletedWithError()
    {
        var conflictId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, CreateItem("DEL02", "Unidad en Conflicto", "Dirección")));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado",
                "Ya existe una unidad activa con el mismo código."));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?status=eliminadas&p=1&search=test&sort=nombre_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = conflictId.ToString(),
            ["page"] = "1",
            ["search"] = "test",
            ["sort"] = "nombre_asc",
            ["status"] = "eliminadas"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // After conflict, stay in deleted view
        var location = response.Headers.Location?.OriginalString;
        Assert.StartsWith("/organizacion/unidades-organizativas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo reactivar", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("código", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenSuccessful_ShowsReactivationBanner()
    {
        var itemToDelete = CreateItem("R01", "Unidad Reactivable", "Dirección");
        var remainingItem = CreateItem("R02", "Otra Unidad", "Dirección");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 11, itemToDelete),
            CreatePage(2, 10, 10),
            CreatePage(1, 10, 10, remainingItem));
        apiClient.DeleteResult = new UnidadOrganizativaDeleteResult(true, HttpStatusCode.NoContent, null, null);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?p=2&search=dir&sort=nombre_desc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = itemToDelete.Id.ToString(),
            ["page"] = "2",
            ["search"] = "dir",
            ["sort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/organizacion/unidades-organizativas?p=1&search=dir&sort=nombre_desc", response.Headers.Location?.OriginalString);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("La unidad organizativa se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reactivar", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ReactivateFromIndex_WhenSuccessful_RedirectsPreservingContext()
    {
        var reactivatedId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25, CreateItem("S01", "Unidad Reactivada", "Dirección")));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(reactivatedId, "S01", "Unidad Reactivada", Guid.NewGuid(), "Dirección", null, null, null, null, null, null));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?p=2&search=test&sort=nombre_desc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = reactivatedId.ToString(),
            ["page"] = "2",
            ["search"] = "test",
            ["sort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/organizacion/unidades-organizativas?p=2&search=test&sort=nombre_desc", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se reactivó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ReactivateFromIndex_WhenConflict_ShowsFeedbackAndKeepsContext()
    {
        var conflictId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, CreateItem("T01", "Unidad en Conflicto", "Dirección")));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado",
                "Ya existe una unidad activa con el mismo código."));

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?p=1&search=conflict&sort=nombre_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = conflictId.ToString(),
            ["page"] = "1",
            ["search"] = "conflict",
            ["sort"] = "nombre_asc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/organizacion/unidades-organizativas?p=1&search=conflict&sort=nombre_asc", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo reactivar", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("código", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }
}
