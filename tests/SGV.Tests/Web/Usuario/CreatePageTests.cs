using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Tests web del módulo Usuarios para PR 4/4: formulario de creación
/// (<c>Create</c>). Espejo de <c>Persona.CreatePageTests</c>: cubre
/// autorización, carga inicial del dropdown de Personas activas, éxito
/// (PRG a Details), 400 (FieldErrors por control), 409 (unicidad de
/// UserName/Email), dropdown vacío (mensaje guía + submit bloqueado) y
/// fallos de transporte.
/// </summary>
[Collection("WebIntegration")]
public sealed class CreatePageTests
{
    private readonly WebIntegrationFixture _fixture;

    public CreatePageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: GET requiere rol Administrador
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            FakePersonaOptionsProvider.Empty());

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 2: GET retorna formulario vacío con dropdown poblado
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticatedAsAdmin_RendersEmptyFormWithPersonaDropdown()
    {
        var personas = new[]
        {
            BuildPersona("L-1", "Ana", "García"),
            BuildPersona("L-2", "Juan", "Pérez")
        };
        var apiClient = new FakeUsuarioApiClient();
        var personasProvider = FakePersonaOptionsProvider.WithActivas(personas);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, personasProvider, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nuevo usuario", content, StringComparison.OrdinalIgnoreCase);

        // El formulario debe exponer los inputs/select esperados (espejo _Form.cshtml).
        Assert.Contains($"name=\"{UsuarioFormKeys.PersonaIdKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{UsuarioFormKeys.UserNameKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{UsuarioFormKeys.EmailKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{UsuarioFormKeys.PasswordKey}\"", content, StringComparison.OrdinalIgnoreCase);

        // El catálogo debe popular el select y el checkbox de roles.
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pérez, Juan", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Roles\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"Administrador\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"GestorVacantes\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"Consultor\"", content, StringComparison.OrdinalIgnoreCase);

        // El proveedor fue invocado exactamente una vez durante el GET.
        Assert.Equal(1, personasProvider.GetActivasCalls);
    }

    // ──────────────────────────────────────────────
    // T-XX 3: GET con dropdown vacío → mensaje guía + submit bloqueado
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenNoActivePersonas_ShowsGuidanceAndDisabledSubmit()
    {
        var apiClient = new FakeUsuarioApiClient();
        var personasProvider = FakePersonaOptionsProvider.Empty();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, personasProvider, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No hay personas activas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/personas/crear", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled=\"disabled\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 4: POST 201 redirige a Details con feedback success
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetailsWithFeedback()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakeUsuarioApiClient
        {
            CreateResult = UsuarioCommandResult.Success(
                new UsuarioDto("u-new", personaId, "anuevo", "anuevo@example.com", new[] { "Consultor" }))
        };
        var personasProvider = FakePersonaOptionsProvider.WithActivas(BuildPersona("L-NEW", "Nueva", "Persona"));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, personasProvider, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios/crear?p=2&search=anuevo&sort=username_asc");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios/crear?p=2&search=anuevo&sort=username_asc",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.UserName"] = "anuevo",
                ["Input.Email"] = "anuevo@example.com",
                ["Input.Password"] = "Password1!",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;

        var posted = Assert.Single(apiClient.CreateCalls);
        Assert.Equal(personaId, posted.PersonaId);
        Assert.Equal("anuevo", posted.UserName);
        Assert.Equal("anuevo@example.com", posted.Email);
        Assert.Equal("Password1!", posted.Password);
        Assert.Equal(new[] { "Consultor" }, posted.Roles);

        // El fake rebasa el Id a "u-{guid}" para evitar colisiones
        // en escenarios donde varios tests comparten la misma
        // configuración. Verificamos que el redirect contiene el id
        // final generado por el fake, no el placeholder inicial.
        Assert.Contains("/seguridad/usuarios/detalle/u-", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("se creó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 5: POST 400 mapea FieldErrors al ModelState con prefijo "Input."
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnInputFields()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakeUsuarioApiClient
        {
            CreateResult = UsuarioCommandResult.Failure(
                new UsuarioError(UsuarioErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["userName"] = new[] { "El nombre de usuario ya está en uso." },
                    ["email"] = new[] { "El email no tiene un formato válido." }
                })
        };
        var personasProvider = FakePersonaOptionsProvider.WithActivas(BuildPersona("L-1", "Ana", "García"));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, personasProvider, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.UserName"] = "aduplicado",
                ["Input.Email"] = "valid@example.com",
                ["Input.Password"] = "Password1!",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(UsuarioFormKeys.UserNameKey)}""[^>]*>[\s\S]*?ya est.{{1,5}} en uso[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message to be rendered inside the {UsuarioFormKeys.UserNameKey} field-validation span.");
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(UsuarioFormKeys.EmailKey)}""[^>]*>[\s\S]*?formato v.{{1,5}}lido[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message to be rendered inside the {UsuarioFormKeys.EmailKey} field-validation span.");

        // El dropdown debe re-renderizarse con las opciones para preservar contexto.
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 6: POST 409 por UserName duplicado → mensaje de unicidad
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenUserNameDuplicate_ReturnsConflictFeedbackAndKeepsForm()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakeUsuarioApiClient
        {
            CreateResult = UsuarioCommandResult.Failure(
                new UsuarioError(
                    UsuarioErrorType.Conflict,
                    "UserNameDuplicado",
                    "Ya existe un usuario activo con el nombre 'agarcia'.",
                    Categoria: ErrorCategoria.Conflict))
        };
        var personasProvider = FakePersonaOptionsProvider.WithActivas(BuildPersona("L-1", "Ana", "García"));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, personasProvider, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.UserName"] = "agarcia",
                ["Input.Email"] = "agarcia@example.com",
                ["Input.Password"] = "Password1!",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Nuevo usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ya existe un usuario activo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("agarcia", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 7: POST fallo de transporte → estado recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenTransportFails_ShowsRecoverableErrorAndKeepsForm()
    {
        var personaId = Guid.NewGuid();
        var apiClient = new FakeUsuarioApiClient
        {
            CreateException = new HttpRequestException("network down")
        };
        var personasProvider = FakePersonaOptionsProvider.WithActivas(BuildPersona("L-1", "Ana", "García"));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, personasProvider, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.UserName"] = "atransport",
                ["Input.Email"] = "atransport@example.com",
                ["Input.Password"] = "Password1!",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Nuevo usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("atransport", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
    }

    private static PersonaDto BuildPersona(string legajo, string nombres, string apellidos)
        => new(Guid.NewGuid(), legajo, nombres, apellidos, null, null, null, null, true);
}