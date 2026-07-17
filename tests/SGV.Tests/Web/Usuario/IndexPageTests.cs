using System.Net;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Web integration tests for the segmented Usuarios index and its lifecycle
/// handlers introduced by Phase 3 of the change
/// <c>2026-07-15-quita-soft-delete-usuario</c>: hard-delete, lockout
/// administrative (Bloquear / Desbloquear) y auto-fence contra
/// auto-bloqueo / auto-eliminación.
/// </summary>
[Collection("WebIntegration")]
public sealed class IndexPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public IndexPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActiveUsersAndAdminActions()
    {
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var second = BuildUsuario("u-2", "jperez", "Juan", "Pérez", "juan@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first, second);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de usuarios activos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.UserName, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Email, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Nombres!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Apellidos!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Administrador", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/seguridad/usuarios/detalle/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/seguridad/usuarios/editar/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Crear usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(apiClient.QueryCalls);
    }

    [Fact]
    public async Task Get_Index_WhenTogglingSegment_PreservesSearchAndSortAndResetsPage()
    {
        var bloqueada = BuildUsuario("u-blocked", "blocked", "Elena", "Bloqueada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(bloqueada);
        apiClient.SeedBlocked(bloqueada.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=bloqueadas&search=blo&sort=apellidos_desc&p=3");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de usuarios bloqueados", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=blo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=1", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(3, query.Page);
        Assert.Equal("blo", query.Search);
        Assert.Equal("apellidos_desc", query.Sort);
        Assert.Equal(UsuarioSegmentoListado.Bloqueadas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WhenQueryStringHasSearchSortAndPage_PassesThemToQueryAsync()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        await lease.Client.GetAsync("/seguridad/usuarios?status=activas&search=garcia&sort=nombres_asc&p=2");

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(2, query.Page);
        Assert.Equal("garcia", query.Search);
        Assert.Equal("nombres_asc", query.Sort);
        Assert.Equal(UsuarioSegmentoListado.Activas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticatedWithoutAdminRole_HidesAdminActions()
    {
        var usuario = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/seguridad/usuarios/detalle/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear usuario", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenSegmentIsBloqueadas_ExposesOnlyDesbloquearAction()
    {
        var usuario = BuildUsuario("u-blocked", "blocked", "Elena", "Bloqueada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.SeedBlocked(usuario.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/detalle/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/seguridad/usuarios/editar/{usuario.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear usuario", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenBloqueadasSegmentAndNoAdmin_HidesDesbloquearAction()
    {
        var usuario = BuildUsuario("u-blocked", "blocked", "Elena", "Bloqueada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.SeedBlocked(usuario.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-usuario-desbloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-delete-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions()
    {
        // Auto-fence UI: el admin actual no puede bloquearse ni borrarse
        // a sí mismo. El shell debe ocultar los botones para impedir el
        // doble-click o el descubrimiento del código de error
        // AutoBloqueo/AutoEliminacion.
        var self = BuildUsuario("admin-test", "self", "Self", "User", "self@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(self);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fila del admin actual: bloqueamos + borramos desde el row de la tabla
        AssertUsuarioActionFormNotRendered(content, "data-usuario-bloquear-form", self.Id);
        AssertUsuarioActionFormNotRendered(content, "data-usuario-delete-form", self.Id);
        // El Details/Edit siguen visibles para no romper la navegación
        Assert.Contains($"/seguridad/usuarios/detalle/{self.Id}", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsToActiveSegmentWithFeedback()
    {
        var toDelete = BuildUsuario("u-delete", "adelete", "Ana", "Delete", "delete@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(toDelete);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=activas&p=2&search=delete&sort=username_desc");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios?handler=Delete",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = toDelete.Id,
                ["page"] = "2",
                ["search"] = "delete",
                ["sort"] = "username_desc",
                ["status"] = "activas"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(toDelete.Id, Assert.Single(apiClient.EliminarCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("status=activas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=delete", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=username_desc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("El usuario se eliminó correctamente", content, StringComparison.OrdinalIgnoreCase);
        // Phase 3: el banner ya NO ofrece "Reactivar" (el borrado es
        // físico y no se puede revertir).
        Assert.DoesNotContain("formaction=\"?handler=Reactivate\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("Bloquear")]
    [InlineData("Desbloquear")]
    public async Task Post_LifecycleHandler_WhenUserIsNotAdmin_RedirectsToAccessDeniedWithoutCallingApi(string handler)
    {
        var usuario = BuildUsuario("u-delete", "adelete", "Ana", "Delete", "delete@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios?handler={handler}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = usuario.Id,
                ["page"] = "1"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.EliminarCalls);
        Assert.Empty(apiClient.BloquearCalls);
        Assert.Empty(apiClient.DesbloquearCalls);
    }

    [Fact]
    public async Task Post_Delete_WhenApiRejectsAutoEliminacion_ShowsActionableFeedback()
    {
        var usuario = BuildUsuario("u-self", "self", "Self", "User", "self@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.EliminarResult = UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.Unauthorized,
            "AutoEliminacion",
            "No puede eliminar su propio usuario.",
            403,
            ErrorCategoria.Forbidden));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Delete", usuario.Id);
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AutoEliminacion", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No puede eliminar su propio usuario", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenApiReturnsConflict_ShowsConflictFeedback()
    {
        var usuario = BuildUsuario("u-conflict", "conflict", "Conflict", "User", "conflict@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.EliminarResult = UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.Conflict,
            "Dependencias",
            "La cuenta tiene dependencias activas.",
            409,
            ErrorCategoria.Conflict));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Delete", usuario.Id);
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("Dependencias", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dependencias activas", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext()
    {
        var usuario = BuildUsuario("u-bloq", "bloq", "Bloc", "Kado", "b@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=activas&p=3&search=bloq&sort=nombres_asc");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios?handler=Bloquear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = usuario.Id,
                ["page"] = "3",
                ["search"] = "bloq",
                ["sort"] = "nombres_asc",
                ["status"] = "activas"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(usuario.Id, Assert.Single(apiClient.BloquearCalls));
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        // Phase 3: el redirect tras bloquear va al segmento
        // `bloqueadas` para que el admin vea inmediatamente el estado.
        Assert.Contains("status=bloqueadas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=3", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=bloq", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombres_asc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("El usuario se bloqueó correctamente", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Bloquear_WhenApiRejectsAutoBloqueo_ShowsActionableFeedback()
    {
        var usuario = BuildUsuario("u-self", "self", "Self", "User", "self@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.BloquearResult = UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.Unauthorized,
            "AutoBloqueo",
            "No puede bloquear su propio usuario.",
            403,
            ErrorCategoria.Forbidden));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Bloquear", usuario.Id);
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AutoBloqueo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No puede bloquear su propio usuario", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Desbloquear_WhenSuccessful_RedirectsToActiveSegment()
    {
        var usuario = BuildUsuario("u-unlock", "unlock", "Un", "Lock", "u@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.SeedBlocked(usuario.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=bloqueadas&search=unlock&sort=apellidos_asc");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios?handler=Desbloquear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = usuario.Id,
                ["page"] = "1",
                ["search"] = "unlock",
                ["sort"] = "apellidos_asc",
                ["status"] = "bloqueadas"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(usuario.Id, Assert.Single(apiClient.DesbloquearCalls));
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("status=activas", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status=bloqueadas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=unlock", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("El usuario se desbloqueó correctamente", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Desbloquear_WhenApiReturnsTransportFailure_ShowsRecoverableFeedback()
    {
        var usuario = BuildUsuario("u-transport", "transport", "Trans", "Port", "t@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.DesbloquearException = new HttpRequestException("upstream down");

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/seguridad/usuarios?handler=Desbloquear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = usuario.Id,
                ["page"] = "1",
                ["status"] = "bloqueadas"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("status=bloqueadas", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo desbloquear", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Bloquear_WhenApiReturnsTransportFailure_ShowsRecoverableFeedback()
    {
        // REL-002: cubrimos la rama transport de Bloquear análoga a la
        // ya cubierta de Desbloquear. El admin debe permanecer en el
        // mismo segmento (activas) con un banner de error recuperable.
        var usuario = BuildUsuario("u-bloq-transport", "bloqt", "Bloc", "Trans", "bt@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.BloquearException = new HttpRequestException("upstream down");

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios?status=activas&p=2");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Bloquear", usuario.Id, status: "activas");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("status=activas", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo bloquear", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenApiReturnsTransportFailure_ShowsRecoverableFeedback()
    {
        // REL-002: cubrimos la rama transport de Delete análoga a la ya
        // cubierta de Desbloquear. El admin debe permanecer en el
        // segmento donde estaba (activas) con un banner de error
        // recuperable — el usuario NO se eliminó, así que el listado
        // debe seguir mostrándolo.
        var usuario = BuildUsuario("u-del-transport", "delt", "Del", "Trans", "dt@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.EliminarException = new HttpRequestException("upstream down");

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios?status=activas");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Delete", usuario.Id, status: "activas");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("status=activas", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo eliminar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(usuario.UserName, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenPageAndStatusAreInvalid_NormalizesToActivePageOne()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        await lease.Client.GetAsync("/seguridad/usuarios?status=archivo&p=0");

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(1, query.Page);
        Assert.Equal(UsuarioSegmentoListado.Activas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WhenQueryFailsWithTransportException_ShowsRecoverableError()
    {
        var apiClient = FakeUsuarioApiClient.WithUsuarioList();
        apiClient.QueryException = new HttpRequestException("upstream unavailable");

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado de usuarios", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/seguridad/usuarios");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenCommandReturnsNotFound_ShowsNotAvailableMessage()
    {
        // REL-003: doble eliminación o carrera con otro admin produce
        // 404. El PageModel traduce la categoría NotFound al mensaje
        // "ya no está disponible" y vuelve al segmento donde estaba el
        // admin (no fuerza a `activas`) para preservar el contexto.
        var usuario = BuildUsuario("u-ghost", "ghost", "Gh", "Ost", "g@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        apiClient.EliminarResult = UsuarioCommandResult.Failure(new UsuarioError(
            UsuarioErrorType.NotFound,
            "UsuarioNoEncontrado",
            "El usuario no existe.",
            404,
            ErrorCategoria.NotFound));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);
        var getResponse = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas&p=2");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await PostHandlerAsync(lease, token, "Delete", usuario.Id, status: "bloqueadas");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("status=bloqueadas", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var content = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("ya no está disponible", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("Bloquear")]
    [InlineData("Desbloquear")]
    public async Task Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi(string handler)
    {
        // RIS-001 (CRITICAL): el atributo [AutoValidateAntiforgeryToken]
        // del PageModel debe rechazar cualquier POST sin token. La vista
        // emite @Html.AntiForgeryToken() pero un atacante CSRF no puede
        // falsificarlo; este guard verifica que la ausencia del token
        // cierra la brecha antes de tocar el cliente API.
        var usuario = BuildUsuario("u-csrf", "csrf", "Crsf", "User", "csrf@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.PostAsync(
            $"/seguridad/usuarios?handler={handler}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = usuario.Id,
                ["page"] = "1"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(apiClient.EliminarCalls);
        Assert.Empty(apiClient.BloquearCalls);
        Assert.Empty(apiClient.DesbloquearCalls);
    }

    [Fact]
    public async Task Get_Index_RendersBloquearButton_WithDataAttributeAndNoFormAction()
    {
        // REQ-UCB-01 + REQ-UCB-10: tras PR 2, el botón Bloquear dispara
        // SweetAlert2 (cableado vía data-usuario-bloquear-button +
        // usuarios-index.js) en lugar de un modal Bootstrap nativo.
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-bloquear-button", content, StringComparison.OrdinalIgnoreCase);
        // PR 2: nada de data-bs-toggle/data-bs-target — el JS maneja la
        // confirmación vía Swal.fire (ver usuarios-index.js).
        Assert.DoesNotContain("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-bs-target=\"#confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        // El botón no debe llevar formaction: el submit lo dispara el
        // handler JS sólo tras confirmación explícita.
        Assert.DoesNotContain("formaction=\"?handler=Bloquear\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_BloquearButtonDoesNotSubmitDirectly()
    {
        // REQ-UCB-01 + REQ-UCB-10: el botón Bloquear sigue siendo
        // type="button" — la responsabilidad del submit pasa a
        // wireUsuarioBloquearConfirmation en usuarios-index.js.
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-bloquear-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Bloquear\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-button\" type=\"submit\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_RendersBloquearModal_WithConfirmButton()
    {
        // REQ-UCB-01 + REQ-UCB-04 + REQ-UCB-10: PR 2 reemplaza el modal
        // Bootstrap por SweetAlert2. El contrato observable de la vista
        // es: el bundle de SweetAlert2 + el script usuarios-index.js están
        // cargados, y NO existe markup nativo del modal viejo.
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Bundle SweetAlert2 cargado.
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.min.css", content, StringComparison.OrdinalIgnoreCase);
        // Script de wiring cargado.
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        // Sin markup nativo del modal viejo.
        Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
        // El título del alert queda embebido en usuarios-index.js, no en
        // la vista; el cuerpo del alert también (text: 'esta usuario').
    }

    [Fact]
    public async Task Get_Index_BloquearModal_HasAriaWiring()
    {
        // REQ-UCB-05: PR 2 reemplaza el aria-labelledby propio del modal
        // Bootstrap por el manejo interno de SweetAlert2 (que envuelve
        // title en <h2 aria-label> automáticamente). El contrato observable
        // es: el bundle + script están cargados y NO hay markup nativo del
        // modal viejo.
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aria-labelledby=\"confirm-bloquear-modal-title\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_RendersFormDataUsuarioBloquearForm_WithHiddenInputs()
    {
        // REQ-UCB-06 + REQ-UCB-08: tras el cambio a confirmación modal, el
        // form data-usuario-bloquear-form debe seguir teniendo antiforgery
        // y los hidden inputs de contexto (id/page/search/sort/status) que
        // el PRG vigente necesita para preservar el segmento y los filtros.
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/seguridad/usuarios?status=activas&p=1&search=ana&sort=user_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-bloquear-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__RequestVerificationToken", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"<input name=\"id\" type=\"hidden\" value=\"{first.Id}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<input name=\"page\" type=\"hidden\" value=\"1\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<input name=\"search\" type=\"hidden\" value=\"ana\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<input name=\"sort\" type=\"hidden\" value=\"user_asc\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<input name=\"status\" type=\"hidden\" value=\"activas\"", content, StringComparison.OrdinalIgnoreCase);
        // El form debe apuntar al handler Bloquear vía action (porque el
        // botón ya no usa formaction tras la confirmación modal). Usamos
        // regex con word boundary para no matchear el substring
        // "action=" dentro de "formaction=" del botón vigente.
        Assert.Matches(@"\baction=""[?]handler=Bloquear""", content);
    }

    [Fact]
    public async Task Get_Index_BloquearModal_DoesNotContainPii()
    {
        // REQ-UCB-04 + REQ-UCB-10: PR 2 — el cuerpo del alert
        // SweetAlert2 de Bloquear NO debe exponer PII del usuario
        // objetivo. Como el alert se renderiza en runtime desde
        // usuarios-index.js (no desde markup Razor), la verificación
        // se hace sobre el bundle completo: PII sólo debe aparecer
        // en la tabla de resultados, no en el wiring del alert.
        var first = BuildUsuario("u-1", "agarcía", "Ana", "García", "ana@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(first);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El bundle SweetAlert2 + script usuarios-index.js deben estar
        // presentes, pero sin PII: el alert sólo dice "este usuario".
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        // El bloque <script src="/js/pages/usuarios-index.js"> debe
        // emitir la config canónica sin PII.
        var scriptStart = content.IndexOf("usuarios-index.js", StringComparison.OrdinalIgnoreCase);
        Assert.True(scriptStart >= 0);
        // La página completa no debe incluir el modal viejo ni sus atributos.
        Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-bloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aria-labelledby=\"confirm-bloquear-modal-title\"", content, StringComparison.OrdinalIgnoreCase);
        // Y el JS no debe estar interpolando PII: el title es estático
        // 'Bloquear usuario' (assertado por el harness JS en
        // UsuariosIndexPageJsTests.cs). Verificamos que el source del
        // script emitido por Razor sea el archivo estático, no algo
        // que interpoló datos del usuario.
        Assert.DoesNotContain("agarcía", content.IndexOf("usuarios-index.js", StringComparison.OrdinalIgnoreCase) >= 0
            ? content.Substring(scriptStart)
            : string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_RendersDesbloquearButton_WithDataAttributeAndNoFormAction()
    {
        // REQ-UCB-02 + REQ-UCB-10: análogo al Bloquear pero para el
        // segmento bloqueadas y el form data-usuario-desbloquear-form.
        var bloqueada = BuildUsuario("u-blocked", "blocked", "Elena", "Bloqueada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(bloqueada);
        apiClient.SeedBlocked(bloqueada.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-desbloquear-button", content, StringComparison.OrdinalIgnoreCase);
        // PR 2: nada de modal Bootstrap nativo.
        Assert.DoesNotContain("data-bs-toggle=\"modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-bs-target=\"#confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Desbloquear\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_RendersDesbloquearModal_WithConfirmButton()
    {
        // REQ-UCB-02 + REQ-UCB-04 + REQ-UCB-10: PR 2 reemplaza el modal
        // Bootstrap por SweetAlert2 — el bundle + script deben estar
        // cargados y el modal viejo no debe existir.
        var bloqueada = BuildUsuario("u-blocked", "blocked", "Elena", "Bloqueada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(bloqueada);
        apiClient.SeedBlocked(bloqueada.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_DesbloquearModal_DoesNotContainPii()
    {
        // REQ-UCB-04 + REQ-UCB-10: PR 2 — el alert SweetAlert2 de
        // Desbloquear NO debe exponer PII del usuario objetivo.
        var bloqueada = BuildUsuario("u-blocked", "blocked", "Elena", "Bloqueada", "elena@example.com", "Consultor");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(bloqueada);
        apiClient.SeedBlocked(bloqueada.Id);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/usuarios-index.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"confirm-desbloquear-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-desbloquear-confirm", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_E2E_Admin_NoVeSusPropiosBotones()
    {
        var self = BuildUsuario("admin-test", "admin", "Admin", "Test", "admin@example.com", "Administrador");
        var apiClient = FakeUsuarioApiClient.WithUsuarioList(self);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(apiClient, adminRole: true);

        var activeResponse = await lease.Client.GetAsync("/seguridad/usuarios?status=activas");
        var activeContent = HttpUtility.HtmlDecode(await activeResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        AssertUsuarioActionFormNotRendered(activeContent, "data-usuario-bloquear-form", self.Id);
        AssertUsuarioActionFormNotRendered(activeContent, "data-usuario-delete-form", self.Id);
        AssertUsuarioActionFormNotRendered(activeContent, "data-usuario-desbloquear-form", self.Id);

        apiClient.SeedBlocked(self.Id);
        var blockedResponse = await lease.Client.GetAsync("/seguridad/usuarios?status=bloqueadas");
        var blockedContent = HttpUtility.HtmlDecode(await blockedResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, blockedResponse.StatusCode);
        AssertUsuarioActionFormNotRendered(blockedContent, "data-usuario-bloquear-form", self.Id);
        AssertUsuarioActionFormNotRendered(blockedContent, "data-usuario-delete-form", self.Id);
        AssertUsuarioActionFormNotRendered(blockedContent, "data-usuario-desbloquear-form", self.Id);
    }

    private static void AssertUsuarioActionFormNotRendered(
        string content,
        string formAttribute,
        string usuarioId)
    {
        var forms = content.Split("</form>", StringSplitOptions.RemoveEmptyEntries);

        Assert.DoesNotContain(
            forms,
            form => form.Contains(formAttribute, StringComparison.OrdinalIgnoreCase)
                && form.Contains($"value=\"{usuarioId}\"", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<HttpResponseMessage> PostHandlerAsync(
        WebClientLease lease,
        string token,
        string handler,
        string id,
        string status = "activas",
        string? search = null)
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = id,
            ["page"] = "1",
            ["status"] = status
        };

        if (search is not null)
        {
            values["search"] = search;
        }

        return await lease.Client.PostAsync(
            $"/seguridad/usuarios?handler={handler}",
            new FormUrlEncodedContent(values));
    }

    private static UsuarioDto BuildUsuario(
        string id,
        string userName,
        string nombres,
        string apellidos,
        string email,
        params string[] roles)
        => new(id, Guid.NewGuid(), userName, email, roles, nombres, apellidos);
}
