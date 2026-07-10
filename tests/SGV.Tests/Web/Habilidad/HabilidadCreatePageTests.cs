using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests del módulo web de Habilidades Create page.
/// </summary>
public sealed class HabilidadCreatePageTests : IClassFixture<HabilidadWebTestFixture>
{
    private readonly HabilidadWebTestFixture _fixture;

    public HabilidadCreatePageTests(HabilidadWebTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Create_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/organizacion/habilidades/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenAuthenticated_RendersEmptyForm()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nueva habilidad", content, StringComparison.OrdinalIgnoreCase);
        // Los 4 campos del dominio están presentes.
        Assert.Contains("name=\"Input.Codigo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Nombre\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Categoria\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Descripcion\"", content, StringComparison.OrdinalIgnoreCase);

        // Anti-drift: NO hay ningún <select> relacionado con nivel.
        Assert.DoesNotContain("<select", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Input.NivelId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Nivel<", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenAuthenticated_CodigoEsEditable()
    {
        // En Create, el campo Input.Codigo NO debe ser readonly: el usuario
        // debe poder ingresar el código de la nueva habilidad. El antiguo
        // patrón `readonly="@nullable"` podía dejar el atributo como
        // `readonly=""` en Create (boolean attribute HTML5). El render actual
        // separa Edit/Create y este test blinda explícitamente el camino de
        // alta.
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(HabilidadWebTestFixture.HasInputNamed(content, "Input.Codigo"));
        Assert.False(HabilidadWebTestFixture.InputHasAttribute(content, "Input.Codigo", "readonly"),
            "El campo Input.Codigo en Create no debe llevar readonly (debe ser editable).");
    }

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation()
    {
        var createdId = Guid.NewGuid();
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.CreateResult = HabilidadCommandResult.Success(
            new HabilidadDto(createdId, "NVO", "Nueva Habilidad", "Desc", "Técnica"));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, "/organizacion/habilidades/crear");

        var formPost = await PostCreateAsync(client, token, "NVO", "Nueva Habilidad", "Técnica", "Desc");

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains($"/organizacion/habilidades/detalles/{createdId}", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.CreateCalls);
        Assert.Equal("NVO", apiClient.CreateCalls[0].Codigo);
    }

    [Fact]
    public async Task Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "Ya existe una habilidad activa con ese código." }
        };
        apiClient.CreateResult = HabilidadCommandResult.Failure(
            new HabilidadError(HabilidadErrorType.Conflict, "CodigoDuplicado", "Ya existe una habilidad activa con ese código."),
            fieldErrors);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, "/organizacion/habilidades/crear");

        var formPost = await PostCreateAsync(client, token, "PROG", "Duplicado", null, null);

        Assert.Equal(HttpStatusCode.OK, formPost.StatusCode);
        var content = HttpUtility.HtmlDecode(await formPost.Content.ReadAsStringAsync());
        Assert.Contains("Ya existe una habilidad activa con ese código", content, StringComparison.OrdinalIgnoreCase);
        // El valor del Codigo debe preservarse en el re-render del form.
        Assert.Contains("value=\"PROG\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Create_WhenBackendUnavailable_ShowsRecoverableError()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.CreateException = new HttpRequestException("API caída");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, "/organizacion/habilidades/crear");

        var formPost = await PostCreateAsync(client, token, "RST", "Reintento", null, null);

        Assert.Equal(HttpStatusCode.OK, formPost.StatusCode);
        var content = HttpUtility.HtmlDecode(await formPost.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
        // El valor del Codigo debe preservarse para que el usuario pueda reintentar.
        Assert.Contains("value=\"RST\"", content, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        string antiforgeryToken,
        string codigo,
        string nombre,
        string? categoria,
        string? descripcion)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(codigo), "Input.Codigo" },
            { new StringContent(nombre), "Input.Nombre" },
            { new StringContent(categoria ?? string.Empty), "Input.Categoria" },
            { new StringContent(descripcion ?? string.Empty), "Input.Descripcion" },
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" }
        };
        return await client.PostAsync("/organizacion/habilidades/crear", form);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await HabilidadWebTestFixture.ExtractAntiforgeryTokenAsync(response);
    }
}