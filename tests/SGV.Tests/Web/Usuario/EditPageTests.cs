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
/// Tests web del módulo Usuarios para PR 4/4: formulario de edición
/// (<c>Edit</c>). Espejo de <c>Persona.EditPageTests</c>: cubre
/// autorización, carga inicial vía <see cref="IUsuarioApiClient.GetByIdAsync"/>,
/// éxito (PRG al propio edit con feedback), 400 (FieldErrors), 409
/// (UserName/Email duplicado), 404 recuperable, y fallos de transporte.
/// </summary>
[Collection("WebIntegration")]
public sealed class EditPageTests
{
    /// <summary>
    /// Id que el handler de autenticación del fixture emite en el claim
    /// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>
    /// cuando se pide rol Administrador. Usar este id como target hace
    /// que la página entre en la rama de auto-edición del usuario sin
    /// necesidad de un handler custom.
    /// </summary>
    private const string AdminSelfUserId = "admin-test";
    private readonly WebIntegrationFixture _fixture;

    public EditPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: GET requiere rol Administrador
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        var id = "u-1";
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            new FakeUsuarioApiClient());

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // WU-6: GET muestra la Persona vinculada como card
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_ConPersonaVinculada_RenderizaCardPreseleccionada()
    {
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario("u-edit", personaId, "Ana", "García");
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = new FakePersonaApiClient();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient,
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            $@"<input(?=[^>]*name=""{Regex.Escape(UsuarioFormKeys.PersonaIdKey)}"")(?=[^>]*value=""{personaId:D}"")[^>]*>",
            content);
        Assert.Contains("Quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cambiar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(
            $@"<select[^>]*name=""{Regex.Escape(UsuarioFormKeys.PersonaIdKey)}""",
            content);
        Assert.Empty(personaApiClient.QueryCalls);
    }

    [Fact]
    public async Task Get_Edit_BotonQuitar_LimpiaSelector_VuelveAEstadoVacio()
    {
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario("u-edit", personaId, "Ana", "García");
        var personaApiClient = new FakePersonaApiClient();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            FakeUsuarioApiClient.WithUsuarioList(usuario),
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(personaApiClient.QueryCalls);
    }

    [Fact]
    public async Task Post_Edit_SinPersonaSeleccionada_PermiteActualizarCamposEditables()
    {
        var id = "u-edit";
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario(id, personaId, "Ana", "García");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.UpdateResult = UsuarioCommandResult.Success(
            new UsuarioDto(id, personaId, "agarcia", "ana@example.com", new[] { "Consultor" }));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{id}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios/editar/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.UserName"] = "agarcia",
                ["Input.Email"] = "ana@example.com",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(apiClient.UpdateCalls);
    }

    // ──────────────────────────────────────────────
    // T-XX 3: GET con id no consultable → estado recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenUsuarioNotFound_ShowsRecoverableState()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/editar/u-missing");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);

        // El formulario no debe renderizarse en estado recuperable.
        Assert.DoesNotContain($"name=\"{UsuarioFormKeys.UserNameKey}\"", content, StringComparison.OrdinalIgnoreCase);

        // El enlace "Volver al listado" debe estar presente.
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 4: POST 200 → PRG al propio edit con feedback success
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenSuccessful_RedirectsToEditWithSuccessFeedback()
    {
        var personaId = Guid.NewGuid();
        var id = "u-update";
        var apiClient = new FakeUsuarioApiClient
        {
            UpdateResult = UsuarioCommandResult.Success(
                new UsuarioDto(id, personaId, "aeditado", "editado@example.com", new[] { "Administrador", "Consultor" }))
        };
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync(
            $"/seguridad/usuarios/editar/{id}?p=2&search=anuevo&sort=username_desc&returnStatus=activas");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios/editar/{id}?p=2&search=anuevo&sort=username_desc&returnStatus=activas",
            new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", antiforgeryToken),
                new("Input.PersonaId", personaId.ToString()),
                new("Input.UserName", "aeditado"),
                new("Input.Email", "editado@example.com"),
                new("Input.Roles", "Administrador"),
                new("Input.Roles", "Consultor")
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/seguridad/usuarios/editar/{id}", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=2", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=anuevo", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=username_desc", location, StringComparison.OrdinalIgnoreCase);

        var updated = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(id, updated.Id);
        Assert.Equal("aeditado", updated.Request.UserName);
        Assert.Equal("editado@example.com", updated.Request.Email);
        Assert.Equal(new[] { "Administrador", "Consultor" }, updated.Request.Roles);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("se actualizó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 5: POST 400 → FieldErrors preservando input
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm()
    {
        var personaId = Guid.NewGuid();
        var id = "u-edit";
        var apiClient = new FakeUsuarioApiClient
        {
            UpdateResult = UsuarioCommandResult.Failure(
                new UsuarioError(UsuarioErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["userName"] = new[] { "El nombre de usuario ya está en uso." },
                    ["email"] = new[] { "El email ya está registrado." }
                })
        };
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{id}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios/editar/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.UserName"] = "aduplicado",
                ["Input.Email"] = "duplicado@example.com",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(UsuarioFormKeys.UserNameKey)}""[^>]*>[\s\S]*?ya est.{{1,5}} en uso[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message to be rendered inside the {UsuarioFormKeys.UserNameKey} field-validation span.");
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(UsuarioFormKeys.EmailKey)}""[^>]*>[\s\S]*?ya est.{{1,5}} registrado[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message to be rendered inside the {UsuarioFormKeys.EmailKey} field-validation span.");

        // El form sigue visible.
        Assert.Contains("Editar usuario", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 6: POST 409 por UserName duplicado → mensaje de unicidad
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenUserNameDuplicate_ReturnsConflictFeedbackAndKeepsForm()
    {
        var personaId = Guid.NewGuid();
        var id = "u-edit";
        var apiClient = new FakeUsuarioApiClient
        {
            UpdateResult = UsuarioCommandResult.Failure(
                new UsuarioError(
                    UsuarioErrorType.Conflict,
                    "UserNameDuplicado",
                    "Ya existe un usuario activo con el nombre 'agarcia'.",
                    Categoria: ErrorCategoria.Conflict))
        };
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{id}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios/editar/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.UserName"] = "agarcia",
                ["Input.Email"] = "agarcia@example.com",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Editar usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ya existe un usuario activo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("agarcia", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 7: POST fallo de transporte → estado recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenTransportFails_ShowsRecoverableErrorAndKeepsForm()
    {
        var personaId = Guid.NewGuid();
        var id = "u-edit";
        var apiClient = new FakeUsuarioApiClient
        {
            UpdateException = new HttpRequestException("network down")
        };
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{id}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios/editar/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.UserName"] = "atransport",
                ["Input.Email"] = "atransport@example.com",
                ["Input.Roles"] = "Consultor"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Editar usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("atransport", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Auto-edición prohibida (AutoEdicionSelf): el admin edita su propio
    // usuario. UI deshabilita los campos editables y agrega alert; el POST
    // además fuerza los roles al estado actual del backend como
    // defensa contra tampering.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAdminEditsSelf_RendersAlertAndDisabledRoleCheckboxes()
    {
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario(AdminSelfUserId, personaId, "Self", "Admin");
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = new FakePersonaApiClient();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient,
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{AdminSelfUserId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Alert visible con el mensaje esperado.
        Assert.Contains("data-usuario-self-rol-alert", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No podés modificar tu propio usuario", content, StringComparison.OrdinalIgnoreCase);

        // Todos los checkboxes de Roles tienen el atributo disabled.
        var checkboxPattern = new Regex(
            @"<input[^>]*name=""Input\.Roles""[^>]*>",
            RegexOptions.IgnoreCase);
        var matches = checkboxPattern.Matches(content);
        Assert.NotEmpty(matches);
        foreach (Match m in matches)
        {
            Assert.Contains("disabled", m.Value, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Matches(
            @"<input(?=[^>]*name=""Input\.UserName"")(?=[^>]*disabled)[^>]*>",
            content);
        Assert.Matches(
            @"<input(?=[^>]*name=""Input\.Email"")(?=[^>]*disabled)[^>]*>",
            content);
        Assert.DoesNotContain("Guardar cambios", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);

        // La card de Persona sigue renderizada con el PersonaDisplay.
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Admin, Self", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenAdminEditsSelfAndPersonaApiReturnsDto_RendersEnrichedCard()
    {
        // Variante enriquecida del self-edit: el API de Personas devuelve
        // un DTO completo. La card debe mostrar Legajo, Documento, Email,
        // Teléfono y el badge Activo.
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario(AdminSelfUserId, personaId, "Self", "Admin");
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaDto = new PersonaDto(
            Id: personaId,
            Legajo: "LEG-7777",
            Nombres: "Self",
            Apellidos: "Admin",
            Email: "self.admin@example.com",
            TipoDocumento: "DNI",
            NumeroDocumento: "30123456",
            Telefono: "+54 11 5555-0000",
            IsActive: true);
        var personaApiClient = FakePersonaApiClient.WithPersonaList(personaDto);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient,
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{AdminSelfUserId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("LEG-7777", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DNI", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("30123456", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("self.admin@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+54 11 5555-0000", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Activa", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-self-rol-alert", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Edit_WhenAdminEditsSelfAndFormTampered_RequestSentWithBackendRoles()
    {
        // El admin edita su propio usuario y manipula el POST para
        // intentar subir su rol. La defensa web del EditModel debe
        // hacer que el request a IUsuarioApiClient.UpdateAsync llegue
        // con los roles ORIGINALES (los que el backend tiene hoy),
        // anulando el tampering del form.
        var personaId = Guid.NewGuid();
        var originalRoles = new string[] { RolesSgv.Consultor };
        var usuario = new UsuarioDto(
            Id: AdminSelfUserId,
            PersonaId: personaId,
            UserName: "admin",
            Email: "admin@example.com",
            Roles: originalRoles,
            Nombres: "Self",
            Apellidos: "Admin");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.UpdateResult = UsuarioCommandResult.Success(usuario);

        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            apiClient,
            personaApiClient,
            adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{AdminSelfUserId}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        // El POST intenta cambiar el rol a Administrador (manipulación).
        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios/editar/{AdminSelfUserId}",
            new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", antiforgeryToken),
                new("Input.UserName", "admin"),
                new("Input.Email", "admin@example.com"),
                new("Input.Roles", "Administrador")
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var updated = Assert.Single(apiClient.UpdateCalls);
        // La defensa web forzó los roles al estado actual del backend.
        Assert.Equal(originalRoles, updated.Request.Roles);
    }

    [Fact]
    public async Task Post_Edit_WhenAdminEditsSelfAndBackendRejectsAutoEdicionSelf_RendersSpecificMessage()
    {
        // Simula el escenario donde la defensa web no logra hacer always-fetch
        // (ej. timeout) y el request llega al backend con roles originales,
        // pero el backend rechaza con AutoEdicionSelf porque el admin sigue
        // siendo el target. La UI debe mostrar el mensaje específico, no el
        // genérico de "Conflicto al persistir el usuario".
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario(AdminSelfUserId, personaId, "Self", "Admin");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        // El fake devuelve Forbidden AutoEdicionSelf para el PUT.
        apiClient.UpdateResult = UsuarioCommandResult.Failure(
            new UsuarioError(
                UsuarioErrorType.Unauthorized,
                "AutoEdicionSelf",
                "No puede modificar su propio usuario.",
                Categoria: ErrorCategoria.Forbidden));

        var personaApiClient = new FakePersonaApiClient();
        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            apiClient,
            personaApiClient,
            adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{AdminSelfUserId}");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios/editar/{AdminSelfUserId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["Input.UserName"] = "admin",
                ["Input.Email"] = "admin@example.com",
                ["Input.Roles"] = "Administrador"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("No podés modificar tu propio usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Conflicto al persistir el usuario", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenAdminEditsAnotherUser_DoesNotShowAutoEdicionSelf()
    {
        // Contrapartida del self-edit: cuando el admin edita a OTRO
        // usuario, NO se muestra el alert ni los checkboxes quedan
        // deshabilitados (deben estar operables).
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario("u-otro", personaId, "Otro", "Usuario");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = new FakePersonaApiClient();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            apiClient,
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/editar/u-otro");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Sin alert.
        Assert.DoesNotContain("data-usuario-self-rol-alert", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No podés modificar tu propio usuario", content, StringComparison.OrdinalIgnoreCase);

        // Checkboxes habilitados.
        var checkboxPattern = new Regex(
            @"<input[^>]*name=""Input\.Roles""[^>]*>",
            RegexOptions.IgnoreCase);
        var matches = checkboxPattern.Matches(content);
        Assert.NotEmpty(matches);
        foreach (Match m in matches)
        {
            Assert.DoesNotContain("disabled", m.Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static UsuarioDto BuildUsuario(string id, Guid personaId, string nombres, string apellidos)
        => new(id, personaId, "agarcia", "ana@example.com", new[] { "Consultor" }, nombres, apellidos);
}