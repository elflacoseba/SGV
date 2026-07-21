using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests web del módulo Personas para PR 4/4: listado paginado server-side,
/// segmentación activas/eliminadas con preservación de search/sort, role
/// gating, PRG desde los handlers <c>?handler=Delete</c> y
/// <c>?handler=Reactivate</c>, y estados recuperables (404/transporte).
/// Espejo de <c>CargoIndexPageTests</c>.
/// </summary>
[Collection("WebIntegration")]
public sealed class IndexPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public IndexPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: renderizado del listado activo con paginación server-side
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActivePersonasTable()
    {
        var first = BuildPersonaDto("L-001", "Ana", "García", "ana@example.com");
        var second = BuildPersonaDto("L-002", "Juan", "Pérez", null);
        var apiClient = FakePersonaApiClient.WithPersonaList(first, second);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de personas activas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Legajo!, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Apellidos, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Nombres, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Legajo!, content, StringComparison.OrdinalIgnoreCase);

        // Las acciones Detalle, Editar y Eliminar deben aparecer para admin en vista Activas.
        Assert.Contains($"/personas/detalle/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/personas/editar/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-persona-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-persona-delete-button", content, StringComparison.OrdinalIgnoreCase);

        // En vista Activas: NO se exponen acciones de Eliminadas.
        Assert.DoesNotContain("data-persona-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Listado de personas eliminadas", content, StringComparison.OrdinalIgnoreCase);

        // El PageModel debe haber invocado QueryAsync (server-side pagination).
        Assert.NotEmpty(apiClient.QueryCalls);
    }

    // ──────────────────────────────────────────────
    // T-XX 2: toggle Activas/Eliminadas preserva search/sort
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenTogglingSegmento_PreservesSearchAndSortAndResetsPage()
    {
        var eliminada = BuildPersonaDto("L-009", "Eli", "Minada", null);
        eliminada = eliminada with { IsActive = false };
        var apiClient = FakePersonaApiClient.WithPersonaList(eliminada);
        apiClient.QueryHandler = q => q.Segmento == PersonaSegmentoListado.Eliminadas
            ? new PersonaListadoDto([eliminada], 1, q.Page, q.PageSize)
            : new PersonaListadoDto([], 0, q.Page, q.PageSize);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/personas?status=eliminadas&search=min&sort=apellidos_desc&p=3");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de personas eliminadas", content, StringComparison.OrdinalIgnoreCase);

        // El toggle Activas debe preservar search/sort pero resetear p=1.
        Assert.Contains("search=min", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", content, StringComparison.OrdinalIgnoreCase);

        // El query inicial debe llevar el segmento Eliminadas y la página solicitada.
        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(PersonaSegmentoListado.Eliminadas, query.Segmento);
        Assert.Equal("min", query.Search);
        Assert.Equal("apellidos_desc", query.Sort);
    }

    // ──────────────────────────────────────────────
    // T-XX 3: búsqueda + orden + paginación desde query string
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenQueryStringHasSearchSortAndPage_PassesThemToQueryAsync()
    {
        var apiClient = FakePersonaApiClient.WithPersonaList();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        await lease.Client.GetAsync("/personas?search=garcia&sort=nombres_asc&p=2");

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(2, query.Page);
        Assert.Equal("garcia", query.Search);
        Assert.Equal("nombres_asc", query.Sort);
        Assert.Equal(PersonaSegmentoListado.Activas, query.Segmento);
    }

    // ──────────────────────────────────────────────
    // T-XX 4: role gating — CTAs ocultas para no-Administrador
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAuthenticatedWithoutAdminRole_HidesAdminActions()
    {
        var persona = BuildPersonaDto("L-001", "Ana", "García", null);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Detalle sigue visible para cualquier autenticado.
        Assert.Contains($"/personas/detalle/{persona.Id}", content, StringComparison.OrdinalIgnoreCase);

        // Acciones de admin NO visibles.
        Assert.DoesNotContain("Crear persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"/personas/editar/{persona.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-persona-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-persona-delete-button", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 5: ?handler=Delete PRG con feedback
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndShowsFeedback()
    {
        var toDelete = BuildPersonaDto("L-001", "Ana", "García", null);
        var remaining = BuildPersonaDto("L-002", "Juan", "Pérez", null);
        var apiClient = FakePersonaApiClient.WithPersonaList(toDelete, remaining);
        apiClient.DeleteResult = new PersonaDeleteResult(true, HttpStatusCode.NoContent, null, null);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas?p=1&search=ana&sort=apellidos_desc");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = toDelete.Id.ToString(),
            ["page"] = "1",
            ["search"] = "ana",
            ["sort"] = "apellidos_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(toDelete.Id, Assert.Single(apiClient.DeleteCalls));

        // El redirect preserva filtros/sort y propaga el deletedId en query string.
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("search=ana", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_desc", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"deletedId={toDelete.Id}", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 6: ?handler=Reactivate PRG
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Reactivate_WhenSuccessful_RedirectsToActivasWithoutStatusEliminadas()
    {
        var reactivableId = Guid.NewGuid();
        var apiClient = FakePersonaApiClient.WithPersonaList();
        apiClient.ReactivarResult = PersonaCommandResult.Success(
            new PersonaDto(reactivableId, "L-001", "Ana", "García", null, null, null, null, null, null, true));

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas?status=eliminadas&search=react&sort=nombres_asc");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = reactivableId.ToString(),
            ["page"] = "1",
            ["search"] = "react",
            ["sort"] = "nombres_asc",
            ["status"] = "eliminadas"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(reactivableId, Assert.Single(apiClient.ReactivarCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        // Tras éxito: redirige a Activas (sin status=eliminadas).
        Assert.DoesNotContain("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=react", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombres_asc", location, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 7: estado recuperable 404/transporte
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenQueryFailsWithHttpRequestException_ShowsVisibleError()
    {
        // AC: el PageModel captura excepciones de transporte de QueryAsync
        // y muestra un banner recuperable sin propagar 500.
        var apiClient = FakePersonaApiClient.WithPersonaList();
        apiClient.QueryException = new HttpRequestException("boom");

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        // El filtro de búsqueda sigue visible para que el usuario pueda reintentar.
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 8: CTA rápido post-baja (TempData → banner)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_StoresLastDeletedId_PromptsReactivarCtaInBanner()
    {
        // REQ-CW-06: tras una baja lógica exitosa, el banner debe mostrar
        // un botón "Reactivar" que ejecute ?handler=Reactivate con el id
        // del último Persona eliminado. El CTA se persiste vía TempData y
        // sólo se muestra en la vista Activas.
        var toDelete = BuildPersonaDto("L-001", "Ana", "García", null);
        var apiClient = FakePersonaApiClient.WithPersonaList(toDelete);
        apiClient.DeleteResult = new PersonaDeleteResult(true, HttpStatusCode.NoContent, null, null);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var deleteResponse = await lease.Client.PostAsync("/personas?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = toDelete.Id.ToString(),
            ["page"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        var refreshed = await lease.Client.GetAsync(deleteResponse.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
        // El banner contiene el botón Reactivar apuntando a
        // ?handler=Reactivate con el id del Persona eliminado.
        Assert.Contains("formaction=\"?handler=Reactivate\"", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{toDelete.Id}\"", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 9: paginación inválida cae a p=1
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenPageQueryIsZero_NormalizesToPageOne()
    {
        var apiClient = FakePersonaApiClient.WithPersonaList();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        await lease.Client.GetAsync("/personas?p=0");

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(1, query.Page);
    }

    // ──────────────────────────────────────────────
    // T-XX 10: status desconocido cae a activas
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenStatusIsUnknown_FallsBackToActivas()
    {
        // AC: status != "eliminadas" cae a activas (default). El PageModel
        // normaliza el segmento en NormalizeSegmento().
        var apiClient = FakePersonaApiClient.WithPersonaList();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient);

        await lease.Client.GetAsync("/personas?status=archivo");

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(PersonaSegmentoListado.Activas, query.Segmento);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    internal static PersonaDto BuildPersonaDto(string legajo, string nombres, string apellidos, string? email)
        => new(Guid.NewGuid(), legajo, nombres, apellidos, email, null, null, null, null, null, true);
}