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
/// Tests web del módulo Personas para PR 4/4: formulario de creación
/// (<c>Create</c>). Espejo de <c>CargoCreatePageTests</c>: cubre
/// autorización, carga inicial, éxito (PRG a Details), 400 (FieldErrors),
/// 409 (unicidad) y fallos de transporte.
/// </summary>
[Collection("WebIntegration")]
public sealed class CreatePageTests
{
    private readonly WebIntegrationFixture _fixture;

    public CreatePageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: GET requiere rol Administrador → Forbid/redirect 403
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        await using var lease = await _fixture.CreatePersonaLeaseAsync(new FakePersonaApiClient());

        var response = await lease.Client.GetAsync("/personas/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 2: GET retorna formulario vacío
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticatedAsAdmin_RendersEmptyForm()
    {
        await using var lease = await _fixture.CreatePersonaLeaseAsync(new FakePersonaApiClient(), adminRole: true);

        var response = await lease.Client.GetAsync("/personas/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nueva persona", content, StringComparison.OrdinalIgnoreCase);

        // El formulario debe tener los inputs esperados (espejo _Form.cshtml).
        Assert.Contains($"name=\"{PersonaFormKeys.LegajoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.NombresKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.ApellidosKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.EmailKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.TipoDocumentoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.NumeroDocumentoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.TelefonoKey}\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 3: POST 201 redirige a Details
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetailsWithFeedback()
    {
        var newId = Guid.NewGuid();
        var apiClient = new FakePersonaApiClient
        {
            CreateResult = PersonaCommandResult.Success(
                new PersonaDto(newId, "L-NEW", "Nueva", "Persona", null, null, null, null, true))
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-NEW",
            ["Input.Nombres"] = "Nueva",
            ["Input.Apellidos"] = "Persona"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/personas/detalle/{newId}", location, StringComparison.OrdinalIgnoreCase);

        var posted = Assert.Single(apiClient.CreateCalls);
        Assert.Equal("L-NEW", posted.Legajo);
        Assert.Equal("Nueva", posted.Nombres);
        Assert.Equal("Persona", posted.Apellidos);
    }

    // ──────────────────────────────────────────────
    // T-XX 4: POST 400 mapea FieldErrors al ModelState con prefijo "Input."
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnInputFields()
    {
        var apiClient = new FakePersonaApiClient
        {
            CreateResult = PersonaCommandResult.Failure(
                new PersonaError(PersonaErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["legajo"] = new[] { "El legajo es obligatorio." },
                    ["email"] = new[] { "Email inválido." }
                })
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-RT",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García"
        }));

        // El form debe re-renderizarse con el mensaje en el field-validation span correspondiente.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PersonaFormKeys.LegajoKey)}""[^>]*>[\s\S]*?El legajo es obligatorio[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'El legajo es obligatorio' to be rendered inside the {PersonaFormKeys.LegajoKey} field-validation span.");
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PersonaFormKeys.EmailKey)}""[^>]*>[\s\S]*?Email inválido[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'Email inválido' to be rendered inside the {PersonaFormKeys.EmailKey} field-validation span.");
    }

    // ──────────────────────────────────────────────
    // T-XX 5: POST 409 preserva datos con feedback claro
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenConflictOnLegajo_RendersGeneralFeedbackAndKeepsForm()
    {
        // AC: 409 (Legajo/Email/NumeroDocumento duplicado) → el PageModel
        // agrega el mensaje del campo afectado al ModelState[string.Empty]
        // para que aparezca en el ValidationSummary, preservando el input
        // del usuario.
        var apiClient = new FakePersonaApiClient
        {
            CreateResult = PersonaCommandResult.Failure(
                new PersonaError(
                    PersonaErrorType.Conflict,
                    "LegajoDuplicado",
                    "Ya existe una persona activa con el legajo L-DUP.",
                    Categoria: ErrorCategoria.Conflict))
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-DUP",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados.
        Assert.Contains("Nueva persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-DUP", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de unicidad aparece en el summary (model error de clave vacía).
        Assert.Contains("Ya existe una persona activa con el legajo L-DUP.", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 6: POST fallo de transporte → estado recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenTransportFails_ShowsRecoverableErrorAndKeepsForm()
    {
        // AC: el PageModel captura HttpRequestException / TaskCanceledException
        // vía TransportFailureClassifier y muestra un mensaje recuperable,
        // preservando el input del usuario.
        var apiClient = new FakePersonaApiClient
        {
            CreateException = new HttpRequestException("boom")
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-TRANSPORT",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados.
        Assert.Contains("Nueva persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-TRANSPORT", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de transporte es visible.
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
    }
}