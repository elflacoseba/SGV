using System.Net;
using System.Web;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests de la página cruzada <c>/personas/{id:guid}/ocupaciones</c> del
/// change #208 / Slice 3b (REQ-OCC-NAV-001..006). Cubre:
/// <list type="bullet">
///   <item>Render con ocupaciones vigentes filtradas por <c>personaId</c>.</item>
///   <item>Ausencia del toggle Activas/Eliminadas (REQ-OCC-NAV-004).</item>
///   <item>Estado vacío cuando la persona no tiene ocupaciones.</item>
///   <item>NotFound recuperable cuando la persona no existe o está inactiva.</item>
///   <item>Botón "Nueva" gated por rol Administrador con <c>?personaId=</c>.</item>
///   <item>Botón "Volver" hacia el detalle de la persona dueña.</item>
///   <item>Ignora <c>?status=eliminadas</c> y conserva el segmento Activas.</item>
///   <item>Fallo de transporte: estado recuperable sin stack trace.</item>
/// </list>
/// Usa <see cref="SgvWebApplicationFactory"/> + <see cref="FakePersonaApiClient"/>
/// + <see cref="FakeOcupacionApiClient"/> para no requerir MySQL.
/// </summary>
[Collection("WebIntegration")]
public sealed class PersonaOcupacionesPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonaOcupacionesPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private async Task<WebClientLease> CreateLeaseAsync(
        FakePersonaApiClient persona, FakeOcupacionApiClient ocupacion, bool adminRole = false)
        => await _fixture.CreatePersonaOcupacionesLeaseAsync(persona, ocupacion, adminRole);

    private static PersonaDto BuildPersona(
        Guid id,
        string nombres = "Ana",
        string apellidos = "García",
        bool isActive = true,
        string? legajo = "L-001")
        => new(
            id, legajo, nombres, apellidos, "ana@example.com",
            null, "DNI", "Documento", "30123456", null, isActive);

    private static OcupacionDto BuildOcupacion(
        Guid personaId,
        string puestoNombre = "Analista",
        Guid? puestoId = null)
        => FakeOcupacionApiClient.BuildDto(
            personaId: personaId,
            personaNombre: "Ana García",
            puestoId: puestoId,
            puestoNombre: puestoNombre);

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-001 / Scenario: Persona con ocupaciones
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePersonaWithOcupaciones_RendersTableForThatPersona()
    {
        var personaId = Guid.NewGuid();
        var persona = BuildPersona(personaId);
        var row = BuildOcupacion(personaId);
        var otherPersonaRow = BuildOcupacion(Guid.NewGuid(), "Otro puesto");

        var personaApi = FakePersonaApiClient.WithPersonaList(persona);
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([row], 1, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ana García", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analista", content, StringComparison.OrdinalIgnoreCase);
        // Filtro server-side: la query lleva el personaId y Segmento=Activas.
        var query = Assert.Single(ocupacionApi.ListarCalls);
        Assert.Equal(personaId, query.PersonaId);
        Assert.Equal(OcupacionSegmentoListado.Activas, query.Segmento);

        // La página NO debe invocar GetByIdAsync contra el cliente de ocupaciones;
        // la verificación de persona activa se delega a IPersonaApiClient.
        Assert.Empty(ocupacionApi.ObtenerPorIdCalls);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-004 / Scenario: HTML sin toggle
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePersona_HtmlDoesNotRenderToggleEliminadas()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // La página cruzada fija Segmento=Activas. No debe existir ningún control
        // de toggle (links "Eliminadas" o "Historial", botones de segmento).
        Assert.DoesNotContain(">Eliminadas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Historial</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status=eliminadas", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-004 / Scenario: Status inyectado se ignora
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_StatusEliminadasQueryString_StillUsesActivasSegment()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarHandler = q => q.Segmento == OcupacionSegmentoListado.Activas
                ? new PagedResult<OcupacionDto>([BuildOcupacion(personaId)], 1, q.Page, q.PageSize)
                : new PagedResult<OcupacionDto>([], 0, q.Page, q.PageSize)
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync(
            $"/personas/{personaId:D}/ocupaciones?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(ocupacionApi.ListarCalls);
        Assert.Equal(OcupacionSegmentoListado.Activas, query.Segmento);
        Assert.Contains("Ana García", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-001 / Scenario: Persona sin ocupaciones
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePersonaWithoutOcupaciones_RendersEmptyState()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no tiene ocupaciones", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-001 / Scenario: Persona inexistente (no está en la lista)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_NonExistentPersona_ReturnsNotFoundWithoutInvokingOcupaciones()
    {
        var personaApi = FakePersonaApiClient.WithPersonaList();
        var ocupacionApi = new FakeOcupacionApiClient();

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/personas/{Guid.NewGuid():D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El handler detecta persona inexistente y NO debe invocar al cliente
        // de ocupaciones: el listado contextual no aplica sin persona dueña.
        Assert.Empty(ocupacionApi.ListarCalls);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-001 / Scenario: Persona inactiva → 404 recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_InactivePersona_ReturnsNotFoundWithoutInvokingOcupaciones()
    {
        var personaId = Guid.NewGuid();
        var inactivePersona = BuildPersona(personaId, isActive: false);
        var personaApi = FakePersonaApiClient.WithPersonaList(inactivePersona);
        var ocupacionApi = new FakeOcupacionApiClient();

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(ocupacionApi.ListarCalls);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-006 / Scenario: Botón "Nueva" gated admin
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_NonAdmin_DoesNotRenderNewButton()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi, adminRole: false);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"href=\"/organizacion/ocupaciones/crear?personaId={personaId:D}",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Admin_RendersNewButtonWithPersonaIdQuery()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi, adminRole: true);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // REQ-OCC-NAV-006: el alta contextual pre-carga PersonaId.
        Assert.Contains("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/ocupaciones/crear?personaId={personaId:D}",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-005 / Scenario: Volver al detalle dueño
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePersona_RendersBackLinkToPersonaDetails()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Volver", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-001 / Scenario: Fallo de transporte recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ListarAsyncTransportFailure_ShowsRecoverableError()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarException = new HttpRequestException("network down")
        };

        await using var lease = await CreateLeaseAsync(personaApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/personas/{personaId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo", content, StringComparison.OrdinalIgnoreCase);
        // Sin stack traces ni tipo de excepción en el HTML.
        Assert.DoesNotContain("HttpRequestException", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Spec: anónimo redirige a sign-in
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Anonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync($"/personas/{Guid.NewGuid():D}/ocupaciones");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/auth/sign-in",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-003 / Enlace desde Persona/Details cuando activa
    // (mirror del test de integración de la página origen)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_PersonaDetails_WhenActive_RendersLinkToOcupaciones()
    {
        var personaId = Guid.NewGuid();
        var personaApi = FakePersonaApiClient.WithPersonaList(BuildPersona(personaId));

        await using var lease = await _fixture.CreatePersonaLeaseAsync(personaApi, adminRole: false);

        var response = await lease.Client.GetAsync($"/personas/detalle/{personaId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ver ocupaciones", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/personas/{personaId:D}/ocupaciones",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_PersonaDetails_WhenInactive_DoesNotRenderLinkToOcupaciones()
    {
        var personaId = Guid.NewGuid();
        var inactive = BuildPersona(personaId, isActive: false);
        var personaApi = FakePersonaApiClient.WithPersonaList(inactive);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(personaApi, adminRole: false);

        var response = await lease.Client.GetAsync($"/personas/detalle/{personaId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            $"href=\"/personas/{personaId:D}/ocupaciones",
            content,
            StringComparison.OrdinalIgnoreCase);
    }
}