using System.Net;
using System.Web;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests del PageModel de <c>/organizacion/ocupaciones</c> para Slice 2:
/// renderizado inicial con paginación server-side, toggle
/// vigentes/historial, búsqueda, filtros contextuales y feedback de
/// transporte. Espejo de <c>PuestoIndexPageTests</c>.
/// </summary>
[Collection("WebIntegration")]
public sealed class OcupacionIndexPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public OcupacionIndexPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private async Task<WebClientLease> CreateLeaseAsync(
        FakeOcupacionApiClient apiClient, bool adminRole = false)
    {
        return await _fixture.CreateOcupacionLeaseAsync(apiClient, adminRole);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-LST-002 / Scenario: carga inicial
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActiveOcupacionesTable()
    {
        var first = FakeOcupacionApiClient.BuildDto(personaNombre: "Juan", puestoNombre: "Analista");
        var second = FakeOcupacionApiClient.BuildDto(personaNombre: "Ana", puestoNombre: "Vendedor");
        var apiClient = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>(
                [first, second], TotalCount: 2, Page: 1, PageSize: 20)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ocupaciones", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Listado de ocupaciones activas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Juan", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analista", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vendedor", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.ListarCalls);
        Assert.Equal(OcupacionSegmentoListado.Activas, query.Segmento);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Null(query.Search);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-LST-006 / Scenario: Vigente admin → Ver + Editar + Eliminar
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenActiveRowAndAdmin_RendersActionLinks()
    {
        var vigente = FakeOcupacionApiClient.BuildDto(
            personaNombre: "Vigente",
            estado: OcupacionEstado.Vigente);
        var apiClient = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>(
                [vigente], TotalCount: 1, Page: 1, PageSize: 20)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Spec REQ-OCC-LST-006: Ver a todo autenticado; Editar/Eliminar
        // solo Administrador y solo en fila Vigente.
        Assert.Contains($"href=\"/organizacion/ocupaciones/detalles/{vigente.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"href=\"/organizacion/ocupaciones/editar/{vigente.Id}", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenDeletedRowAndAdmin_RendersReactivateNotEdit()
    {
        var eliminada = FakeOcupacionApiClient.BuildDto(
            personaNombre: "Eliminada",
            estado: OcupacionEstado.Eliminada);
        var apiClient = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>(
                [eliminada], TotalCount: 1, Page: 1, PageSize: 20),
            ListarHandler = q => q.Segmento == OcupacionSegmentoListado.Eliminadas
                ? new PagedResult<OcupacionDto>([eliminada], 1, q.Page, q.PageSize)
                : new PagedResult<OcupacionDto>([], 0, q.Page, q.PageSize)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // REQ-OCC-LST-006: en Eliminadas se muestra Ver + Reactivar, NO Editar ni Eliminar.
        Assert.DoesNotContain($"href=\"/organizacion/ocupaciones/editar/{eliminada.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-ocupacion-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("formaction=\"?handler=Reactivate\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenNonAdmin_HidesAdminActions()
    {
        var row = FakeOcupacionApiClient.BuildDto(personaNombre: "SoloLectura");
        var apiClient = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([row], 1, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: false);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"href=\"/organizacion/ocupaciones/detalles/{row.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear ocupación", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"href=\"/organizacion/ocupaciones/editar/{row.Id}", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-LST-002 / Scenario: filtros aplicados (search + sort)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WithSearchAndSort_ForwardsQueryAndRendersItems()
    {
        var match = FakeOcupacionApiClient.BuildDto(personaNombre: "Analista Senior");
        var other = FakeOcupacionApiClient.BuildDto(personaNombre: "Vendedor");
        var apiClient = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([match], 1, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/organizacion/ocupaciones?search=analista&sort=persona_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(match.PersonaNombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(other.PersonaNombre, content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.ListarCalls);
        Assert.Equal("analista", query.Search);
        Assert.Equal("persona_asc", query.Sort);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-LST-002 / Scenario: sin coincidencias
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenListIsEmpty_ShowsEmptyState()
    {
        var apiClient = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron ocupaciones", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-LST-004 / Scenario: transporte recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenApiFails_ShowsVisibleError()
    {
        var apiClient = new FakeOcupacionApiClient
        {
            ListarException = new HttpRequestException("boom")
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(apiClient.ListarCalls);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-LST-003 / Scenario: cambio a historial preservando búsqueda
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_ToggleEliminadas_RendersActivasLinkPreservingFilters()
    {
        var apiClient = new FakeOcupacionApiClient
        {
            ListarResult = new PagedResult<OcupacionDto>([], 0, 1, 20)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: false);

        var response = await lease.Client.GetAsync(
            "/organizacion/ocupaciones?search=ana&sort=persona_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El toggle expone ambos botones; el click debe preservar search/sort
        // y forzar status=eliminadas con p=1.
        Assert.Contains(">Activas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Eliminadas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=eliminadas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=persona_asc", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_StatusEliminadas_QueriesDeletedSegment()
    {
        var eliminada = FakeOcupacionApiClient.BuildDto(
            personaNombre: "Eliminada",
            estado: OcupacionEstado.Eliminada);
        var apiClient = new FakeOcupacionApiClient
        {
            ListarHandler = q => q.Segmento == OcupacionSegmentoListado.Eliminadas
                ? new PagedResult<OcupacionDto>([eliminada], 1, q.Page, q.PageSize)
                : new PagedResult<OcupacionDto>([], 0, q.Page, q.PageSize)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(eliminada.PersonaNombre, content, StringComparison.OrdinalIgnoreCase);
        var query = Assert.Single(apiClient.ListarCalls);
        Assert.Equal(OcupacionSegmentoListado.Eliminadas, query.Segmento);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-LST-002 / Scenario: paginación server-side con TotalCount
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WithMultiplePages_RendersPaginationControls()
    {
        var apiClient = new FakeOcupacionApiClient
        {
            ListarHandler = q => new PagedResult<OcupacionDto>(
                Items: [FakeOcupacionApiClient.BuildDto(personaNombre: "Pag2")],
                TotalCount: 21,
                Page: q.Page,
                PageSize: q.PageSize)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/organizacion/ocupaciones?p=2&search=ana&sort=persona_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Página 2 de 2", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Primera</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Anterior</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Siguiente</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Última</a>", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.ListarCalls);
        Assert.Equal(2, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    // ──────────────────────────────────────────────────
    // Anónimo redirige a sign-in
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}