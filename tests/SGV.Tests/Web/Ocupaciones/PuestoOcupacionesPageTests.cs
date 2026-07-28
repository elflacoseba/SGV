using System.Net;
using System.Web;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Puesto;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests de la página cruzada <c>/organizacion/puestos/{id:guid}/ocupaciones</c>
/// del change #208 / Slice 3b (REQ-OCC-NAV-002..006). Espejo de
/// <see cref="PersonaOcupacionesPageTests"/> con filtro
/// <see cref="OcupacionListQuery.PuestoId"/> en lugar de
/// <see cref="OcupacionListQuery.PersonaId"/>.
/// <list type="bullet">
///   <item>Render con ocupaciones vigentes filtradas por <c>puestoId</c>.</item>
///   <item>Ausencia del toggle Activas/Eliminadas (REQ-OCC-NAV-004).</item>
///   <item>Estado vacío cuando el puesto no tiene ocupaciones.</item>
///   <item>NotFound recuperable cuando el puesto no existe o está inactivo
///         (la API ya devuelve <c>null</c> para puestos inactivos, comportamiento
///         heredado de <see cref="IPuestosApiClient.GetByIdAsync"/>).</item>
///   <item>Botón "Nueva" gated por rol Administrador con <c>?puestoId=</c>.</item>
///   <item>Botón "Volver" hacia el detalle del puesto dueño.</item>
///   <item>Ignora <c>?status=eliminadas</c> y conserva el segmento Activas.</item>
///   <item>Fallo de transporte: estado recuperable sin stack trace.</item>
/// </list>
/// Usa <see cref="SgvWebApplicationFactory"/> + <see cref="FakePuestosApiClient"/>
/// + <see cref="FakeOcupacionApiClient"/> para no requerir MySQL.
/// </summary>
[Collection("WebIntegration")]
public sealed class PuestoOcupacionesPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public PuestoOcupacionesPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private async Task<WebClientLease> CreateLeaseAsync(
        FakePuestosApiClient puestos, FakeOcupacionApiClient ocupacion, bool adminRole = false)
        => await _fixture.CreatePuestoOcupacionesLeaseAsync(puestos, ocupacion, adminRole);

    private static PuestoDto BuildPuesto(
        Guid id,
        string codigo = "P-001",
        string nombre = "Analista Senior",
        Guid? unidadId = null,
        Guid? cargoId = null)
        => new(
            id, codigo, nombre, null,
            unidadId ?? Guid.NewGuid(), "Comercial",
            cargoId ?? Guid.NewGuid(), "Vendedor",
            PuestoSuperiorId: null);

    private static OcupacionDto BuildOcupacion(
        Guid puestoId,
        string personaNombre = "Ana García",
        Guid? personaId = null,
        string puestoNombre = "Analista Senior")
        => FakeOcupacionApiClient.BuildDto(
            personaId: personaId,
            personaNombre: personaNombre,
            puestoId: puestoId,
            puestoNombre: puestoNombre);

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-002 / Scenario: Puesto ocupado
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePuestoWithOcupaciones_RendersTableForThatPuesto()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var row = BuildOcupacion(puestoId);
        var otherPuestoRow = BuildOcupacion(Guid.NewGuid(), "Otro nombre", Guid.NewGuid(), "Otro puesto");

        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([row], 1, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{puestoId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ana García", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analista Senior", content, StringComparison.OrdinalIgnoreCase);
        // Filtro server-side: la query lleva el puestoId y Segmento=Activas.
        var query = Assert.Single(ocupacionApi.ListarCalls);
        Assert.Equal(puestoId, query.PuestoId);
        Assert.Equal(OcupacionSegmentoListado.Activas, query.Segmento);

        // La verificación de puesto activo se delega a IPuestosApiClient.GetByIdAsync;
        // la página cruzada NO invoca ObtenerPorIdAsync del cliente de ocupaciones.
        Assert.Empty(ocupacionApi.ObtenerPorIdCalls);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-004 / Scenario: HTML sin toggle
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePuesto_HtmlDoesNotRenderToggleEliminadas()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{puestoId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarHandler = q => q.Segmento == OcupacionSegmentoListado.Activas
                ? new PagedResult<OcupacionDto>([BuildOcupacion(puestoId)], 1, q.Page, q.PageSize)
                : new PagedResult<OcupacionDto>([], 0, q.Page, q.PageSize)
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi);

        var response = await lease.Client.GetAsync(
            $"/organizacion/puestos/{puestoId:D}/ocupaciones?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(ocupacionApi.ListarCalls);
        Assert.Equal(OcupacionSegmentoListado.Activas, query.Segmento);
        Assert.Contains("Ana García", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-002 / Scenario: Puesto sin ocupación
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePuestoWithoutOcupaciones_RendersEmptyState()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{puestoId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no tiene ocupaciones", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-002 / Scenario: Puesto inexistente (GetByIdAsync devuelve null)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_NonExistentPuesto_ReturnsNotFoundWithoutInvokingOcupaciones()
    {
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = null,
            GetAllResult = []
        };
        var ocupacionApi = new FakeOcupacionApiClient();

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{Guid.NewGuid():D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El handler detecta puesto inexistente y NO debe invocar al cliente
        // de ocupaciones: el listado contextual no aplica sin puesto dueño.
        Assert.Empty(ocupacionApi.ListarCalls);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-006 / Scenario: Botón "Nueva" gated admin
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_NonAdmin_DoesNotRenderNewButton()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi, adminRole: false);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{puestoId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"href=\"/organizacion/ocupaciones/crear?puestoId={puestoId:D}",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Admin_RendersNewButtonWithPuestoIdQuery()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{puestoId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // REQ-OCC-NAV-006: el alta contextual pre-carga PuestoId.
        Assert.Contains("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/ocupaciones/crear?puestoId={puestoId:D}",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-005 / Scenario: Volver al detalle dueño
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ActivePuesto_RendersBackLinkToPuestoDetails()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{puestoId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Volver", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/puestos/detalles/{puestoId:D}",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-002 / Scenario: Fallo de transporte recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_ListarAsyncTransportFailure_ShowsRecoverableError()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };
        var ocupacionApi = new FakeOcupacionApiClient
        {
            ListarException = new HttpRequestException("network down")
        };

        await using var lease = await CreateLeaseAsync(puestosApi, ocupacionApi);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{puestoId:D}/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
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

        var response = await lease.Client.GetAsync($"/organizacion/puestos/{Guid.NewGuid():D}/ocupaciones");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/auth/sign-in",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-NAV-003 / Enlace desde Puesto/Details cuando activo
    // (mirror del test de integración de la página origen)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_PuestoDetails_WhenActive_RendersLinkToOcupaciones()
    {
        var puestoId = Guid.NewGuid();
        var puesto = BuildPuesto(puestoId);
        var puestosApi = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(puestosApi, adminRole: false);

        var response = await lease.Client.GetAsync($"/organizacion/puestos/detalles/{puestoId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ver ocupaciones", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/puestos/{puestoId:D}/ocupaciones",
            content,
            StringComparison.OrdinalIgnoreCase);
    }
}