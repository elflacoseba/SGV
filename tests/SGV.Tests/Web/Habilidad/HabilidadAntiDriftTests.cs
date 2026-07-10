using System.Net;
using System.Net.Http.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Anti-drift centralizado: blindaje explícito contra reintroducción del
/// dropdown de nivel por copia del patrón Cargos. Verifica para los
/// formularios de Create, Edit y el partial _Form que NO existe ningún
/// <c>&lt;select&gt;</c> cuyo atributo contenga "Nivel", NO existe texto
/// "Nivel" en el form y NO existe input <c>name="Input.NivelId"</c>.
/// </summary>
public sealed class HabilidadAntiDriftTests : IClassFixture<HabilidadWebTestFixture>
{
    private readonly HabilidadWebTestFixture _fixture;

    public HabilidadAntiDriftTests(HabilidadWebTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreatePage_NoExponeSelectDeNivel()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoNivelForm(content);
    }

    [Fact]
    public async Task EditPage_NoExponeSelectDeNivel()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoNivelForm(content);
    }

    [Fact]
    public async Task CreatePage_PartialForm_NoExponeNivelEnMarkup()
    {
        // El partial _Form.cshtml se renderiza dentro del form de Create.
        // Verificamos explícitamente que el label "Nivel" no aparece.
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form tiene un input con name="Input.Codigo" pero NO hay <select name="Input.NivelId" ...>.
        Assert.DoesNotContain("name=\"Input.NivelId\"", content, StringComparison.OrdinalIgnoreCase);
        // Y tampoco hay un label "Nivel" asociado al form.
        Assert.DoesNotContain(">Nivel</label>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditPage_PartialForm_NoExponeNivelEnMarkup()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);
        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.DoesNotContain("name=\"Input.NivelId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Nivel</label>", content, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoNivelForm(string content)
    {
        // Anti-drift: el catálogo maestro de Habilidad NO debe capturar un nivel.
        Assert.DoesNotContain("<select", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Input.NivelId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"nivelId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"nivel\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Nivel<", content, StringComparison.OrdinalIgnoreCase);
    }
}