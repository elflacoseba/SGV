using System.Net;
using System.Web;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests web del módulo Personas para PR 4/4: vista readonly de detalle
/// (<c>Details</c>). Espejo de <c>CargoDetailsPageTests</c>: cubre acceso
/// autenticado, 404 recuperable, preservación de contexto del listado y
/// display readonly.
/// </summary>
[Collection("WebIntegration")]
public sealed class DetailsPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public DetailsPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: GET accesible para cualquier autenticado
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAuthenticatedAsRegularUser_RendersPersonaReadOnly()
    {
        var persona = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", "ana@example.com", null, null, "DNI", "30123456", "+5491112345678", true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/personas/detalle/{persona.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Detalle de persona", content, StringComparison.OrdinalIgnoreCase);

        // Los campos del Persona deben aparecer en el detalle.
        Assert.Contains(persona.Apellidos, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(persona.Nombres, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(persona.Email!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(persona.NumeroDocumento!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(persona.Telefono!, content, StringComparison.OrdinalIgnoreCase);

        // El detalle debe ofrecer "Volver al listado".
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/personas", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 2: GET 404 → recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenPersonaNotFound_ShowsNotAvailableState()
    {
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var missingId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/personas/detalle/{missingId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);

        // El detalle debe ofrecer camino de retorno al listado.
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/personas", content, StringComparison.OrdinalIgnoreCase);

        // El botón Editar NO debe aparecer en estado 404 (sólo en estado OK).
        Assert.DoesNotContain("/personas/editar/", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 3: enlace "Volver" preserva p/search/sort/status
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenListingContextProvided_PreservesItInBackToListLink()
    {
        var persona = BuildPersonaDto("L-001", "Ana", "García", "ana@example.com");
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync(
            $"/personas/detalle/{persona.Id}?p=3&search=garcia&sort=apellidos_desc&returnStatus=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El enlace "Volver al listado" debe preservar p/search/sort/status
        // para que el back-to-list mantenga el contexto del listado filtrado.
        Assert.Contains("/personas?", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=garcia", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=eliminadas", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 4: display readonly (campos no editables)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAuthenticatedAsRegularUser_DoesNotRenderListActionForms()
    {
        // AC: el detalle es readonly — no debe exponer formularios de
        // listado (data-persona-delete-form / data-persona-reactivate-form
        // son exclusivos del Index). El botón Editar sí se renderiza para
        // que el usuario sepa que existe; el gate de admin se ejecuta al
        // hacer click sobre él (GET /personas/editar/{id} redirige a 403
        // si el usuario no es admin — ver CargoEditPageTests equivalente).
        var persona = BuildPersonaDto("L-001", "Ana", "García", "ana@example.com");
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/personas/detalle/{persona.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El detalle no expone formularios de listado (Delete/Reactivate
        // son exclusivos del Index).
        Assert.DoesNotContain("data-persona-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-persona-reactivate-form", content, StringComparison.OrdinalIgnoreCase);

        // Los datos se muestran como <dd> (definición), no como <input>/<select>
        // — el detalle es readonly, no un form editable.
        Assert.DoesNotContain("asp-for=\"Input.Legajo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-for=\"Input.Nombres\"", content, StringComparison.OrdinalIgnoreCase);
    }

    internal static PersonaDto BuildPersonaDto(string legajo, string nombres, string apellidos, string? email)
        => new(Guid.NewGuid(), legajo, nombres, apellidos, email, null, null, null, null, null, true);
}