using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Contracts.Seguridad.Usuarios;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class AuthControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public AuthControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessToken()
    {
        var factory = _fixture.RootFactory;
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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IAuthServicio>();
            services.AddSingleton<IAuthServicio>(FakeAuthServicio.Unauthorized());
        });
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "bad"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AnonymousHeaderless_Returns200()
    {
        // La fallback policy global de Program.cs exige autenticación por default,
        // pero [AllowAnonymous] en Login exime a este único endpoint del API.
        // Esta aserción demuestra que el cliente NO envía Authorization, y el endpoint responde 200.
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(token!.AccessToken));
    }
}
