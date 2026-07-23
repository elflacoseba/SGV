using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests del módulo web de Habilidades Edit page.
/// </summary>
[Collection("WebIntegration")]
public sealed class HabilidadEditPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public HabilidadEditPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Edit_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenAuthenticated_PrepopulatesForm()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Editar habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"H-001\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"Liderazgo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Desc", content, StringComparison.OrdinalIgnoreCase);

        // Anti-drift: no hay select de nivel. El <select> de CategoriaId es legítimo.
        Assert.DoesNotContain("name=\"Input.NivelId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Nivel<", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenHabilidadNotFound_ShowsRecoverableState()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(); // empty → GetByIdAsync returns null

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/habilidades/editar/{Guid.NewGuid()}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La habilidad solicitada no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Input.Codigo", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditPage_MuestraCodigoEditable()
    {
        // Cambio de contrato: el campo Input.Codigo en edit ya no lleva
        // readonly; el usuario puede editar el código y la unicidad activa se
        // evalúa contra otras Habilidades al guardar. El selector sigue
        // siendo puntual sobre el mismo tag <input>.
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;
        var response = await client.GetAsync($"/organizacion/habilidades/editar/{id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(HabilidadMarkup.HasInputNamed(content, "Input.Codigo"),
            "El campo Input.Codigo debe renderizarse en la página de edición.");
        Assert.False(HabilidadMarkup.InputHasAttribute(content, "Input.Codigo", "readonly"),
            "El campo Input.Codigo NO debe llevar readonly en Edit (ahora es editable).");
        // El resto de los campos editables siguen sin llevar readonly por la
        // misma razón: el form es completamente editable.
        foreach (var other in new[] { "Input.Nombre", "Input.Categoria", "Input.Descripcion" })
        {
            Assert.False(HabilidadMarkup.InputHasAttribute(content, other, "readonly"),
                $"El campo {other} no debe llevar readonly.");
        }
    }

    [Fact]
    public async Task Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation()
    {
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo Senior", "Desc actualizada", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);
        apiClient.UpdateResult = HabilidadCommandResult.Success(dto);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        var formPost = await PostEditAsync(client, token, id, "H-001", "Liderazgo Senior", "Conductual", "Desc actualizada");

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains($"/organizacion/habilidades/detalles/{id}", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.UpdateCalls);
        Assert.Equal("H-001", apiClient.UpdateCalls[0].Request.Codigo);
        Assert.Equal("Liderazgo Senior", apiClient.UpdateCalls[0].Request.Nombre);
    }

    [Fact]
    public async Task Post_Edit_WhenCodigoChanges_RedirectsWithUpdatedCodigo()
    {
        // Cambio de código end-to-end: el form envía un Codigo distinto y el
        // request que llega al backend lo refleja exactamente. El redirect
        // apunta a Details de la misma habilidad (el id no cambia).
        var id = Guid.NewGuid();
        var originalDto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var dtoActualizado = new HabilidadDto(id, "H-002", "Liderazgo Senior", "Desc", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(originalDto);
        apiClient.UpdateResult = HabilidadCommandResult.Success(dtoActualizado);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        var formPost = await PostEditAsync(client, token, id, "H-002", "Liderazgo Senior", "Conductual", "Desc");

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Single(apiClient.UpdateCalls);
        Assert.Equal("H-002", apiClient.UpdateCalls[0].Request.Codigo);
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

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        var formPost = await PostEditAsync(client, token, id, "H-002", "Nuevo nombre", null, null);

        Assert.Equal(HttpStatusCode.OK, formPost.StatusCode);
        var content = HttpUtility.HtmlDecode(await formPost.Content.ReadAsStringAsync());
        Assert.Contains("El código ya está en uso", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Edit_WhenInvalidCodigo_ShowsValidationErrorAndKeepsForm()
    {
        // Cobertura REAL de validación: se postea un Codigo inválido y se
        // verifica que la página corta antes de invocar al cliente API,
        // muestra el error de ModelState sobre Input.Codigo y conserva el
        // resto de los datos del form. Cubre tres escenarios: vacío,
        // whitespace (no es vacío pero tampoco válido) y exactamente 51
        // caracteres (boundary + 1 sobre el máximo).
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        // Tres POST inválidos consecutivos. Cada uno debe ser rechazado por
        // ModelState sin invocar al cliente API.
        var invalidCodigos = new[]
        {
            string.Empty,
            new string(' ', 3),
            new string('X', 51)
        };

        foreach (var codigoInvalido in invalidCodigos)
        {
            var formPost = await PostEditAsync(client, token, id, codigoInvalido, "Liderazgo", "Conductual", "DescripcionX");

            Assert.Equal(HttpStatusCode.OK, formPost.StatusCode);
            var content = HttpUtility.HtmlDecode(await formPost.Content.ReadAsStringAsync());
            Assert.Contains("El código", content, StringComparison.OrdinalIgnoreCase);
            // El form debe conservar el resto de los datos para corregir.
            Assert.Contains("value=\"Liderazgo\"", content, StringComparison.OrdinalIgnoreCase);
        }

        // Validación cliente/servidor corta antes de invocar al cliente API.
        // Ninguno de los 3 POST debe haber llegado al backend.
        Assert.Empty(apiClient.UpdateCalls);
    }

    [Fact]
    public async Task Post_Edit_WhenBackendUnavailable_ShowsRecoverableError()
    {
        // Spec CRITICAL-05 escenario 4: edit backend no disponible durante
        // el guardado MUST mostrar un error visible con acción de reintento
        // y preservar los valores del form.
        var id = Guid.NewGuid();
        var dto = new HabilidadDto(id, "H-001", "Liderazgo", "Desc", null, "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dto);
        apiClient.UpdateException = new HttpRequestException("API caída");

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{id}");

        var formPost = await PostEditAsync(client, token, id, "H-001", "Reintento", null, null);

        Assert.Equal(HttpStatusCode.OK, formPost.StatusCode);
        var content = HttpUtility.HtmlDecode(await formPost.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
        // El valor del Codigo debe preservarse para que el usuario pueda reintentar.
        Assert.Contains("value=\"H-001\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Edit_WhenCodigoReusedFromSoftDeleted_Succeeds()
    {
        // Scenario del delta spec `habilidad-web-crear-editar`:
        // "Reutilizar un Codigo liberado por baja lógica". El backend YA
        // acepta el guardado (cubierto por
        // `ActualizarAsync_CodigoDeEliminada_PermiteReutilizar` en el servicio
        // de aplicación). Este test verifica el camino observable web:
        // la página de Edit postea el Codigo reusado al cliente API y
        // completa el PRG con redirect a Details cuando el backend confirma
        // el guardado.
        var idActiva = Guid.NewGuid();
        var idEliminada = Guid.NewGuid();
        var codigoReusado = "H-LEGACY";

        var dtoActiva = new HabilidadDto(idActiva, "H-OLD", "Liderazgo", "Desc", null, "Conductual");
        var dtoEliminada = new HabilidadDto(idEliminada, codigoReusado, "Trabajo en equipo", "Desc legacy", null, "Conductual");

        var apiClient = FakeHabilidadApiClient.WithHabilidadList(dtoActiva, dtoEliminada);
        // Sembrar la baja lógica de la habilidad previa que tiene el Codigo
        // que se va a reusar.
        await apiClient.DeleteAsync(idEliminada);
        Assert.True(apiClient.IsDeleted(idEliminada),
            "Setup: la habilidad previa debe estar marcada como eliminada en el fake.");

        var dtoActualizado = new HabilidadDto(idActiva, codigoReusado, "Liderazgo", "Desc", null, "Conductual");
        apiClient.UpdateResult = HabilidadCommandResult.Success(dtoActualizado);

        await using var lease = await _fixture.CreateHabilidadLeaseAsync(apiClient);
        var client = lease.Client;
        var token = await GetAntiforgeryTokenAsync(client, $"/organizacion/habilidades/editar/{idActiva}");

        var formPost = await PostEditAsync(client, token, idActiva, codigoReusado, "Liderazgo", "Conductual", "Desc");

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains($"/organizacion/habilidades/detalles/{idActiva}",
            formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(codigoReusado, apiClient.UpdateCalls[0].Request.Codigo);
        Assert.Equal("Liderazgo", apiClient.UpdateCalls[0].Request.Nombre);
        // La baja lógica previa debe preservarse: el guardado de la activa no
        // reactiva la soft-deleted.
        Assert.True(apiClient.IsDeleted(idEliminada),
            "La habilidad previa con baja lógica no debe reactivarse al guardar la habilidad activa.");
    }

    private static async Task<HttpResponseMessage> PostEditAsync(
        HttpClient client,
        string antiforgeryToken,
        Guid id,
        string codigo,
        string nombre,
        string? categoria,
        string? descripcion)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(id.ToString()), "id" },
            { new StringContent(codigo), "Input.Codigo" },
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
        return await WebTestBuilders.ExtractAntiforgeryTokenAsync(response);
    }
}