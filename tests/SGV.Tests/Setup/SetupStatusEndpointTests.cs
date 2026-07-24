using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Setup;
using SGV.Tests.Api;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Tests del endpoint <c>GET /api/v1/setup/status</c> (issue #195).
/// Verifica:
/// - Es accesible sin autenticación (issue #195 REQ-SETUP-001).
/// - Devuelve 200 con <see cref="SetupStatusResponse"/> cuando la DB está vacía.
/// - Devuelve 200 con <see cref="SetupStatusResponse"/> cuando hay usuarios.
/// </summary>
[Collection("ApiIntegration")]
public sealed class SetupStatusEndpointTests
{
    private readonly ApiIntegrationFixture _fixture;
    public SetupStatusEndpointTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetStatus_NoAuth_Devuelve200()
    {
        // La fallback policy exige autenticación por default; [AllowAnonymous]
        // en SetupController.GetStatus exime este único endpoint.
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient(); // sin Authorization

        var response = await client.GetAsync("/api/v1/setup/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(status);
    }

    [Fact]
    public async Task GetStatus_FakeDevuelveRequiresSetupTrue_ClienteRecibeTrue()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ISetupServicio>();
            services.AddSingleton<ISetupServicio>(new FakeSetupServicio(
                obtenerEstadoAsync: () => new SetupStatusResponse(RequiresSetup: true)));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/setup/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status!.RequiresSetup);
    }

    [Fact]
    public async Task GetStatus_FakeDevuelveRequiresSetupFalse_ClienteRecibeFalse()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ISetupServicio>();
            services.AddSingleton<ISetupServicio>(new FakeSetupServicio(
                obtenerEstadoAsync: () => new SetupStatusResponse(RequiresSetup: false)));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/setup/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(status);
        Assert.False(status!.RequiresSetup);
    }
}
