using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests web del módulo Personas para PR 4/4: formulario de edición
/// (<c>Edit</c>). Espejo de <c>CargoEditPageTests</c>: cubre autorización,
/// carga inicial vía <see cref="IPersonaApiClient.GetByIdAsync"/>, éxito
/// (PRG al propio edit con feedback), 400 (FieldErrors), 409 (unicidad) y
/// fallos de transporte.
/// </summary>
[Collection("WebIntegration")]
public sealed class EditPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public EditPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: GET requiere Administrador
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        var id = Guid.NewGuid();
        await using var lease = await _fixture.CreatePersonaLeaseAsync(new FakePersonaApiClient());

        var response = await lease.Client.GetAsync($"/personas/editar/{id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 2: GET prellena vía GetByIdAsync; 404 → recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenPersonaExists_PrefillsFormWithCurrentValues()
    {
        var persona = new PersonaDto(
            Guid.NewGuid(), "L-001", "Ana", "García", "ana@example.com",
            "DNI", "30123456", "+5491112345678", true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/personas/editar/{persona.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Editar persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-001", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ana@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30123456", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenPersonaNotFound_ShowsRecoverableState()
    {
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var missingId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/personas/editar/{missingId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);

        // El formulario no debe renderizarse en estado recuperable.
        Assert.DoesNotContain($"name=\"{PersonaFormKeys.LegajoKey}\"", content, StringComparison.OrdinalIgnoreCase);

        // El enlace "Volver al listado" debe estar presente.
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 3: POST 200 → PRG al propio edit con feedback
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenSuccessful_RedirectsToEditWithSuccessFeedback()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakePersonaApiClient
        {
            UpdateResult = PersonaCommandResult.Success(
                new PersonaDto(personaId, "L-001", "Ana Editada", "García", null, null, null, null, true))
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/editar/{personaId}?p=2&search=ana&sort=apellidos_desc");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync($"/personas/editar/{personaId}?p=2&search=ana&sort=apellidos_desc", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-001",
            ["Input.Nombres"] = "Ana Editada",
            ["Input.Apellidos"] = "García"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/personas/editar/{personaId}", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=2", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=ana", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se actualizó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 4: POST 400 → FieldErrors preservando input
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakePersonaApiClient
        {
            UpdateResult = PersonaCommandResult.Failure(
                new PersonaError(PersonaErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["legajo"] = new[] { "El legajo es obligatorio." },
                    ["apellidos"] = new[] { "Los apellidos son obligatorios." }
                })
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/editar/{personaId}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync($"/personas/editar/{personaId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = string.Empty,
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = string.Empty
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PersonaFormKeys.LegajoKey)}""[^>]*>[\s\S]*?El legajo es obligatorio[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'El legajo es obligatorio' to be rendered inside the {PersonaFormKeys.LegajoKey} field-validation span.");
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PersonaFormKeys.ApellidosKey)}""[^>]*>[\s\S]*?Los apellidos son obligatorios[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'Los apellidos son obligatorios' to be rendered inside the {PersonaFormKeys.ApellidosKey} field-validation span.");
    }

    // ──────────────────────────────────────────────
    // T-XX 5: POST 409 → mensaje de unicidad
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenConflictOnEmail_RendersUniquenessMessage()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakePersonaApiClient
        {
            UpdateResult = PersonaCommandResult.Failure(
                new PersonaError(
                    PersonaErrorType.Conflict,
                    "EmailDuplicado",
                    "Ya existe una persona activa con el email ana@example.com.",
                    Categoria: ErrorCategoria.Conflict))
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/editar/{personaId}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync($"/personas/editar/{personaId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-001",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García",
            ["Input.Email"] = "ana@example.com"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Editar persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ana@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ya existe una persona activa con el email ana@example.com.", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 6: POST fallo transporte → recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenTransportFails_ShowsRecoverableErrorAndKeepsForm()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakePersonaApiClient
        {
            UpdateException = new HttpRequestException("network down")
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/editar/{personaId}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync($"/personas/editar/{personaId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-001",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados.
        Assert.Contains("Editar persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-001", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de transporte es visible.
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
    }
}