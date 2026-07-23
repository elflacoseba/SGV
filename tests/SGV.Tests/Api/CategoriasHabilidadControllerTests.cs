using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Categorias.Consultas;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

/// <summary>
/// Tests de integración del <c>CategoriasHabilidadController</c>
/// (issue migrar-campo-categoria-habilidades-a-tabla).
/// </summary>
[Collection("ApiIntegration")]
public sealed class CategoriasHabilidadControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public CategoriasHabilidadControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_ConAuth_Devuelve4Categorias()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/categorias-habilidad");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<CategoriaHabilidadDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Equal(4, dtos!.Count);
        Assert.Contains(dtos, d => d.Codigo == "Conduccion");
        Assert.Contains(dtos, d => d.Codigo == "Tecnica");
        Assert.Contains(dtos, d => d.Codigo == "Dominio");
        Assert.Contains(dtos, d => d.Codigo == "Academica");
    }

    [Fact]
    public async Task GetAll_SinAuth_401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/categorias-habilidad");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ConduccionExiste_DevuelveConduccionDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/categorias-habilidad/{FakeCategoriaHabilidadServicioConsulta.ConduccionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<CategoriaHabilidadDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakeCategoriaHabilidadServicioConsulta.ConduccionId, dto!.Id);
        Assert.Equal("Conduccion", dto.Codigo);
        Assert.Equal("Conducción", dto.Nombre);
    }

    [Fact]
    public async Task GetById_GuidInexistente_Devuelve404()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var idInexistente = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var response = await client.GetAsync($"/api/v1/categorias-habilidad/{idInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_SinAuth_401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/categorias-habilidad/{FakeCategoriaHabilidadServicioConsulta.ConduccionId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_InvalidGuid_400()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/categorias-habilidad/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Returns405MethodNotAllowed()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.PostAsync("/api/v1/categorias-habilidad", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Put_Returns405MethodNotAllowed()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.PutAsync(
            $"/api/v1/categorias-habilidad/{FakeCategoriaHabilidadServicioConsulta.ConduccionId}", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns405MethodNotAllowed()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/categorias-habilidad/{FakeCategoriaHabilidadServicioConsulta.ConduccionId}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.CategoriasHabilidadController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.True(hasAuthorize, "Controller MUST require authorization");
    }

    [Fact]
    public async Task Dto_Shape_OnlyExpectedProperties()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/categorias-habilidad");
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var properties = new HashSet<string>();
            foreach (var prop in element.EnumerateObject())
            {
                properties.Add(prop.Name);
            }

            Assert.Equal(3, properties.Count);
            Assert.Contains("id", properties);
            Assert.Contains("codigo", properties);
            Assert.Contains("nombre", properties);
        }
    }

    [Fact]
    public async Task GetAll_WhenNoData_Returns200WithEmptyArray()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICategoriaHabilidadServicioConsulta>();
            services.AddSingleton<ICategoriaHabilidadServicioConsulta>(
                new FakeCategoriaHabilidadServicioConsulta(isEmpty: true));
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/categorias-habilidad");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<CategoriaHabilidadDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos!);
    }
}