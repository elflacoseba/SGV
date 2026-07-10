using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

/// <summary>
/// Tests for <see cref="SGV.Api.Controllers.NivelesHabilidadController"/>.
/// Mirrors the pattern of <c>NivelesCargoControllerTests</c>: list +
/// get-by-id + auth (401 without credentials).
/// </summary>
public sealed class NivelesHabilidadControllerTests
{
    private static readonly Guid BasicoId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid AvanzadoId = Guid.Parse("91000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task GetAll_ReturnsOkWithDtos()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<INivelHabilidadServicioConsulta>();
            services.AddSingleton<INivelHabilidadServicioConsulta>(new FakeNivelHabilidadServicio());
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.GetAsync("/api/v1/niveles-habilidad");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await response.Content.ReadFromJsonAsync<List<NivelHabilidadDto>>();
        Assert.NotNull(dtos);
        Assert.Equal(2, dtos!.Count);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOk()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<INivelHabilidadServicioConsulta>();
            services.AddSingleton<INivelHabilidadServicioConsulta>(new FakeNivelHabilidadServicio());
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.GetAsync($"/api/v1/niveles-habilidad/{BasicoId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<NivelHabilidadDto>();
        Assert.NotNull(dto);
        Assert.Equal(BasicoId, dto!.Id);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<INivelHabilidadServicioConsulta>();
            services.AddSingleton<INivelHabilidadServicioConsulta>(new FakeNivelHabilidadServicio());
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.GetAsync($"/api/v1/niveles-habilidad/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutCredentials_ReturnsUnauthorized()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/niveles-habilidad");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithEmptyCatalog_Returns200WithEmptyArray()
    {
        // Spec CRITICAL-05 escenario 2: cuando el catálogo de niveles está
        // vacío, el endpoint MUST responder 200 OK con una colección vacía.
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<INivelHabilidadServicioConsulta>();
            services.AddSingleton<INivelHabilidadServicioConsulta>(new FakeNivelHabilidadServicio(isEmpty: true));
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.GetAsync("/api/v1/niveles-habilidad");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await response.Content.ReadFromJsonAsync<List<NivelHabilidadDto>>();
        Assert.NotNull(dtos);
        Assert.Empty(dtos!);
    }
}