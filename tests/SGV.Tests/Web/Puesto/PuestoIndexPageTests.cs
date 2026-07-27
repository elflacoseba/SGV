using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Organizacion;
using Xunit;
using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Tests del módulo web de Puestos para PR2: listado segmentado y paginado,
/// baja lógica confirmada y harness JS de <c>puestos-index.js</c>. Cubre los
/// escenarios de carga activa/eliminada, búsqueda, orden, paginación, feedback
/// 409 y confirmaciones SweetAlert2. Espejo de <c>CargoIndexPageTests</c>.
/// </summary>
[Collection("WebIntegration")]
public sealed class PuestoIndexPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public PuestoIndexPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────────
    // Tarea 2.1.1: render inicial del listado activo con 6 columnas
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActivePuestosTable()
    {
        var first = WebTestBuilders.BuildPuestoDto("P-001", "Analista", "Desc A");
        var second = WebTestBuilders.BuildPuestoDto("P-002", "Líder de Proyecto", null);
        var apiClient = FakePuestosApiClient.WithPuestoList(first, second);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Puestos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Listado de puestos activos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Nombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ventas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vendedor", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Nombre, content, StringComparison.OrdinalIgnoreCase);

        // La fila activa debe ofrecer las acciones Detalle, Editar y Eliminar
        // (espejo de CargoIndexPageTests.cs:50-55 y spec canónico
        // puesto-web-listado-detalle-baja/spec.md:27).
        Assert.Contains(
            $"href=\"/organizacion/puestos/detalles/{first.Id}",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/puestos/editar/{first.Id}",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-bs-title=\"Editar\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-puesto-delete-form", content, StringComparison.OrdinalIgnoreCase);

        // spec: "cada fila MUST ofrecer Detalle, Editar y Eliminar"
        // La segunda fila también debe exponer Editar y Detalle.
        Assert.Contains(
            $"href=\"/organizacion/puestos/editar/{second.Id}",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/organizacion/puestos/detalles/{second.Id}",
            content,
            StringComparison.OrdinalIgnoreCase);

        // El endpoint segmentado debe haber sido consultado una vez, sin usar
        // el listado legado.
        Assert.Empty(apiClient.GetAllCalls);
        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(PuestoSegmentoListado.Activas, query.Segmento);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    // ──────────────────────────────────────────────────
    // Tarea 3.2: la vista status=eliminadas NO debe exponer botón Editar.
    // Espejo de la rama !Model.IsDeletedView del Index.cshtml.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenDeletedView_DoesNotRenderEditButton()
    {
        var deleted = WebTestBuilders.BuildPuestoDto("P-001", "Analista eliminada", null);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.QueryHandler = query => query.Segmento == PuestoSegmentoListado.Eliminadas
            ? new PagedResult<PuestoDto>([deleted], 1, query.Page, query.PageSize)
            : new PagedResult<PuestoDto>([], 0, query.Page, query.PageSize);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // En vista Eliminadas se expone el botón Reactivar pero NO Editar
        // (espejo del comportamiento backend: solo se pueden editar puestos
        // activos; reactivá primero si querés editar uno eliminado).
        Assert.DoesNotContain("data-bs-title=\"Editar\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-puesto-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("formaction=\"?handler=Reactivate\"", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(PuestoSegmentoListado.Eliminadas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticatedWithoutAdminRole_HidesAdminActions()
    {
        var first = WebTestBuilders.BuildPuestoDto("P-001", "Analista", "Desc A");
        var apiClient = FakePuestosApiClient.WithPuestoList(first);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"href=\"/organizacion/puestos/detalles/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear puesto", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"href=\"/organizacion/puestos/editar/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-puesto-delete-form", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenDeletedViewAndAuthenticatedWithoutAdminRole_HidesReactivateAction()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList(
            WebTestBuilders.BuildPuestoDto("P-001", "Analista", null));

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Crear puesto", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-puesto-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Reactivate\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.2: Puesto superior renderiza link con contexto preservado
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenPuestoHasSuperior_RendersLinkPreservingContext()
    {
        var superior = WebTestBuilders.BuildPuestoDto("SUP-01", "Superior", null);
        var child = WebTestBuilders.BuildPuestoDto("CHD-01", "Dependiente", null, superior.Id);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.QueryHandler = query => new PagedResult<PuestoDto>(
            query.Segmento == PuestoSegmentoListado.Eliminadas ? [superior, child] : [],
            query.Segmento == PuestoSegmentoListado.Eliminadas ? 2 : 0,
            query.Page,
            query.PageSize);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        // status=eliminadas fuerza Segmento="eliminadas" (no-default) y
        // permite verificar que el link preserva contexto via returnStatus.
        // PR 2: el toggle Eliminadas está deshabilitado pero la query sigue
        // siendo válida (forward-compat con puestos-filtro-activos-eliminados).
        var response = await lease.Client.GetAsync($"/organizacion/puestos?search=dep&sort=nombre_asc&status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // La celda "Puesto superior" de la fila dependiente debe contener
        // un <a> al detalle del superior, con p/search/sort preservados vía
        // returnStatus (espejo del patrón de CargoIndex).
        Assert.Contains(
            $"href=\"/organizacion/puestos/detalles/{superior.Id}",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returnStatus=eliminadas", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.3: toggle Eliminadas activo con preservación de contexto
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_ToggleEliminadas_RendersActiveLinkPreservingFilters()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList(
            WebTestBuilders.BuildPuestoDto("P-001", "Analista", null));

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos?search=ana&sort=nombre_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(">Activas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Eliminadas</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=eliminadas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_asc", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Próximamente", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"btn btn-sm btn-primary disabled\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.4: listado sin resultados (lista vacía desde el backend)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenListIsEmpty_ShowsEmptyState()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList();

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron puestos", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-puesto-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.GetAllCalls);
        Assert.NotEmpty(apiClient.QueryCalls);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.5: búsqueda sin resultados
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenSearchHasNoResults_ShowsEmptyState()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList(
            WebTestBuilders.BuildPuestoDto("P-001", "Analista", null));

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos?search=zzzzz");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron puestos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"zzzzz\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.GetAllCalls);
        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal("zzzzz", query.Search);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.6: error visible cuando falla la carga inicial
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenApiFails_ShowsVisibleError()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.QueryException = new HttpRequestException("boom");

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.GetAllCalls);
        Assert.NotEmpty(apiClient.QueryCalls);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.7: POST Delete éxito → PRG preservando filtros + LastDeletedId
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndKeepsLastDeletedId()
    {
        var toDelete = WebTestBuilders.BuildPuestoDto("DEL-01", "Analista Senior", "Desc", null);
        var remaining = WebTestBuilders.BuildPuestoDto("DEL-02", "Analista Junior", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(toDelete, remaining);
        apiClient.DeleteResult = new PuestoDeleteResult(true, HttpStatusCode.NoContent, null, null);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos?p=1&search=ana&sort=nombre_desc");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = toDelete.Id.ToString(),
            ["page"] = "1",
            ["search"] = "ana",
            ["sort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(toDelete.Id, Assert.Single(apiClient.DeleteCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("search=ana", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"deletedId={toDelete.Id}", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);

        // El banner debe ofrecer reactivar con el id persistido
        // vía TempData["LastDeletedId"] → query string deletedId.
        Assert.Contains("formaction=\"?handler=Reactivate\"", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{toDelete.Id}\"", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.8: POST Delete 409 → feedback + fila visible
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible()
    {
        var puesto = WebTestBuilders.BuildPuestoDto("CONF-01", "Con Conflicto", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(puesto);
        apiClient.DeleteResult = new PuestoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.Conflict,
            Code: "PuestoConOcupacionesActivas",
            Message: "El puesto tiene ocupaciones vigentes y no puede darse de baja.",
            Categoria: ErrorCategoria.Conflict);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos?search=conf&sort=codigo_asc");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1",
            ["search"] = "conf",
            ["sort"] = "codigo_asc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("search=conf", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=codigo_asc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo eliminar el puesto", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("El puesto tiene ocupaciones vigentes y no puede darse de baja.", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PuestoConOcupacionesActivas", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(puesto.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.9: POST Delete 404 → feedback recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenNotFound_ShowsFeedbackAndKeepsRowVisible()
    {
        var puesto = WebTestBuilders.BuildPuestoDto("NF-01", "A Borrar", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(puesto);
        apiClient.DeleteResult = new PuestoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.NotFound,
            Code: "PuestoNoEncontrado",
            Message: "El puesto no existe.");

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("ya no está disponible", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(puesto.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        var puesto = WebTestBuilders.BuildPuestoDto("DENY-DEL", "Sin permisos", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(puesto);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.DeleteCalls);
    }

    [Fact]
    public async Task Post_Reactivate_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        var puesto = WebTestBuilders.BuildPuestoDto("DENY-REACT", "Sin permisos", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(puesto);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.ReactivateCalls);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.10: POST Reactivate éxito → Activas + LastDeletedId limpio
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Reactivate_WhenSuccessful_RedirectsToActivasClearsLastDeletedId()
    {
        var puesto = WebTestBuilders.BuildPuestoDto("REACT-01", "A Reactivar", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.ReactivateResult = PuestoCommandResult.Success(
            new PuestoDto(puesto.Id, puesto.Codigo, puesto.Nombre, puesto.Descripcion,
                WebTestBuilders.SampleUnidadOrganizativaId, "Ventas",
                WebTestBuilders.SampleCargoId, "Vendedor", null));

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos?status=eliminadas&search=react&sort=nombre_asc&deletedId=" + puesto.Id);
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1",
            ["search"] = "react",
            ["sort"] = "nombre_asc",
            ["status"] = "eliminadas"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(puesto.Id, Assert.Single(apiClient.ReactivateCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.DoesNotContain("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=react", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_asc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se reactivó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.11: POST Reactivate 409 (código duplicado) → permanece en origen
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Reactivate_WhenConflictByCodigo_ShowsFeedbackAndKeepsContext()
    {
        var puesto = WebTestBuilders.BuildPuestoDto("CONF-REACT-01", "Conflicto React", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.ReactivateResult = PuestoCommandResult.Failure(
            new PuestoError(PuestoErrorType.Conflict, "CodigoDuplicado",
                "Ya existe un puesto activo con el mismo código."));

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos?status=eliminadas");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1",
            ["status"] = "eliminadas"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(puesto.Id, Assert.Single(apiClient.ReactivateCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await lease.Client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo reactivar el puesto", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CodigoDuplicado", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.12: preservación de contexto tras Delete (status=eliminadas)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_StatusEliminadas_QueriesDeletedSegment()
    {
        var deleted = WebTestBuilders.BuildPuestoDto("P-001", "Analista eliminada", null);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.QueryHandler = query => query.Segmento == PuestoSegmentoListado.Eliminadas
            ? new PagedResult<PuestoDto>([deleted], 1, query.Page, query.PageSize)
            : new PagedResult<PuestoDto>([], 0, query.Page, query.PageSize);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync("/organizacion/puestos?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(PuestoSegmentoListado.Eliminadas, query.Segmento);
        Assert.Contains(deleted.Nombre, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WithSearchSortAndPage_PreservesQueryContextAndRendersPagination()
    {
        var visible = WebTestBuilders.BuildPuestoDto("P-021", "Analista página 2", null);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.QueryHandler = query => new PagedResult<PuestoDto>(
            [visible],
            TotalCount: 21,
            Page: query.Page,
            PageSize: query.PageSize);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync(
            "/organizacion/puestos?p=2&search=ana&sort=nombre_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(visible.Nombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Página 2 de 2", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Primera</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Anterior</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Siguiente</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Última</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=1&search=ana&sort=nombre_asc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=2&search=ana&sort=nombre_asc", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(2, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal("ana", query.Search);
        Assert.Equal("nombre_asc", query.Sort);
        Assert.Equal(PuestoSegmentoListado.Activas, query.Segmento);
    }

    [Fact]
    public async Task Get_Index_WithSearch_ReturnsOnlyMatchingServerSideItems()
    {
        var matching = WebTestBuilders.BuildPuestoDto("P-001", "Analista", null);
        var other = WebTestBuilders.BuildPuestoDto("P-002", "Contador", null);
        var apiClient = FakePuestosApiClient.WithPuestoList(matching, other);

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync(
            "/organizacion/puestos?search=analista&sort=nombre_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(matching.Nombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(other.Nombre, content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal("analista", query.Search);
        Assert.Equal("nombre_asc", query.Sort);
    }


    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/puestos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Harness JS — SweetAlert2 confirmación de Delete
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecutePuestoConfirmationScriptAsync(PuestoConfirmationKind.Delete, isConfirmed: false);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.True(result.ShowCancelButton);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.True(result.ReverseButtons);
    }

    [Fact]
    public async Task DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce()
    {
        var result = await ExecutePuestoConfirmationScriptAsync(PuestoConfirmationKind.Delete, isConfirmed: true);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Sí, eliminar", result.ConfirmButtonText);
        Assert.Equal("¿Eliminar puesto?", result.Title);
    }

    [Fact]
    public async Task ReactivateConfirmationScript_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecutePuestoConfirmationScriptAsync(PuestoConfirmationKind.Reactivate, isConfirmed: false);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.True(result.ShowCancelButton);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.True(result.ReverseButtons);
        Assert.Equal("question", result.Icon);
    }

    [Fact]
    public async Task ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce()
    {
        var result = await ExecutePuestoConfirmationScriptAsync(PuestoConfirmationKind.Reactivate, isConfirmed: true);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Sí, reactivar", result.ConfirmButtonText);
        Assert.Equal("¿Reactivar puesto?", result.Title);
    }

    /// <summary>
    /// Selector del par form/button JS. Extraído como enum para evitar
    /// duplicación entre los 4 tests de harness y mantener el contrato
    /// con los data-attributes declarados en Index.cshtml.
    /// </summary>
    private enum PuestoConfirmationKind
    {
        Delete,
        Reactivate
    }

    /// <summary>
    /// Ejecuta el script JS de Puestos en un subproceso Node y devuelve las
    /// métricas de captura (handler invocado, configuración de Swal.fire
    /// emitida, formulario enviado o no). Helper compartido entre los 4
    /// tests de harness de Delete y Reactivate.
    /// </summary>
    private static async Task<PuestoScriptExecutionResult> ExecutePuestoConfirmationScriptAsync(
        PuestoConfirmationKind kind,
        bool isConfirmed)
    {
        var scriptConfig = kind switch
        {
            PuestoConfirmationKind.Delete => new
            {
                Export = "wirePuestoDeleteConfirmation",
                FormSelector = "[data-puesto-delete-form]",
                ButtonSelector = "[data-puesto-delete-button]",
                ErrorMessage = "Puesto delete confirmation click handler was not wired.",
                HarnessPrefix = "puesto-delete-confirmation",
                ItemName = "Analista"
            },
            PuestoConfirmationKind.Reactivate => new
            {
                Export = "wirePuestoReactivateConfirmation",
                FormSelector = "[data-puesto-reactivate-form]",
                ButtonSelector = "[data-puesto-reactivate-button]",
                ErrorMessage = "Puesto reactivate confirmation click handler was not wired.",
                HarnessPrefix = "puesto-reactivate-confirmation",
                ItemName = "Analista Eliminado"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SGV.Web/wwwroot/js/pages/puestos-index.js"));
        var harnessPath = Path.Combine(Path.GetTempPath(), $"{scriptConfig.HarnessPrefix}-{Guid.NewGuid():N}.cjs");

        var harnessSource = $$"""
const { {{scriptConfig.Export}} } = require({{JsonSerializer.Serialize(scriptPath)}});

let clickHandler = null;
let submitCount = 0;
let preventDefaultCalled = false;
let swalConfig = null;

const button = {
  getAttribute(name) {
    if (name === 'data-puesto-item-name') {
      return {{JsonSerializer.Serialize(scriptConfig.ItemName)}};
    }

    if (name === 'data-puesto-item-code') {
      return 'P-001';
    }

    return null;
  },
  addEventListener(type, handler) {
    if (type === 'click') {
      clickHandler = handler;
    }
  }
};

const form = {
  querySelector(selector) {
    return selector === {{JsonSerializer.Serialize(scriptConfig.ButtonSelector)}} ? button : null;
  },
  submit() {
    submitCount += 1;
  }
};

const root = {
  querySelectorAll(selector) {
    return selector === {{JsonSerializer.Serialize(scriptConfig.FormSelector)}} ? [form] : [];
  }
};

const Swal = {
  fire(config) {
    swalConfig = config;
    return Promise.resolve({ isConfirmed: {{(isConfirmed ? "true" : "false")}} });
  }
};

async function main() {
  {{scriptConfig.Export}}(root, Swal);

  if (!clickHandler) {
    throw new Error({{JsonSerializer.Serialize(scriptConfig.ErrorMessage)}});
  }

  clickHandler({
    preventDefault() {
      preventDefaultCalled = true;
    }
  });

  await Promise.resolve();
  await Promise.resolve();

  process.stdout.write(JSON.stringify({
    submitCount,
    preventDefaultCalled,
    showCancelButton: Boolean(swalConfig && swalConfig.showCancelButton),
    reverseButtons: Boolean(swalConfig && swalConfig.reverseButtons),
    confirmButtonText: swalConfig ? swalConfig.confirmButtonText : null,
    cancelButtonText: swalConfig ? swalConfig.cancelButtonText : null,
    title: swalConfig ? swalConfig.title : null,
    icon: swalConfig ? swalConfig.icon : null
  }));
}

main().catch(error => {
  process.stderr.write(error.stack || String(error));
  process.exit(1);
});
""";

        await File.WriteAllTextAsync(harnessPath, harnessSource);

        try
        {
            var startInfo = new ProcessStartInfo("node", $"\"{harnessPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var standardOutput = await process.StandardOutput.ReadToEndAsync();
            var standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"Node harness failed with exit code {process.ExitCode}: {standardError}");

            var result = JsonSerializer.Deserialize<PuestoScriptExecutionResult>(standardOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            Assert.NotNull(result);
            return result!;
        }
        finally
        {
            if (File.Exists(harnessPath))
            {
                File.Delete(harnessPath);
            }
        }
    }

    private sealed record PuestoScriptExecutionResult(
        int SubmitCount,
        bool PreventDefaultCalled,
        bool ShowCancelButton,
        bool ReverseButtons,
        string? ConfirmButtonText,
        string? CancelButtonText,
        string? Title,
        string? Icon);
}
