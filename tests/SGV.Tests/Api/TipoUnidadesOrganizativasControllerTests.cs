using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class TipoUnidadesOrganizativasControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public TipoUnidadesOrganizativasControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_Returns200With20SeedDtos()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-unidad-organizativa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<TipoUnidadOrganizativaDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Equal(20, dtos.Count);
        Assert.Contains(dtos, d => d.Codigo == "Institucion");
        Assert.Contains(dtos, d => d.Codigo == "Area");
        Assert.Contains(dtos, d => d.Codigo == "Gerencia");
    }

    [Fact]
    public async Task GetAll_WithoutCredentials_Returns401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/tipos-unidad-organizativa");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutCredentials_Returns401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/tipos-unidad-organizativa/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WhenNoData_Returns200WithEmptyArray()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ITipoUnidadOrganizativaServicioConsulta>();
            services.AddSingleton<ITipoUnidadOrganizativaServicioConsulta>(
                new FakeTipoUnidadOrganizativaServicio(isEmpty: true));
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-unidad-organizativa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<TipoUnidadOrganizativaDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_Returns200WithDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/tipos-unidad-organizativa/{FakeTipoUnidadOrganizativaServicio.DireccionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<TipoUnidadOrganizativaDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakeTipoUnidadOrganizativaServicio.DireccionId, dto.Id);
        Assert.Equal("Dirección", dto.Nombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/tipos-unidad-organizativa/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_InvalidGuid_Returns400()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            "/api/v1/tipos-unidad-organizativa/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dto_Shape_OnlyIdCodigoNombre()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-unidad-organizativa");
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var properties = new HashSet<string>();
            foreach (var prop in element.EnumerateObject())
                properties.Add(prop.Name);

            Assert.Equal(3, properties.Count);
            Assert.Contains("id", properties);
            Assert.Contains("codigo", properties);
            Assert.Contains("nombre", properties);
        }
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.TipoUnidadesOrganizativasController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.True(hasAuthorize, "Controller MUST require authorization");
    }
}
