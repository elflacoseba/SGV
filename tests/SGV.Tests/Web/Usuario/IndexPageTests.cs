using System.Net;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Web integration tests for the segmented Usuarios index and its lifecycle
/// handlers. The tests exercise observable Razor Page behavior through the
/// authenticated shell and the in-memory typed-client fake.
/// </summary>
[Collection("WebIntegration")]
public sealed class IndexPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public IndexPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActiveUsersAndAdminActions()
    {
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var second = BuildUsuario("u-2", "jperez", "Juan", "Pérez", "juan@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first, second);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de usuarios activos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.UserName, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Email, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Nombres!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Apellidos!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Administrador", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/seguridad/usuarios/detalle/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/seguridad/usuarios/editar/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Crear usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(apiClient.QueryCalls);
    }

    [Fact]
    public async Task Get_Index_WhenTogglingSegment_PreservesSearchAndSortAndResetsPage()
    {
        var deleted = BuildUsuario("u-deleted", "deleted", "Elena", "Minada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(deleted);
        await apiClient.DesactivarAsync(deleted.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=eliminadas&search=min&sort=apellidos_desc&p=3");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de usuarios eliminados", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=min", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=1", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(3, query.Page);
        Assert.Equal("min", query.Search);
        Assert.Equal("apellidos_desc", query.Sort);
        Assert.Equal(UsuarioSegmentoListado.Eliminadas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WhenQueryStringHasSearchSortAndPage_PassesThemToQueryAsync()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        await lease.Client.GetAsync("/seguridad/usuarios?status=activas&search=garcia&sort=nombres_asc&p=2");

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(2, query.Page);
        Assert.Equal("garcia", query.Search);
        Assert.Equal("nombres_asc", query.Sort);
        Assert.Equal(UsuarioSegmentoListado.Activas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticatedWithoutAdminRole_HidesAdminActions()
    {
        var usuario = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/seguridad/usuarios/detalle/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenSegmentIsDeleted_ExposesOnlyAdminReactivateAction()
    {
        var usuario = BuildUsuario("u-deleted", "deleted", "Elena", "Minada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        await apiClient.DesactivarAsync(usuario.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/detalle/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear usuario", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenDeletedSegmentAndNoAdmin_HidesReactivateAction()
    {
        var usuario = BuildUsuario("u-deleted", "deleted", "Elena", "Minada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        await apiClient.DesactivarAsync(usuario.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-usuario-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsToActiveSegmentWithContextAndFeedback()
    {
        var toDelete = BuildUsuario("u-delete", "adelete", "Ana", "Delete", "delete@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(toDelete);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=activas&p=2&search=delete&sort=username_desc");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios?handler=Delete",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = toDelete.Id,
                ["page"] = "2",
                ["search"] = "delete",
                ["sort"] = "username_desc",
                ["status"] = "activas"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(toDelete.Id, Assert.Single(apiClient.DeleteCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("status=activas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=delete", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=username_desc", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deletedId=u-delete", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("El usuario se eliminó correctamente", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("formaction=\"?handler=Reactivate\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{toDelete.Id}\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("Reactivate")]
    public async Task Post_LifecycleHandler_WhenUserIsNotAdmin_RedirectsToAccessDeniedWithoutCallingApi(string handler)
    {
        var usuario = BuildUsuario("u-delete", "adelete", "Ana", "Delete", "delete@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios?handler={handler}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = usuario.Id,
                ["page"] = "1"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.DeleteCalls);
        Assert.Empty(apiClient.ReactivarCalls);
    }

    [Fact]
    public async Task Post_Delete_WhenApiRejectsAutoBaja_ShowsActionableFeedback()
    {
        var usuario = BuildUsuario("u-self", "self", "Self", "User", "self@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.DesactivarResult = UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.Validation,
            "AutoBaja",
            "No se puede dar de baja el usuario actual.",
            403,
            ErrorCategoria.Forbidden));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Delete", usuario.Id);
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AutoBaja", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No se puede dar de baja", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenApiReturnsConflict_ShowsConflictFeedback()
    {
        var usuario = BuildUsuario("u-conflict", "conflict", "Conflict", "User", "conflict@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.DesactivarResult = UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.Conflict,
            "Dependencias",
            "La cuenta tiene dependencias activas.",
            409,
            ErrorCategoria.Conflict));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Delete", usuario.Id);
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("Dependencias", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dependencias activas", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Reactivate_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext()
    {
        var usuario = BuildUsuario("u-reactivate", "reactivate", "React", "ivate", "reactivate@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        await apiClient.DesactivarAsync(usuario.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=eliminadas&p=3&search=react&sort=nombres_asc");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios?handler=Reactivate",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = usuario.Id,
                ["page"] = "3",
                ["search"] = "react",
                ["sort"] = "nombres_asc",
                ["status"] = "eliminadas"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(usuario.Id, Assert.Single(apiClient.ReactivarCalls));
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("status=activas", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=3", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=react", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombres_asc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("El usuario se reactivó correctamente", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Reactivate_WhenPersonaIsInactive_StaysInDeletedSegmentWithFeedback()
    {
        var usuario = BuildUsuario("u-inactive-persona", "inactive", "Inactive", "Persona", "inactive@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        await apiClient.DesactivarAsync(usuario.Id);
        apiClient.ReactivarResult = UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.Conflict,
            "PersonaInactiva",
            "La persona vinculada está inactiva.",
            409,
            ErrorCategoria.Conflict));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios?status=eliminadas&search=inactive");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Reactivate", usuario.Id, "eliminadas", "inactive");
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("PersonaInactiva", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("persona vinculada está inactiva", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.UserName, content, StringComparison.OrdinalIgnoreCase);
        Assert.True(apiClient.IsDeleted(usuario.Id));
    }

    [Fact]
    public async Task Get_Index_WhenPageAndStatusAreInvalid_NormalizesToActivePageOne()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        await lease.Client.GetAsync("/seguridad/usuarios?status=archivo&p=0");

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(1, query.Page);
        Assert.Equal(UsuarioSegmentoListado.Activas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WhenQueryFailsWithTransportException_ShowsRecoverableError()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();
        apiClient.QueryException = new HttpRequestException("upstream unavailable");

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado de usuarios", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> PostHandlerAsync(
        WebClientLease lease,
        string token,
        string handler,
        string id,
        string status = "activas",
        string? search = null)
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = id,
            ["page"] = "1",
            ["status"] = status
        };

        if (search is not null)
        {
            values["search"] = search;
        }

        return await lease.Client.PostAsync(
            $"/seguridad/usuarios?handler={handler}",
            new FormUrlEncodedContent(values));
    }

    private static UsuarioDto BuildUsuario(
        string id,
        string userName,
        string nombres,
        string apellidos,
        string email,
        params string[] roles)
        => new(id, Guid.NewGuid(), userName, email, roles, nombres, apellidos);
}
