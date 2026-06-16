using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class TipoUnidadesOrganizativasControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_Returns200With7SeedDtos()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/tipos-unidad-organizativa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<TipoUnidadOrganizativaDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Equal(7, dtos.Count);
        Assert.Contains(dtos, d => d.Codigo == "Institucion");
        Assert.Contains(dtos, d => d.Codigo == "Area");
    }

    [Fact]
    public async Task GetAll_WhenNoData_Returns200WithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ITipoUnidadOrganizativaServicioConsulta>();
            services.AddSingleton<ITipoUnidadOrganizativaServicioConsulta>(
                new FakeTipoUnidadOrganizativaServicio(isEmpty: true));
        });
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/tipos-unidad-organizativa/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_InvalidGuid_Returns400()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/tipos-unidad-organizativa/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dto_Shape_OnlyIdCodigoNombre()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

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
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.TipoUnidadesOrganizativasController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }
}
