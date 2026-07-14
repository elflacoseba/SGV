using System.Net;
using System.Net.Http.Json;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class UsuariosControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public UsuariosControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    [Fact]
    public void FakeAuth_ExposesUserHeaderForAuthenticatedNonAdmin()
    {
        var header = FakeAuthenticationDefaults.UserHeader;

        Assert.Equal(FakeAuthenticationDefaults.Scheme, header.Scheme);
        Assert.Equal("user", header.Parameter);
    }

    [Fact]
    public async Task GetUsuarios_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.GetAsync("/api/v1/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsuarios_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/usuarios");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithAdminCredentials_ReturnsFixedCatalog()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.AdminHeader;

        var roles = await client.GetFromJsonAsync<IReadOnlyList<string>>("/api/v1/usuarios/roles");

        Assert.Equal(RolesSgv.Todos, roles);
    }
}
