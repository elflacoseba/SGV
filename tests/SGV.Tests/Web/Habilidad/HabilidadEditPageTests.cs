using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests del módulo web de Habilidades Edit page.
/// </summary>
public sealed class HabilidadEditPageTests : IClassFixture<HabilidadWebTestFixture>
{
    private readonly HabilidadWebTestFixture _fixture;

    public HabilidadEditPageTests(HabilidadWebTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Edit_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenAuthenticated_PrepopulatesForm()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Editar habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"H-001\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"Liderazgo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"Conductual\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Desc", content, StringComparison.OrdinalIgnoreCase);

        // Anti-drift: no hay select de nivel.
        Assert.DoesNotContain("<select", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Input.NivelId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Nivel<", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenHabilidadNotFound_ShowsRecoverableState()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(); // empty → GetByIdAsync returns null

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{Guid.NewGuid()}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La habilidad solicitada no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Input.Codigo", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo Senior", "Desc actualizada", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);
        apiClient.UpdateResult = HabilidadCommandResult.Success(dto);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        var formPost = await PostEditAsync(client, token, id, "Liderazgo Senior", "Conductual", "Desc actualizada");

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains($"/organizacion/habilidades/detalles/{id}", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.UpdateCalls);
        Assert.Equal("Liderazgo Senior", apiClient.UpdateCalls[0].Request.Nombre);
    }

    [Fact]
    public async Task Post_Edit_WhenConflictOnCodigo_ReturnsFieldError()
    {
        var id = Guid.NewGuid();
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "El código ya está en uso por otra habilidad activa." }
        };
        apiClient.UpdateResult = HabilidadCommandResult.Failure(
            new HabilidadError(HabilidadErrorType.Conflict, "CodigoDuplicado", "El código ya está en uso por otra habilidad activa."),
            fieldErrors);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        var formPost = await PostEditAsync(client, token, id, "Nuevo nombre", null, null);

        Assert.Equal(HttpStatusCode.OK, formPost.StatusCode);
        var content = HttpUtility.HtmlDecode(await formPost.Content.ReadAsStringAsync());
        Assert.Contains("El código ya está en uso", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditPage_MuestraCodigoComoReadonly_O_Disabled()
    {
        // El input de Input.Codigo en edit debe llevar readonly (la regla
        // de inmutabilidad del dominio Codigo se respeta en UI). El selector
        // es puntual: la marca `readonly` debe estar en el MISMO tag <input>
        // que el name del campo, no en cualquier parte del HTML.
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var response = await client.GetAsync($"/organizacion/habilidades/editar/{id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(HabilidadWebTestFixture.HasInputNamed(content, "Input.Codigo"),
            "El campo Input.Codigo debe renderizarse en la página de edición.");
        Assert.True(HabilidadWebTestFixture.InputHasAttribute(content, "Input.Codigo", "readonly"),
            "El campo Input.Codigo debe llevar el atributo `readonly` en Edit (regla de inmutabilidad).");
        // El resto de los campos editables NO deben llevar readonly.
        foreach (var other in new[] { "Input.Nombre", "Input.Categoria", "Input.Descripcion" })
        {
            Assert.False(HabilidadWebTestFixture.InputHasAttribute(content, other, "readonly"),
                $"El campo {other} no debe llevar readonly.");
        }
    }

    [Fact]
    public async Task Post_Edit_WhenBackendUnavailable_ShowsRecoverableError()
    {
        // Spec CRITICAL-05 escenario 4: edit backend no disponible durante
        // el guardado MUST mostrar un error visible con acción de reintento
        // y preservar los valores del form.
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);
        apiClient.UpdateException = new HttpRequestException("API caída");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        var formPost = await PostEditAsync(client, token, id, "Reintento", null, null);

        Assert.Equal(HttpStatusCode.OK, formPost.StatusCode);
        var content = HttpUtility.HtmlDecode(await formPost.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
        // El valor del Codigo debe preservarse para que el usuario pueda reintentar.
        Assert.Contains("value=\"H-001\"", content, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> PostEditAsync(
        HttpClient client,
        string antiforgeryToken,
        Guid id,
        string nombre,
        string? categoria,
        string? descripcion)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(id.ToString()), "id" },
            { new StringContent("H-001"), "Input.Codigo" },
            { new StringContent(nombre), "Input.Nombre" },
            { new StringContent(categoria ?? string.Empty), "Input.Categoria" },
            { new StringContent(descripcion ?? string.Empty), "Input.Descripcion" },
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" }
        };
        return await client.PostAsync($"/organizacion/habilidades/editar/{id}", form);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await HabilidadWebTestFixture.ExtractAntiforgeryTokenAsync(response);
    }
}