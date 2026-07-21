using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class TiposDocumentoControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public TiposDocumentoControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_ConAuth_Devuelve4Tipos()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-documento");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<TipoDocumentoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Equal(4, dtos!.Count);
        Assert.Contains(dtos, d => d.Codigo == "DNI");
        Assert.Contains(dtos, d => d.Codigo == "LE");
        Assert.Contains(dtos, d => d.Codigo == "LC");
        Assert.Contains(dtos, d => d.Codigo == "Pasaporte");
    }

    [Fact]
    public async Task GetAll_SinAuth_401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/tipos-documento");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_DniExiste_DevuelveDniDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/tipos-documento/{FakeTipoDocumentoCatalogoConsulta.DniId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<TipoDocumentoDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakeTipoDocumentoCatalogoConsulta.DniId, dto!.Id);
        Assert.Equal("DNI", dto.Codigo);
        Assert.Equal("Documento Nacional de Identidad", dto.Nombre);
        Assert.Equal(@"^\d{7,8}$", dto.PatronValidacion);
        Assert.Equal(7, dto.LongitudMinima);
        Assert.Equal(8, dto.LongitudMaxima);
    }

    [Fact]
    public async Task GetById_PasaporteExiste_DevuelvePasaporteDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/tipos-documento/{FakeTipoDocumentoCatalogoConsulta.PasaporteId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<TipoDocumentoDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal("Pasaporte", dto!.Codigo);
        Assert.Equal(9, dto.LongitudMinima);
        Assert.Equal(9, dto.LongitudMaxima);
    }

    [Fact]
    public async Task GetById_GuidInexistente_Devuelve404()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var idInexistente = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var response = await client.GetAsync($"/api/v1/tipos-documento/{idInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_SinAuth_401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/tipos-documento/{FakeTipoDocumentoCatalogoConsulta.DniId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_InvalidGuid_400()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-documento/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Returns405MethodNotAllowed()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.PostAsync("/api/v1/tipos-documento", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Put_Returns405MethodNotAllowed()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.PutAsync(
            $"/api/v1/tipos-documento/{FakeTipoDocumentoCatalogoConsulta.DniId}", null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns405MethodNotAllowed()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/tipos-documento/{FakeTipoDocumentoCatalogoConsulta.DniId}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WhenNoData_Returns200WithEmptyArray()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ITipoDocumentoCatalogoConsulta>();
            services.AddSingleton<ITipoDocumentoCatalogoConsulta>(
                new FakeTipoDocumentoCatalogoConsulta(isEmpty: true));
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-documento");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<TipoDocumentoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos!);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.TiposDocumentoController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.True(hasAuthorize, "Controller MUST require authorization");
    }

    [Fact]
    public async Task Dto_Shape_OnlyExpectedProperties()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-documento");
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var properties = new HashSet<string>();
            foreach (var prop in element.EnumerateObject())
            {
                properties.Add(prop.Name);
            }

            Assert.Equal(6, properties.Count);
            Assert.Contains("id", properties);
            Assert.Contains("codigo", properties);
            Assert.Contains("nombre", properties);
            Assert.Contains("patronValidacion", properties);
            Assert.Contains("longitudMinima", properties);
            Assert.Contains("longitudMaxima", properties);
        }
    }

    [Fact]
    public async Task Json_PatronValidacion_EscapeaBackslashSegunJsonSpec()
    {
        // El spec scenario § "Forma del DTO coincide con el seed" requiere que
        // el patron se serialice con 2 backslashes en JSON (default de System.Text.Json)
        // y que el round-trip lo revierta a 1 backslash al deserializar.
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/tipos-documento");
        var json = await response.Content.ReadAsStringAsync();

        // El seed DNI es ^\d{7,8}$ (1 backslash runtime, 2 en JSON wire).
        Assert.Contains(@"^\\d{7,8}$", json, StringComparison.Ordinal);
    }
}
