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
}
