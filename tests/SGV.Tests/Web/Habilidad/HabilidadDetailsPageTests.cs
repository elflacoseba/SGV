using System.Net;
using System.Net.Http.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests del módulo web de Habilidades Details page.
/// </summary>
public sealed class HabilidadDetailsPageTests : IClassFixture<HabilidadWebTestFixture>
{
    private readonly HabilidadWebTestFixture _fixture;

    public HabilidadDetailsPageTests(HabilidadWebTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/organizacion/habilidades/detalles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAuthenticated_ShowsHabilidadReadOnly()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Descripción completa", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/detalles/{id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Detalle de habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("H-001", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Liderazgo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Descripción completa", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Conductual", content, StringComparison.OrdinalIgnoreCase);
        // El form de edición no debe estar disponible en Details.
        Assert.DoesNotContain("name=\"Input.Codigo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenHabilidadNotFound_ShowsNotAvailableState()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(); // empty → GetByIdAsync returns null

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/detalles/{Guid.NewGuid()}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }
}