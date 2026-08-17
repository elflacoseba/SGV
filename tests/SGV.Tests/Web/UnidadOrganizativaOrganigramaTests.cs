using System.Net;
using System.Web;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web;

public sealed partial class UnidadOrganizativaWebTests
{
    [Fact]
    public async Task Get_Organigrama_WhenTreeHasNodes_RendersHierarchyAndUsesTreeEndpoint()
    {
        var facultyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse(
            [
                new UnidadOrganizativaTreeNodeDto(
                    facultyId,
                    "RECT",
                    "Rectorado",
                    Guid.NewGuid(),
                    "Institución",
                    [
                        new UnidadOrganizativaTreeNodeDto(
                            departmentId,
                            "FI",
                            "Facultad de Ingeniería",
                            Guid.NewGuid(),
                            "Facultad",
                            [])
                    ])
            ],
            []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Organigrama", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"orgchart\"", content, StringComparison.OrdinalIgnoreCase);
        // El árbol se hidrata server-side con el JWT bridged (window.__sgvTreeData),
        // evitando el fetch browser-side que rebotaba con 401. Se valida por
        // identificadores ASCII para no depender del encoding de no-ASCII
        // (los nombres con acentos los serializa el JSON pero la aserción
        // debe ser estable independiente del transporte).
        Assert.Contains("window.__sgvTreeData", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(facultyId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(departmentId.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"codigo\":\"RECT\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"codigo\":\"FI\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.TreeCalls);
        Assert.Empty(apiClient.QueryCalls);
    }

    [Fact]
    public async Task Get_Organigrama_WhenTreeIsEmpty_ShowsEmptyState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse([], []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No hay unidades organizativas para mostrar en el organigrama", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<table", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.TreeCalls);
    }

    [Fact]
    public async Task Get_Organigrama_WhenTreeFails_ShowsVisibleErrorAndFallbackActions()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeException = new HttpRequestException("tree-boom");

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el organigrama", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/organizacion/unidades-organizativas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.TreeCalls);
        Assert.Empty(apiClient.QueryCalls);
    }

    /// <summary>
    /// Issue #277 (WU-8): cuando la API reporta nodos involucrados en
    /// ciclos, la página del organigrama debe renderizar un warning
    /// visible con los IDs de los nodos. El árbol se muestra igual
    /// (sin los nodos cíclicos) pero el usuario sabe que la jerarquía
    /// está corrupta.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_WhenApiReportsCiclicos_ShowsWarningWithIds()
    {
        var cyclicIdA = Guid.Parse("93000000-0000-0000-0000-000000000001");
        var cyclicIdB = Guid.Parse("93000000-0000-0000-0000-000000000002");

        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse(
            [],
            [cyclicIdA, cyclicIdB]);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ciclos detectados", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cyclicIdA.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cyclicIdB.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, apiClient.TreeCalls);
    }

    /// <summary>
    /// WU-8 negative path: cuando la API no reporta ciclos, el warning
    /// NO debe renderizarse (la página sigue mostrando el árbol o el
    /// estado vacío según corresponda).
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_WhenApiReportsNoCiclicos_DoesNotShowWarning()
    {
        var facultyId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse(
            [
                new UnidadOrganizativaTreeNodeDto(
                    facultyId, "RECT", "Rectorado",
                    Guid.NewGuid(), "Institución", [])
            ],
            []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Ciclos detectados", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issue #286: cuando el árbol tiene nodos, la página debe
    /// renderizar la barra de acciones (Exportar PNG, Exportar PDF) y
    /// los dos switches de filtro visual. La presencia del toolbar se
    /// valida por el atributo estable `data-orgchart-toolbar` que
    /// también usa el JS para colgarse de los handlers.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_WhenTreeHasNodes_RendersToolbarAndSwitches()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse(
            [
                new UnidadOrganizativaTreeNodeDto(
                    Guid.NewGuid(), "RECT", "Rectorado",
                    Guid.NewGuid(), "Institución", [])
            ],
            []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Toolbar con las dos acciones de export.
        Assert.Contains("data-orgchart-toolbar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-orgchart-export=\"png\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-orgchart-export=\"pdf\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exportar PNG", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exportar PDF", content, StringComparison.OrdinalIgnoreCase);

        // Switches de filtro visual, ambos arrancan `checked` para
        // preservar el comportamiento actual al cargar la página.
        Assert.Contains("data-orgchart-toggle=\"showCode\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-orgchart-toggle=\"showVigentes\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"toggle-show-code\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"toggle-show-vigentes\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mostrar código", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mostrar unidades vigentes", content, StringComparison.OrdinalIgnoreCase);

        // El contenedor del chart sigue presente y la barra lleva la
        // utility `d-print-none` (Bootstrap) para ocultarse en
        // `window.print()`. Validamos que la utility esté en la misma
        // declaración `class="..."` que la clase propia de la toolbar.
        Assert.Contains("id=\"orgchart\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" orgchart-toolbar d-print-none\"", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issue #286: cuando la API devuelve un árbol vacío, la toolbar
    /// NO debe renderizarse (las acciones de export no tienen sentido
    /// sin nodos) y el fallback a "Volver al listado" sigue presente.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_WhenTreeIsEmpty_OmitsToolbarAndExportActions()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse([], []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-orgchart-toolbar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exportar PNG", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exportar PDF", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"orgchart\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issue #286: cuando falla la carga del árbol, la toolbar y los
    /// switches NO deben renderizarse — exportar un organigrama que no
    /// cargó es exactamente el escenario que la issue prohíbe.
    /// El mensaje de error visible y el botón "Volver al listado"
    /// siguen siendo los affordances correctos.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_WhenTreeFails_OmitsToolbarAndExportActions()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeException = new HttpRequestException("tree-boom");

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-orgchart-toolbar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exportar PNG", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exportar PDF", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No se pudo cargar el organigrama", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issue #286: el wire contract del árbol ahora expone
    /// <c>vigenteDesde</c> y <c>vigenteHasta</c> en el DTO
    /// (<see cref="UnidadOrganizativaTreeNodeDto"/>) para que la
    /// capa de Aplicación pueda propagar la ventana al shell web y
    /// el filtro de unidades no vigentes tenga datos reales para
    /// trabajar. Esta cobertura valida la firma del record por
    /// reflexión — defense-in-depth contra un cambio que retire las
    /// fechas por error.
    /// </summary>
    [Fact]
    public void UnidadOrganizativaTreeNodeDto_ExponeVigenteDesdeYVigenteHasta()
    {
        var type = typeof(UnidadOrganizativaTreeNodeDto);

        var desdeProp = type.GetProperty("VigenteDesde");
        Assert.NotNull(desdeProp);
        Assert.Equal(typeof(DateOnly?), desdeProp!.PropertyType);

        var hastaProp = type.GetProperty("VigenteHasta");
        Assert.NotNull(hastaProp);
        Assert.Equal(typeof(DateOnly?), hastaProp!.PropertyType);
    }

    /// <summary>
    /// Issue #286: el JSON que consume el JS del organigrama debe
    /// incluir el texto derivado del rango de vigencia (formateado
    /// por <see cref="VigenciaViewModel"/>) para que el operador
    /// pueda ver el rango en el tooltip si lo necesita, y el flag
    /// <c>esVigente</c> que el filtro evalúa. Esta cobertura es la
    /// que rompe si alguien refactoriza la proyección del viewmodel.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_WhenApiReturnsVigencia_ExposesVigenciaTextoAndEsVigenteInTreeData()
    {
        var facultyId = Guid.NewGuid();
        var vigenteDesde = new DateOnly(2024, 1, 1);
        var vigenteHasta = new DateOnly(2099, 12, 31);

        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse(
            [
                new UnidadOrganizativaTreeNodeDto(
                    facultyId, "RECT", "Rectorado",
                    Guid.NewGuid(), "Institución", [],
                    vigenteDesde, vigenteHasta)
            ],
            []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // VigenciaViewModel.Desde formatea `dd/MM/yyyy` por cultura
        // invariant. Verificamos el rango presente en `vigencia.texto`
        // (que es lo que el JS podría usar como tooltip futuro) y el
        // flag `esVigente` (que es el input actual del filtro).
        Assert.Contains("\"id\":\"" + facultyId.ToString() + "\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("01/01/2024", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("31/12/2099", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"esVigente\":true", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issue #286: la proyección para el JS incluye <c>esVigente</c>
    /// calculado contra la fecha de referencia del servidor. Una
    /// unidad con <c>VigenteHasta</c> anterior a hoy debe llegar al
    /// browser con <c>esVigente:false</c> para que el filtro la pueda
    /// esconder; una con rango vigente o sin rango debe llegar en
    /// <c>true</c>.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_ProjectsEsVigenteFlagForJsFilter()
    {
        var vigenteId = Guid.NewGuid();
        var fueraDeVigenciaId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse(
            [
                new UnidadOrganizativaTreeNodeDto(
                    vigenteId, "VIG", "Unidad vigente",
                    Guid.NewGuid(), "Institución", [],
                    new DateOnly(2020, 1, 1), new DateOnly(2099, 12, 31)),
                new UnidadOrganizativaTreeNodeDto(
                    fueraDeVigenciaId, "OLD", "Unidad vencida",
                    Guid.NewGuid(), "Institución", [],
                    new DateOnly(2000, 1, 1), new DateOnly(2001, 1, 1))
            ],
            []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El contrato del viewmodel debe exponer `esVigente` para que
        // `applyFilters` del JS pueda tomar la decisión de ocultar.
        // Validamos por substring exacta para fijar el contrato wire
        // entre Razor y organigrama.js.
        Assert.Contains("\"id\":\"" + vigenteId.ToString() + "\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"esVigente\":true", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"id\":\"" + fueraDeVigenciaId.ToString() + "\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"esVigente\":false", content, StringComparison.OrdinalIgnoreCase);
    }
}
