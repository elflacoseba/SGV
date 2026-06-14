using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class PuestosControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/puestos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<PuestoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(FakePuestoServicio.PuestoId1, dtos[0].Id);
        Assert.Equal("GER-001", dtos[0].Codigo);
        Assert.Equal("Gerencia General", dtos[0].UnidadOrganizativaNombre);
        Assert.Equal("Director", dtos[0].CargoNombre);
    }

    [Fact]
    public async Task GetAll_WhenNoData_ReturnsOkWithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioConsulta>();
            services.AddSingleton<IPuestoServicioConsulta>(
                new FakePuestoServicio(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/puestos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<PuestoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<PuestoDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakePuestoServicio.PuestoId1, dto.Id);
        Assert.Equal("Gerente General", dto.Nombre);
        Assert.Equal("Gerencia General", dto.UnidadOrganizativaNombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/puestos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.PuestosController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }
}
