using System.Net;
using System.Net.Http.Json;
using SGV.Aplicacion.Seguridad;
using SGV.Aplicacion.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Api;

public sealed class UsuariosControllerTests
{
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
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.GetAsync("/api/v1/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsuarios_WithoutCredentials_ReturnsUnauthorized()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/usuarios");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithAdminCredentials_ReturnsFixedCatalog()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.AdminHeader;

        var roles = await client.GetFromJsonAsync<IReadOnlyList<string>>("/api/v1/usuarios/roles");

        Assert.Equal(RolesSgv.Todos, roles);
    }
}
