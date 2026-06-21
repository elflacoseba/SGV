using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Usuarios;
using Xunit;

namespace SGV.Tests.Api;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessToken()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(token!.AccessToken));
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IAuthServicio>();
            services.AddSingleton<IAuthServicio>(FakeAuthServicio.Unauthorized());
        });
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "bad"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
