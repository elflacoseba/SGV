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
        // Issue #286 (revisión): el segundo switch filtra unidades
        // EXPIRADAS (no vigentes). Las vigentes se muestran siempre;
        // el switch OFF las oculta. El label refleja esa semántica.
        Assert.Contains("data-orgchart-toggle=\"showCode\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-orgchart-toggle=\"showExpiradas\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"toggle-show-code\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"toggle-show-expiradas\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mostrar código", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mostrar unidades expiradas", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mostrar unidades vigentes", content, StringComparison.OrdinalIgnoreCase);

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
    /// Issue #286 (3er round): el JSON que consume el JS del
    /// organigrama expone las fechas de vigencia CRUDAS
    /// (<c>vigenteDesde</c>, <c>vigenteHasta</c>) para que el filtro de
    /// "Mostrar unidades expiradas" se calcule ENTERAMENTE en el
    /// cliente. Antes dependíamos de un <c>esVigente</c> server-side
    /// que daba resultados confusos al operador para unidades sin
    /// <c>VigenteHasta</c> configurado. Esta cobertura rompe si alguien
    /// intenta revertir el cambio volviendo a calcular la vigencia en
    /// el server.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_WhenApiReturnsVigencia_ExposesRawVigenteDesdeYVigenteHastaInTreeData()
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

        Assert.Contains("\"id\":\"" + facultyId.ToString() + "\"", content, StringComparison.OrdinalIgnoreCase);

        // Las fechas se exponen CRUDAS al cliente como strings ISO-8601.
        // System.Text.Json serializa DateOnly como "YYYY-MM-DD" por default.
        Assert.Contains("\"vigenteDesde\":\"2024-01-01\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"vigenteHasta\":\"2099-12-31\"", content, StringComparison.OrdinalIgnoreCase);

        // El flag server-side ya NO debe existir (tercer feedback del
        // operador #286): el JS recalcula la vigencia con `new Date()`
        // para tener una sola fuente de verdad y evitar bugs de
        // proyección. Si vuelve a aparecer, rompemos este test como
        // regression guard.
        Assert.DoesNotContain("\"esVigente\"", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Issue #286 (3er round): una unidad con <c>VigenteHasta</c>
    /// configurado a una fecha pasada debe llegar al JSON con esa
    /// fecha cruda. El JS se encarga de evaluarla contra
    /// <c>new Date()</c> y aplicar el filtro. Defense-in-depth: si
    /// alguien refactoriza y rompe la propagación de fechas del DTO
    /// al viewmodel, este test lo detecta antes de que el operador
    /// lo vea.
    /// </summary>
    [Fact]
    public async Task Get_Organigrama_ProjectsRawDatesForJsClientSideFilter()
    {
        var vigenteHastaPasadoId = Guid.NewGuid();
        var sinVigenciaHastaId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse(
            [
                new UnidadOrganizativaTreeNodeDto(
                    vigenteHastaPasadoId, "OLD", "Unidad con vigencia pasada",
                    Guid.NewGuid(), "Institución", [],
                    new DateOnly(2000, 1, 1), new DateOnly(2001, 1, 1)),
                new UnidadOrganizativaTreeNodeDto(
                    sinVigenciaHastaId, "OPEN", "Unidad sin VigenteHasta",
                    Guid.NewGuid(), "Institución", [],
                    new DateOnly(2020, 1, 1), null)
            ],
            []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync("/organizacion/unidades-organizativas/organigrama");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Unidad con VigenteHasta explícito (en el pasado).
        Assert.Contains("\"id\":\"" + vigenteHastaPasadoId.ToString() + "\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"vigenteHasta\":\"2001-01-01\"", content, StringComparison.OrdinalIgnoreCase);

        // Unidad sin VigenteHasta — debe llegar como null explícito
        // en el JSON (no undefined). System.Text.Json serializa null
        // como `"vigenteHasta":null`.
        Assert.Contains("\"id\":\"" + sinVigenciaHastaId.ToString() + "\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"vigenteHasta\":null", content, StringComparison.OrdinalIgnoreCase);
    }
}
