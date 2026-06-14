using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class SkillsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<HabilidadDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(FakeHabilidadServicio.HabilidadId1, dtos[0].Id);
        Assert.Equal("PROG", dtos[0].Codigo);
    }

    [Fact]
    public async Task GetAll_WhenNoData_ReturnsOkWithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioConsulta>();
            services.AddSingleton<IHabilidadServicioConsulta>(
                new FakeHabilidadServicio(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<HabilidadDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<HabilidadDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakeHabilidadServicio.HabilidadId1, dto.Id);
        Assert.Equal("Programación", dto.Nombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.SkillsController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }
}
