using System.Net;
using System.Web;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
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
        // REL-004: el bootstrap de AuthenticateClientAsync envía
        // UserNameOrEmail="admin", que AuthSessionFactory siembra como
        // primer claim NameIdentifier (RIS-002). El JWT agrega luego
        // NameIdentifier="admin-test" pero FindFirstValue devuelve el
        // primero, así que CurrentUserId="admin" en este test. Si el
        // DTO trae id="admin" el guard EsAutoAccion activa y la vista
        // debe mostrar sólo Edit (no Bloquear/Eliminar/Desbloquear).
        const string selfId = "admin";
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
        // REQ-UCB-03 + REQ-UCB-10: en Details, el botón Bloquear debe
        // disparar el modal en lugar de hacer submit directo.
        var usuario = BuildUsuario("u-active");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-bloquear-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-target=\"#confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Bloquear\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_DesbloquearButton_OpensModal()
    {
        // REQ-UCB-03 + REQ-UCB-10: en Details bloqueado, el botón
        // Desbloquear debe disparar el modal en lugar de hacer submit directo.
        var usuario = BuildUsuario("u-blocked", bloqueado: true);
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-desbloquear-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-target=\"#confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Desbloquear\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_BloquearModal_HasAriaWiring()
    {
        // REQ-UCB-05: atributos AA mínimos del modal en Details.
        var usuario = BuildUsuario("u-active");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-labelledby=\"confirm-bloquear-modal-title\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-hidden=\"true\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tabindex=\"-1\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_ModalDoesNotContainPii()
    {
        // REQ-UCB-04 + REQ-UCB-10: ningún modal en Details expone PII del
        // usuario objetivo (UserName / Email / Nombres / Apellidos).
        var usuario = BuildUsuario("u-pii");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            $"/seguridad/usuarios/detalle/{usuario.Id}?returnStatus=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        var modalStart = content.IndexOf("id=\"confirm-bloquear-modal\"", StringComparison.OrdinalIgnoreCase);
        Assert.True(modalStart >= 0);
        var nextModalStart = content.IndexOf("id=\"confirm-", modalStart + 1, StringComparison.OrdinalIgnoreCase);
        var modalEnd = nextModalStart >= 0 ? nextModalStart : content.Length;
        var modalBlock = content.Substring(modalStart, modalEnd - modalStart);

        Assert.DoesNotContain("agarcía", modalBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana@example.com", modalBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("García", modalBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Ana<", modalBlock, StringComparison.OrdinalIgnoreCase);
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
}
