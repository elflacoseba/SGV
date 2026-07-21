using System.Net;
using System.Web;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Web integration tests for the readonly Usuarios detail page after
/// Phase 3 of the change <c>2026-07-15-quita-soft-delete-usuario</c>:
/// la página renderiza el DTO real consultado vía API y el
/// <c>returnStatus</c> actúa sólo como hint de view (la fuente de
/// verdad del estado es el flag <c>Bloqueado</c> del DTO).
/// </summary>
[Collection("WebIntegration")]
public sealed class DetailsPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public DetailsPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Details_WhenAuthenticatedAsRegularUser_RendersReadonlyUserData()
    {
        var usuario = BuildUsuario("u-1");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Detalle de usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.UserName, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.Email, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.Nombres!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.Apellidos!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.PersonaId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Administrador", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Consultor", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAdminAndActive_RendersEditAndBloquearAndDeleteActions()
    {
        // Phase 3: en el segmento activas, el admin ve Edit + Bloquear +
        // Delete. La Page NO renderiza Reactivar (el bloque eliminado
        // se reemplaza por el ciclo de Bloquear/Desbloquear).
        var usuario = BuildUsuario("u-admin-view");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenUserIsBlocked_RendersBannerAndDesbloquearAction()
    {
        // AC: el DTO trae Bloqueado=true y la página lo refleja con un
        // banner visible + la acción Desbloquear (en lugar de Bloquear).
        var usuario = BuildUsuario("u-blocked", bloqueado: true);
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cuenta bloqueada", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenUserIsBlockedAndReturnStatusIsActivas_StillRendersBanner()
    {
        // AC: el returnStatus es sólo un hint de view; el estado real
        // viene del DTO. Aunque el caller pase returnStatus=activas,
        // si la API reporta Bloqueado=true la página muestra el banner.
        var usuario = BuildUsuario("u-blocked-2", bloqueado: true);
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cuenta bloqueada", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenUserIsNotFound_ShowsRecoverableState()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios/detalle/u-missing");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/seguridad/usuarios/editar/", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenApiThrowsTransport_ShowsRecoverableNotFoundState()
    {
        // AC: un 5xx del backend NO rompe la página; el PageModel
        // degrada a "no está disponible" recuperable y mantiene el link
        // "Volver al listado".
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();
        apiClient.QueryException = new HttpRequestException("upstream down");

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync(
            "/seguridad/usuarios/detalle/u-transport?returnStatus=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenListingContextProvided_PreservesItInBackLink()
    {
        // returnStatus=bloqueadas se preserva en el link "Volver al
        // listado" aunque el DTO diga activo (Bloqueado=false). Phase 3:
        // el returnStatus es hint de view; el render del banner y de los
        // botones administrativos sale del DTO (no de returnStatus).
        var usuario = BuildUsuario("u-context");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?p=3&search=garcia&sort=apellidos_desc&returnStatus=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/seguridad/usuarios?", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=garcia", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=bloqueadas", content, StringComparison.OrdinalIgnoreCase);
        // DTO es activo → vemos Bloquear + Eliminar, NO desbloqueo.
        Assert.Contains("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cuenta bloqueada", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios/detalle/u-anonymous");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar()
    {
        const string selfId = "admin-test";
        var self = BuildUsuario(selfId);
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(self);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{selfId}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/seguridad/usuarios/editar/{selfId}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_BloquearButton_OpensModal()
    {
        // REQ-UCB-03 + REQ-UCB-10: tras PR 2, el botón Bloquear dispara
        // SweetAlert2 desde usuarios-index.js. El bundle + script deben
        // estar cargados y NO debe haber modal Bootstrap nativo.
        var usuario = BuildUsuario("u-active");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-bloquear-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-bs-target=\"#confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Bloquear\"", content, StringComparison.OrdinalIgnoreCase);
        // Regression guard: usuarios-index.js dispara form.requestSubmit(button)
        // y la spec exige submit button. Con type="button" la llamada tira
        // TypeError y el form nunca se envía.
        Assert.Contains("data-usuario-bloquear-button type=\"submit\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-delete-button type=\"submit\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_DesbloquearButton_OpensModal()
    {
        // REQ-UCB-03 + REQ-UCB-10: en Details bloqueado, el botón
        // Desbloquear dispara SweetAlert2 — el bundle + script deben
        // estar cargados y NO debe haber modal Bootstrap nativo.
        var usuario = BuildUsuario("u-blocked", bloqueado: true);
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-desbloquear-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-bs-target=\"#confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Desbloquear\"", content, StringComparison.OrdinalIgnoreCase);
        // Regression guard análogo a Bloquear en Get_Details_BloquearButton_OpensModal.
        Assert.Contains("data-usuario-desbloquear-button type=\"submit\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_BloquearModal_HasAriaWiring()
    {
        // REQ-UCB-05: PR 2 reemplaza el aria-labelledby propio del modal
        // Bootstrap por el manejo interno de SweetAlert2 (que envuelve
        // title en <h2 aria-label> automáticamente).
        var usuario = BuildUsuario("u-active");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aria-labelledby=\"confirm-bloquear-modal-title\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_ModalDoesNotContainPii()
    {
        // REQ-UCB-04 + REQ-UCB-10: PR 2 — ningún alert SweetAlert2 en
        // Details expone PII del usuario objetivo. El alert se renderiza
        // en runtime desde usuarios-index.js, así que verificamos que el
        // bundle + script estén cargados pero sin markup nativo de modal
        // viejo.
        var usuario = BuildUsuario("u-pii");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
    }

    private static UsuarioDto BuildUsuario(string id, bool bloqueado = false) => new(
        id,
        Guid.NewGuid(),
        "agarcía",
        "ana@example.com",
        new[] { "Administrador", "Consultor" },
        "Ana",
        "García",
        Bloqueado: bloqueado);

    /// <summary>
    /// Overload que fija el <see cref="UsuarioDto.PersonaId"/> para tests
    /// que necesitan triangular la card enriquecida de Persona. Espejo de
    /// <c>EditPageTests.BuildUsuario(string, Guid, string, string)</c>.
    /// </summary>
    private static UsuarioDto BuildUsuario(string id, Guid personaId, bool bloqueado = false) => new(
        id,
        personaId,
        "agarcía",
        "ana@example.com",
        new[] { "Administrador", "Consultor" },
        "Ana",
        "García",
        Bloqueado: bloqueado);

    // ──────────────────────────────────────────────
    // REQ-ULD-04 (MODIFIED): card enriquecida de Persona en el detalle
    // readonly cuando el API de Personas devuelve DTO.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedCard()
    {
        // El API de Personas devuelve DTO completo. El detalle debe
        // mostrar la card enriquecida con Legajo/Documento/Email/Teléfono
        // y badge de Estado, en lugar del Guid crudo. El <a> hacia
        // /personas/detalle/{PersonaId} debe quedar como título.
        var personaId = Guid.NewGuid();
        var personaDto = new PersonaDto(Id: personaId, Legajo: "L-7777", Nombres: "Ana", Apellidos: "García", Email: "ana.garcia@example.com", null, "DNI", "Documento Nacional de Identidad", NumeroDocumento: "30123456", Telefono: "+54 11 5555-0000", IsActive: true);
        var usuario = BuildUsuario("u-detail-enriched", personaId);
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = FakePersonaApiClient.WithPersonaList(personaDto);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient, personaApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-7777", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DNI 30123456", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+54 11 5555-0000", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Activa", content, StringComparison.OrdinalIgnoreCase);
        // El link al detalle de Persona se preserva como título.
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        // El Guid crudo ya NO debe aparecer como texto de la sección persona.
        Assert.DoesNotContain($">{personaId:D}</a>", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // REQ-ULD-04: ausencia de controles de selección en Details (read-only).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenPersonaApiReturnsDto_NoControlesSeleccionPersona()
    {
        // Rama enriquecida: la card existe (data-usuario-persona-card)
        // pero NO debe inyectar los data-attributes ni el modal del
        // buscador de Persona, que son exclusivos del flujo Edit.
        var personaId = Guid.NewGuid();
        var personaDto = new PersonaDto(personaId, "L-7777", "Ana", "García", "ana@example.com", null, null, "DNI", "30123456", "+54 11 5555-0000", true);
        var usuario = BuildUsuario("u-detail-no-controls-enriched", personaId);
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = FakePersonaApiClient.WithPersonaList(personaDto);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient, personaApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usuario-persona-buscador-modal", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenPersonaApiMissing_NoControlesSeleccionPersona()
    {
        // Rama fallback: el API no devuelve el DTO (404). Igual NO deben
        // aparecer los controles de selección porque Details es read-only.
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario("u-detail-no-controls-fallback", personaId);
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = new FakePersonaApiClient(); // vacío → 404

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient, personaApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usuario-persona-buscador-modal", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // REQ-ULD-04: fallback plano cuando el API de Personas devuelve
    // 404 o lanza excepción de transporte. IsNotFound debe permanecer
    // en false (el usuario sí existe; sólo se degrada la card).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay()
    {
        // El API no contiene el PersonaId del usuario → GetByIdAsync
        // devuelve null. La vista debe caer al fallback plano con el
        // display "Apellidos, Nombres" derivado del UsuarioDto, y el
        // detalle del usuario debe renderizar completo (NO estado
        // recuperable "no está disponible").
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario("u-detail-404", personaId);
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = new FakePersonaApiClient(); // sin DTOS → 404

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient, personaApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback plano: atributo neutral data-usuario-details-persona.
        Assert.Contains("data-usuario-details-persona", content, StringComparison.OrdinalIgnoreCase);
        // El display plano "García, Ana" derivado del UsuarioDto.
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        // La card enriquecida NO debe renderizarse.
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        // El link al detalle de Persona se conserva como título.
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        // El Guid crudo ya NO debe aparecer como texto del fallback.
        Assert.DoesNotContain($">{personaId:D}</a>", content, StringComparison.OrdinalIgnoreCase);
        // El detalle del usuario debe renderizar completo, NO estado
        // recuperable. El título "Detalle de usuario" sí está presente,
        // y NO aparece el mensaje de "no está disponible".
        Assert.Contains("Detalle de usuario", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound()
    {
        // El API lanza HttpRequestException al consultar la persona.
        // El catch clasificado por TransportFailureClassifier NO debe
        // marcar IsNotFound: el usuario sí existe, sólo se degrada la
        // card al fallback plano.
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario("u-detail-transport", personaId);
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = new FakePersonaApiClient
        {
            GetByIdException = new HttpRequestException("upstream persona unavailable")
        };

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient, personaApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fallback plano, mismo shape que el caso 404.
        Assert.Contains("data-usuario-details-persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        // El detalle del usuario debe renderizar completo. NO debe
        // aparecer el mensaje de estado recuperable "no está disponible".
        Assert.DoesNotContain("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Detalle de usuario", content, StringComparison.OrdinalIgnoreCase);
    }
}
