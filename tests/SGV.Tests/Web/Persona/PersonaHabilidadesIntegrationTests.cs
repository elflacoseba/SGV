using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests de integración web end-to-end para la página
/// <c>/personas/{id}/habilidades</c> y sus handlers POST. Slice 3b del
/// change <c>implementa-persona-habilidades</c>. Análogo a la batería de
/// <c>CargoHabilidadesMutationTests</c> + <c>CargoHabilidadesDeleteErrorTests</c>
/// + <c>CargoHabilidadesPrgTests</c> + <c>CargoHabilidadesAccessTests</c>:
/// cubre el flujo antiforgery + PRG + TempData feedback a través del host
/// real (no PageModel directo).
/// </summary>
[Collection("WebIntegration")]
public sealed class PersonaHabilidadesIntegrationTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonaHabilidadesIntegrationTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private static PersonaDto BuildActivePersona() => new(
        Guid.NewGuid(), "L-001", "Ana", "García",
        "ana@example.com", null, "DNI", "Documento",
        "30123456", null, true);

    private static PersonaDto BuildInactivePersona(Guid id) => new(
        id, "L-002", "Persona", "Inactiva",
        null, null, null, null, null, null, false);

    // ──────────────────────────────────────────────
    // 3b.4 — POST handlers end-to-end con antiforgery + PRG + TempData
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostAsignar_Admin_EndToEnd_CallsUpsertSkillAsync_AndPrgRedirectsWithSuccess()
    {
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Success(
            new PersonaSkillDto(skillId, nivelId));

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["SkillId"] = skillId.ToString(),
                ["NivelHabilidadId"] = nivelId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/personas/{personaId}/habilidades", location, StringComparison.OrdinalIgnoreCase);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal((personaId, skillId, new AsignarPersonaSkillRequest(nivelId)), upsert);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("asign", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostQuitar_Admin_EndToEnd_CallsDeleteSkillAsync_AndPrgRedirectsWithSuccess()
    {
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteResult = new PersonaSkillDeleteResult(
            true, HttpStatusCode.NoContent, null, null);

        var skillId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var delete = Assert.Single(apiClient.SkillDeleteCalls);
        Assert.Equal((personaId, skillId), delete);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("quit", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsignar_NonAdmin_Forbidden_DoesNotInvokeClient()
    {
        // Equivalente del CargoHabilidadesAccessTests: usuario autenticado
        // sin rol Administrador recibe Forbid (302 a /error/403) y el
        // cliente API NO se invoca.
        var personaId = Guid.NewGuid();
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: false);

        var signInGet = await lease.Client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signInGet);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["SkillId"] = skillId.ToString(),
                ["NivelHabilidadId"] = nivelId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.SkillUpsertCalls);
    }

    [Fact]
    public async Task PostAsignar_InactivePersona_RedirectsWithoutInvokingClient()
    {
        // R-PM-01 + decisión UX de design.md: una persona inactiva NO
        // permite mutaciones. El handler bloquea ANTES de invocar al
        // cliente HTTP.
        var personaId = Guid.NewGuid();
        var inactive = new PersonaDto(
            personaId, "L-002", "Persona", "Inactiva",
            null, null, null, null, null, null, false);
        var apiClient = FakePersonaApiClient.WithPersonaList(inactive);
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        // Para una persona inactiva, GET redirige a /error/404; no hay
        // formulario con antiforgery. Usamos /auth/sign-in para extraer
        // el token, igual que en el patrón de CargoHabilidadesAccessTests.
        Assert.Equal(HttpStatusCode.Redirect, getResponse.StatusCode);

        var signInGet = await lease.Client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signInGet);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["SkillId"] = skillId.ToString(),
                ["NivelHabilidadId"] = nivelId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(apiClient.SkillUpsertCalls);
    }

    [Fact]
    public async Task PostQuitar_InactivePersona_RedirectsWithoutInvokingClient()
    {
        var personaId = Guid.NewGuid();
        var inactive = new PersonaDto(
            personaId, "L-002", "Persona", "Inactiva",
            null, null, null, null, null, null, false);
        var apiClient = FakePersonaApiClient.WithPersonaList(inactive);

        var skillId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var signInGet = await lease.Client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(signInGet);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(apiClient.SkillDeleteCalls);
    }

    [Fact]
    public async Task PostAsignar_BackendValidationFailure_RedirectsWithDangerMessage()
    {
        // El backend rechaza con PersonaSkillErrorType.Validation → el
        // handler traduce a ErrorCategoria.Validation, PRG con TempData
        // danger. La UI muestra el mensaje accionable sin filtrar stack
        // traces.
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Failure(
            new PersonaSkillError(
                PersonaSkillErrorType.Validation,
                "NivelHabilidadNoExiste",
                "El nivel no existe.",
                StatusCode: 400,
                Categoria: SGV.Contracts.Comun.ErrorCategoria.Validation));

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["SkillId"] = skillId.ToString(),
                ["NivelHabilidadId"] = nivelId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("class=\"alert alert-danger alert-dismissible\"", refreshedContent, StringComparison.Ordinal);
        Assert.Contains("nivel", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NivelHabilidadNoExiste", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at SGV.", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsignar_BackendNotFoundFailure_RedirectsWithDangerMessage()
    {
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertResult = PersonaSkillCommandResult.Failure(
            new PersonaSkillError(
                PersonaSkillErrorType.NotFound,
                "PersonaNoEncontrada",
                "La persona no existe.",
                StatusCode: 404,
                Categoria: SGV.Contracts.Comun.ErrorCategoria.NotFound));

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["SkillId"] = skillId.ToString(),
                ["NivelHabilidadId"] = nivelId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("class=\"alert alert-danger alert-dismissible\"", refreshedContent, StringComparison.Ordinal);
        Assert.Contains("no existe", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PersonaNoEncontrada", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsignar_TransportFailure_RedirectsWithDangerMessage()
    {
        // Una falla de transporte (HttpRequestException) debe traducirse
        // en PRG con TempData danger (mensaje accionable, sin filtrar
        // stack trace).
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillUpsertException = new HttpRequestException("network down");

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["SkillId"] = skillId.ToString(),
                ["NivelHabilidadId"] = nivelId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("class=\"alert alert-danger alert-dismissible\"", refreshedContent, StringComparison.Ordinal);
        Assert.Contains("No se pudo contactar", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at SGV.", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostQuitar_BackendNotFound_RedirectsWithWarningMessage()
    {
        // 404 al quitar = race condition natural. PRG con TempData warning.
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteResult = new PersonaSkillDeleteResult(
            false, HttpStatusCode.NotFound, "AsociacionNoEncontrada",
            "La asociación ya no existe.",
            Categoria: SGV.Contracts.Comun.ErrorCategoria.NotFound);

        var skillId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("class=\"alert alert-warning alert-dismissible\"", refreshedContent, StringComparison.Ordinal);
        Assert.Contains("ya no existe", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostQuitar_TransportFailure_RedirectsWithDangerMessage()
    {
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.SkillDeleteException = new HttpRequestException("network down");

        var skillId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("class=\"alert alert-danger alert-dismissible\"", refreshedContent, StringComparison.Ordinal);
        Assert.Contains("No se pudo contactar", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Render_StatusMessageAlert_LlevaAlertDismissibleYBotonClose()
    {
        // Espejo del CargoHabilidadesValidationTests.Render_StatusMessageAlert_Lleva
        // AlertDismissibleYBotonClose: el banner de feedback tras un PRG
        // debe ser dismissible (alert-dismissible + botón btn-close) para
        // que el usuario pueda cerrarlo sin esperar al próximo redirect.
        // Esta cobertura blinda la decisión de UI acordada en el patrón
        // vigente del módulo Cargos (commit a96eeea9), llevada al módulo
        // Personas.
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        // SkillDeleteResult default = Success NoContent, suficiente para
        // que el handler setee TempData success y emita PRG.

        var skillId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        // Follow the GET (warming antiforgery) → POST Quitar → PRG →
        // GET redirect chain. El GET renderizado debe contener la alerta
        // con la clase alert-dismissible y el botón btn-close de Bootstrap.
        var getResponse = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);
        var postResponse = await lease.Client.PostAsync(
            $"/personas/{personaId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var refreshed = await lease.Client.GetAsync(postResponse.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("alert-dismissible", refreshedContent, StringComparison.Ordinal);
        Assert.Contains("btn-close", refreshedContent, StringComparison.Ordinal);
        Assert.Contains("data-bs-dismiss=\"alert\"", refreshedContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_Admin_RowStartsLockedAndExposesEditSaveAndSweetAlertDeleteControls()
    {
        var persona = BuildActivePersona();
        var skillId = Guid.NewGuid();
        var level = new NivelHabilidadDto(Guid.NewGuid(), "BAS", "Básico", 1, 1);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);
        apiClient.GetSkillsResult =
        [
            new PersonaSkillDetailDto(
                new HabilidadDto(skillId, "H-001", "Liderazgo", null, null, "Conductual"),
                level)
        ];
        var habilidadApiClient = FakeHabilidadApiClient.WithHabilidadList();
        habilidadApiClient.NivelesResult = [level];

        await using var lease = await _fixture.CreatePersonaLeaseAsync(
            apiClient, habilidadApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/personas/{persona.Id}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-skill-management-row", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-editable", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-edit-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-save-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-skill-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skill-management.js", content, StringComparison.OrdinalIgnoreCase);

        // Contrato DOM endurezido (cambio fix Quitar wiring): el botón
        // Quitar debe ser type="submit" para que form.requestSubmit(submitter)
        // del JS funcione con un submitter real (antes era type="button" y
        // requestSubmit(submitter) lanzaba NotSupportedError, abortando el
        // submit del POST). La aserción regex exige type="submit"
        // explícito en el botón marcado con data-skill-delete-button.
        Assert.Matches(
            new Regex(
                @"<button[^>]*type\s*=\s*""submit""[^>]*data-skill-delete-button",
                RegexOptions.IgnoreCase),
            content);

        // Contrato DOM endurezido: el form Quitar debe apuntar a
        // handler=Quitar vía tag helper asp-page-handler. Esto blinda
        // contra una regresión donde el form Quitar quede con action
        // vacía/heredada y el submit del botón termine en un handler
        // equivocado.
        Assert.Matches(
            new Regex(
                @"<form[^>]*data-skill-delete-form[^>]*action\s*=\s*""[^""]*\?handler=Quitar""",
                RegexOptions.IgnoreCase),
            content);
    }

    // ──────────────────────────────────────────────
    // (analog a ApiBearerTokenIntegrationTests para Cargo).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_PersonaHabilidades_ForwardsBearerTokenToPersonaApi()
    {
        // Garantiza que el ApiBearerTokenHandler está en el pipeline del
        // HttpClient de IPersonaApiClient: cuando la Razor Page invoca
        // GetByIdAsync/GetSkillsAsync, la llamada a la API lleva el
        // bearer del usuario autenticado.
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId, "L-001", "Ana", "García",
            null, null, null, null, null, null, true);

        // Stub de auth: emite un JWT firmado con la clave de test que
        // coincide con el fixture (AdminJwtTestHelper.SigningKey).
        var expectedJwt = AdminJwtTestHelper.BuildAdminRoleJwt();
        var authHandler = new WebTestBuilders.RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(
                    new LoginResponse(expectedJwt, DateTimeOffset.UtcNow.AddHours(1)))
            });

        // Recording handler para el subrecurso persona: emite 200 OK con
        // payload vacío + captura cada request para inspección.
        var personaHandler = new RecordingPersonaHandler(persona);

        // Construir el lease vía el patrón del fixture composite. Como
        // aquí necesitamos overrides de handler (no del cliente tipado),
        // usamos el helper de bridge equivalente a CreateCargoBridgeLease.
        await using var lease = await _fixture.CreatePersonaBridgeLeaseAsync(
            authHandler, personaHandler);

        var response = await lease.Client.GetAsync($"/personas/{personaId}/habilidades");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El primer request saliente al endpoint /api/v1/personas/{id}
        // debe llevar el bearer del usuario autenticado.
        var personaRequest = personaHandler.Requests
            .FirstOrDefault(r => r.RequestUri?.AbsolutePath?.Contains($"/api/v1/personas/{personaId}", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(personaRequest);
        Assert.NotNull(personaRequest!.Headers.Authorization);
        Assert.Equal("Bearer", personaRequest.Headers.Authorization!.Scheme);
        Assert.Equal(expectedJwt, personaRequest.Headers.Authorization.Parameter);
    }

    private sealed class RecordingPersonaHandler(PersonaDto persona) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (path.Contains("/skills", StringComparison.OrdinalIgnoreCase))
            {
                response.Content = System.Net.Http.Json.JsonContent.Create(Array.Empty<PersonaSkillDetailDto>());
            }
            else if (path.Contains("/tipos-documento", StringComparison.OrdinalIgnoreCase))
            {
                response.Content = System.Net.Http.Json.JsonContent.Create(Array.Empty<SGV.Contracts.Personas.Consultas.Dtos.TipoDocumentoDto>());
            }
            else
            {
                response.Content = System.Net.Http.Json.JsonContent.Create(persona);
            }
            return Task.FromResult(response);
        }
    }
}
