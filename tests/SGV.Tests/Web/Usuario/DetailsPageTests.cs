using System.Net;
using System.Web;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Web integration tests for the readonly Usuarios detail page.
/// </summary>
[Collection("WebIntegration")]
public sealed class DetailsPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public DetailsPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Details_WhenAuthenticatedAsRegularUser_RendersReadonlyUserData()
    {
        var usuario = BuildUsuario("u-1");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Detalle de usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.UserName, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.Email, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.Nombres!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.Apellidos!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.PersonaId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Administrador", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Consultor", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAdminAndActive_RendersEditAndDeleteActions()
    {
        var usuario = BuildUsuario("u-admin-view");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenUserIsNotFound_ShowsRecoverableState()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/detalle/u-missing");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/seguridad/usuarios/editar/", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenListingContextProvided_PreservesItInBackLink()
    {
        var usuario = BuildUsuario("u-context");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?p=3&search=garcia&sort=apellidos_desc&returnStatus=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/seguridad/usuarios?", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=garcia", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=eliminadas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios/detalle/u-anonymous");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static UsuarioDto BuildUsuario(string id) => new(
        id,
        Guid.NewGuid(),
        "agarcía",
        "ana@example.com",
        new[] { "Administrador", "Consultor" },
        "Ana",
        "García");
}
