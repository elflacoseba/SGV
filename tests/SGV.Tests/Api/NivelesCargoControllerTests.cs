using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class NivelesCargoControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_Returns200With2SeedDtos()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/niveles-cargo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<NivelCargoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Equal(2, dtos.Count);
        Assert.Contains(dtos, d => d.Codigo == "Directivo");
        Assert.Contains(dtos, d => d.Codigo == "Operativo");
    }

    [Fact]
    public async Task GetAll_WhenNoData_Returns200WithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<INivelCargoServicioConsulta>();
            services.AddSingleton<INivelCargoServicioConsulta>(
                new FakeNivelCargoServicioConsulta(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/niveles-cargo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<NivelCargoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_Returns200WithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/niveles-cargo/{FakeNivelCargoServicioConsulta.Nivel1Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<NivelCargoDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakeNivelCargoServicioConsulta.Nivel1Id, dto.Id);
        Assert.Equal("Directivo", dto.Nombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/niveles-cargo/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_InvalidGuid_Returns400()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/niveles-cargo/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Returns405MethodNotAllowed()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/niveles-cargo", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Put_Returns405MethodNotAllowed()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsync(
            $"/api/v1/niveles-cargo/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns405MethodNotAllowed()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/niveles-cargo/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Patch_Returns405MethodNotAllowed()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/niveles-cargo/{Guid.NewGuid()}")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Dto_Shape_OnlyExpectedProperties()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/niveles-cargo");
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var properties = new HashSet<string>();
            foreach (var prop in element.EnumerateObject())
                properties.Add(prop.Name);

            Assert.Equal(5, properties.Count);
            Assert.Contains("id", properties);
            Assert.Contains("codigo", properties);
            Assert.Contains("nombre", properties);
            Assert.Contains("valorNumerico", properties);
            Assert.Contains("orden", properties);
        }
    }

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.NivelesCargoController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }
}
