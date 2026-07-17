using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
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
        await using var lease = await CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            adminRole: false);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // WU-5: GET expone buscador sin catálogo completo
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_NoRenderizaSelectPoblado_RenderizaBotonBuscar()
    {
        var personas = new[]
        {
            BuildPersona("L-1", "Ana", "García"),
            BuildPersona("L-2", "Juan", "Pérez")
        };
        var personaApiClient = FakePersonaApiClient.WithPersonaList(personas);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotMatch(
            $@"<select[^>]*name=""{Regex.Escape(UsuarioFormKeys.PersonaIdKey)}""",
            content);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // WU-5: totalCount=0 muestra banner y CTA
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_ConTotalCountCero_MuestraBannerConCtaAPersonasCrear()
    {
        var personaApiClient = new FakePersonaApiClient
        {
            QueryHandler = query => new PersonaListadoDto([], 0, query.Page, query.PageSize)
        };

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient(),
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No hay personas disponibles para asociar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/personas/crear\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Crear persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(personaApiClient.QueryCalls);
        Assert.Equal(1, query.Page);
        Assert.Equal(1, query.PageSize);
        Assert.True(query.SoloSinUsuario);
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
        await using var lease = await CreateUsuarioLeaseAsync(
            apiClient,
            adminRole: true,
            BuildPersona("L-NEW", "Nueva", "Persona"));

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
        await using var lease = await CreateUsuarioLeaseAsync(
            apiClient,
            adminRole: true,
            BuildPersona("L-1", "Ana", "García"));

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

        // El selector debe conservar el identificador enviado para que la persona
        // pueda cambiarse sin volver a completar el resto del formulario.
        Assert.Matches(
            $@"<input(?=[^>]*name=""{Regex.Escape(UsuarioFormKeys.PersonaIdKey)}"")(?=[^>]*value=""{personaId:D}"")[^>]*>",
            content);
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
        await using var lease = await CreateUsuarioLeaseAsync(
            apiClient,
            adminRole: true,
            BuildPersona("L-1", "Ana", "García"));

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

    [Fact]
    public async Task Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId()
    {
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(
            personaId,
            "L-1",
            "Ana",
            "García",
            "ana@example.com",
            "DNI",
            "12345678",
            null,
            true);
        var usuarioApiClient = new FakeUsuarioApiClient
        {
            CreateResult = UsuarioCommandResult.Failure(
                new UsuarioError(
                    UsuarioErrorType.Conflict,
                    "PersonaYaTieneUsuario",
                    "La persona ya tiene un usuario asociado.",
                    Categoria: ErrorCategoria.Conflict))
        };
        var personaApiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient,
            personaApiClient,
            adminRole: true);

        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios/crear",
            new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", antiforgeryToken),
                new("Input.PersonaId", personaId.ToString()),
                new("PersonaDisplay", "García, Ana (DNI: 12345678)"),
                new("Input.UserName", "agarcia"),
                new("Input.Email", "ana@example.com"),
                new("Input.Password", "Password1!"),
                new("Input.Roles", "Administrador"),
                new("Input.Roles", "Consultor")
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Matches(
            $@"<span[^>]*data-valmsg-for=""{Regex.Escape(UsuarioFormKeys.PersonaIdKey)}""[^>]*>[\s\S]*?Esa persona ya tiene un usuario activo\.[\s\S]*?</span>",
            content);
        Assert.Matches(
            $@"<input(?=[^>]*name=""{Regex.Escape(UsuarioFormKeys.PersonaIdKey)}"")(?=[^>]*value=""{personaId:D}"")[^>]*>",
            content);
        Assert.Contains("value=\"agarcia\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"ana@example.com\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            $@"<input(?=[^>]*name=""{Regex.Escape(UsuarioFormKeys.PasswordKey)}"")(?=[^>]*type=""password"")[^>]*>",
            content);
        Assert.Contains("García, Ana (DNI: 12345678)", content, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            @"<input(?=[^>]*name=""Input\.Roles"")(?=[^>]*value=""Administrador"")(?=[^>]*checked)[^>]*>",
            content);
        Assert.Matches(
            @"<input(?=[^>]*name=""Input\.Roles"")(?=[^>]*value=""Consultor"")(?=[^>]*checked)[^>]*>",
            content);
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
        await using var lease = await CreateUsuarioLeaseAsync(
            apiClient,
            adminRole: true,
            BuildPersona("L-1", "Ana", "García"));

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

    private Task<WebClientLease> CreateUsuarioLeaseAsync(
        IUsuarioApiClient usuarioApiClient,
        bool adminRole,
        params PersonaDto[] personas)
        => _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient,
            FakePersonaApiClient.WithPersonaList(personas),
            adminRole);

    private static PersonaDto BuildPersona(string legajo, string nombres, string apellidos)
        => new(Guid.NewGuid(), legajo, nombres, apellidos, null, null, null, null, true);
}