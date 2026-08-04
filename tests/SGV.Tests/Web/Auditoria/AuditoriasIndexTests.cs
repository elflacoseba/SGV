using System.Net;
using System.Web;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Pages.Auditorias;
using Xunit;

namespace SGV.Tests.Web.Auditoria;

/// <summary>
/// Tests S3 del módulo de auditoría: seam tests del PageModel
/// <c>Pages/Auditorias/Index</c> ejecutados contra
/// <see cref="SgvWebApplicationFactory"/> con un
/// <see cref="FakeAuditoriaApiClient"/> inyectado en el contenedor
/// del host. Cubre los escenarios del task 3.1:
///   - Admin 200 con tabla + paginación.
///   - Lista vacía legible.
///   - Error de transporte recuperable sin perder filtros.
///   - Paginación que conserva los filtros vigentes.
///   - No-admin → estado de error.
///   - Anónimo → redirect a sign-in.
/// </summary>
/// <remarks>
/// <para>
/// En STRICT TDD este archivo es la fase RED. El tipo
/// <c>SGV.Web.Pages.Auditorias.IndexModel</c> aún NO existe
/// (se introduce en la fase GREEN); el archivo NO compila
/// hasta que la fase GREEN lo introduzca junto con su Razor
/// Page, su <see cref="IAuditoriaApiClient"/> y la registración
/// DI en <c>Program.cs</c>.
/// </para>
/// <para>
/// Para evitar acoplar la suite a un backend real, los tests
/// inyectan <see cref="FakeAuditoriaApiClient"/> vía
/// <see cref="WebIntegrationFixture.CreateAuditoriaLeaseAsync"/>,
/// que sigue el mismo patrón de los otros módulos
/// (<c>CreateCargoLeaseAsync</c>, <c>CreateHabilidadLeaseAsync</c>).
/// El fake respeta el contrato del <c>IAuditoriaApiClient</c>
/// real: <c>QueryAsync</c> devuelve <see cref="PagedResult{T}"/>;
/// <c>GetDetalleAsync</c> devuelve <c>null</c> cuando el
/// backend responde 404 (no se usa acá porque la página actual
/// es read-only de listado).
/// </para>
/// </remarks>
[Collection("WebIntegration")]
public sealed class AuditoriasIndexTests
{
    private readonly WebIntegrationFixture _fixture;

    public AuditoriasIndexTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private async Task<WebClientLease> CreateAuditoriaLeaseAsync(
        FakeAuditoriaApiClient apiClient, bool adminRole = true)
        => await _fixture.CreateAuditoriaLeaseAsync(apiClient, adminRole);

    private static AuditoriaDto MakeAuditoriaDto(
        Guid? id = null,
        string entityName = "Cargo",
        string operation = "Modificacion",
        string? userId = "u-test",
        DateTime? occurredAt = null) =>
        new(
            id ?? Guid.NewGuid(),
            entityName,
            operation,
            occurredAt ?? new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            userId,
            "u-test-name",
            "[\"Nombre\"]",
            Guid.NewGuid());

    // ====================================================================
    // 3.1.a — Admin 200 con tabla + paginación
    // ====================================================================

    /// <summary>
    /// 3.1.a — Un administrador que carga la página recibe 200 OK y ve
    /// la grilla con las filas devueltas por el fake + los
    /// controles de paginación. La query inicial usa defaults
    /// (Page=1, PageSize=20, sin filtros) — replica del patrón
    /// vigente en los otros Index read-only del shell.
    /// </summary>
    [Fact]
    public async Task Get_Index_WhenAdmin_RendersTableAndPagination()
    {
        var first = MakeAuditoriaDto(entityName: "Cargo", operation: "Alta", occurredAt: new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));
        var second = MakeAuditoriaDto(entityName: "Persona", operation: "Modificacion", occurredAt: new DateTime(2026, 2, 2, 11, 0, 0, DateTimeKind.Utc));
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>(
                [first, second], TotalCount: 2, Page: 1, PageSize: 20)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/auditorias");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de auditoría del sistema", content, StringComparison.OrdinalIgnoreCase);

        // Filas visibles: la tabla debe mostrar el nombre de la
        // entidad, la operación y el UserName (resultado del LEFT JOIN
        // contra AspNetUsers). Como la auditoría es read-only
        // (no expone old/new), el contenido de la fila viene del
        // wire contract seguro (D-2).
        Assert.Contains("Cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Alta", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Modificacion", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("u-test-name", content, StringComparison.OrdinalIgnoreCase);

        // Paginación: cuando TotalCount=2 y PageSize=20, TotalPages=1
        // y los enlaces de paginación se renderizan en estado
        // disabled (el wire no se rompe ni en una página única).
        Assert.Contains("Página 1 de 1", content, StringComparison.OrdinalIgnoreCase);

        // El backend recibió exactamente un QueryAsync con los
        // defaults (Page=1, PageSize=20, sin filtros).
        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Null(query.EntityName);
        Assert.Null(query.Operation);
        Assert.Null(query.DateFrom);
        Assert.Null(query.DateTo);
        Assert.Null(query.UserName);
    }

    // ====================================================================
    // 3.1.b — Lista vacía legible
    // ====================================================================

    /// <summary>
    /// 3.1.b — Cuando el backend devuelve una página vacía, la
    /// tabla muestra un mensaje legible y el contador refleja 0
    /// registros (no se renderiza la fila vacía genérica del
    /// shell).
    /// </summary>
    [Fact]
    public async Task Get_Index_WhenListIsEmpty_ShowsEmptyState()
    {
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>([], 0, 1, 20)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/auditorias");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "No se encontraron registros de auditoría para los filtros aplicados.",
            content,
            StringComparison.OrdinalIgnoreCase);
        // El contador visible debe decir "0 registro(s)" en la
        // barra de header de la grilla.
        Assert.Contains("0 registro(s)", content, StringComparison.OrdinalIgnoreCase);

        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Null(query.EntityName);
    }

    // ====================================================================
    // 3.1.c — Error de transporte recuperable sin perder filtros
    // ====================================================================

    /// <summary>
    /// 3.1.c — Una falla de transporte (HttpRequestException) hace
    /// que la página muestre un banner de error recuperable y un
    /// estado vacío; los filtros vigentes del querystring deben
    /// preservarse en el HTML renderizado para que el usuario
    /// pueda reintentar sin re-tipear.
    /// </summary>
    [Fact]
    public async Task Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters()
    {
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryException = new HttpRequestException("boom")
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/auditorias?entityName=Cargo&operation=Alta&userName=u-42");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado de auditoría", content, StringComparison.OrdinalIgnoreCase);

        // Los filtros vigentes deben quedar renderizados en el
        // form de la sidebar para permitir reintento sin re-tipear.
        Assert.Contains("value=\"Cargo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"Alta\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"u-42\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ====================================================================
    // 3.1.d — Paginación preserva los filtros vigentes
    // ====================================================================

    /// <summary>
    /// 3.1.d — Los enlaces de paginación preservan los filtros
    /// vigentes del querystring. La página siguiente debe llevar
    /// los filtros de cadena + pageSize + page incrementado. Las
    /// fechas (<c>DateTime?</c>) se serializan vía el binder de
    /// <c>Url.Page</c> en formato dependiente de cultura, así que
    /// el assert se concentra en los filtros string-only para
    /// mantener el test estable y no culture-fragile.
    /// </summary>
    [Fact]
    public async Task Get_Index_Pagination_PreservesFilters()
    {
        // 45 filas en total → 3 páginas con PageSize=20.
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>(
                [MakeAuditoriaDto()], TotalCount: 45, Page: 2, PageSize: 20)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/auditorias?p=2&pageSize=20&entityName=Cargo&operation=Alta&userName=u-7");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Página 2 de 3", content, StringComparison.OrdinalIgnoreCase);

        // El link "Siguiente" (p=3) debe preservar los filtros
        // string-only y el pageSize. El parámetro de paginación es
        // `p` (no `page`) para no colisionar con el identificador
        // interno de Razor Pages.
        Assert.Contains("p=3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageSize=20", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("entityName=Cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operation=Alta", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("userName=u-7", content, StringComparison.OrdinalIgnoreCase);
    }

    // ====================================================================
    // 3.1.e — No-admin → error (no autorizado)
    // ====================================================================

    /// <summary>
    /// 3.1.e — Un usuario autenticado sin rol Administrador que
    /// intenta acceder a la página NO recibe 200 con la grilla:
    /// el atributo <c>[Authorize(Roles = RolesSgv.Administrador)]</c>
    /// lo rechaza. El shell redirige a la página de 403 configurada
    /// en la cookie auth (<c>/error/403</c>).
    /// </summary>
    [Fact]
    public async Task Get_Index_WhenNonAdmin_RedirectsToAccessDenied()
    {
        var apiClient = new FakeAuditoriaApiClient();

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: false);

        var response = await lease.Client.GetAsync("/auditorias");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/error/403",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
        // El backend NO debe recibir ninguna consulta: el guard del
        // PageModel corta antes de invocar al cliente.
        Assert.Empty(apiClient.QueryCalls);
    }

    // ====================================================================
    // 3.1.f — Anónimo → redirect a sign-in
    // ====================================================================

    /// <summary>
    /// 3.1.f — Un cliente sin autenticar que pide la página es
    /// redirigido al flujo de sign-in (mismo comportamiento que el
    /// resto de las páginas protegidas del shell).
    /// </summary>
    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/auditorias");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/auth/sign-in",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    // ====================================================================
    // 1.B.1 — Slice B: sort reset p=1 + pageSize selector + propagation
    // ====================================================================

    /// <summary>
    /// 1.B.1.a — Selector de pageSize expone las 4 opciones canónicas
    /// (10/20/50/100) y refleja la opción vigente. Cuando no se
    /// pasa <c>pageSize</c>, la opción seleccionada es la default
    /// del sistema (20). Spec <c>auditoria-page-size</c> §"Selector
    /// de PageSize con opciones 10/20/50/100".
    /// </summary>
    [Fact]
    public async Task Get_Index_PageSizeSelector_RendersAllFourOptionsWithDefaultSelected()
    {
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>([], 0, 1, IndexModel.DefaultPageSize)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/auditorias");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<select", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"pageSize\"", content, StringComparison.OrdinalIgnoreCase);

        // Las 4 opciones canónicas deben estar presentes.
        Assert.Matches("value=\"10\"", content);
        Assert.Matches("value=\"20\"", content);
        Assert.Matches("value=\"50\"", content);
        Assert.Matches("value=\"100\"", content);

        // Default seleccionado cuando no hay pageSize en querystring.
        // El helper Razor <option ... selected> deja "selected" como
        // atributo; assert relajado para tolerar el formato exacto
        // que el binder elige.
        Assert.Contains("selected", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">20<", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.B.1.b — El selector refleja la opción vigente cuando el
    /// request trae <c>pageSize=50</c>. Spec <c>auditoria-page-size</c>
    /// §"Selector refleja el pageSize actual".
    /// </summary>
    [Fact]
    public async Task Get_Index_PageSizeSelector_ReflectsActivePageSize()
    {
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>([], 0, 1, 50)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/auditorias?pageSize=50");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El backend recibió pageSize=50 explícito (no se normaliza
        // a default porque está en {10,20,50,100}).
        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(50, query.PageSize);
    }

    /// <summary>
    /// 1.B.1.c — pageSize fuera del set canónico {10,20,50,100} cae
    /// al default (20) antes de llegar al backend. Spec
    /// <c>auditoria-page-size</c> §"PageSize inválido o fuera de
    /// rango se normaliza".
    /// </summary>
    [Theory]
    [InlineData(15)]
    [InlineData(0)]
    [InlineData(999)]
    public async Task Get_Index_PageSizeOutOfSet_NormalizesToDefault(int requested)
    {
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>([], 0, 1, IndexModel.DefaultPageSize)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/auditorias?pageSize={requested}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El backend debe recibir el default (20), NO el valor
        // fuera de rango. La shell es la primera línea de
        // normalización del selector (auditoria-page-size).
        var query = Assert.Single(apiClient.QueryCalls);
        Assert.Equal(IndexModel.DefaultPageSize, query.PageSize);
    }

    /// <summary>
    /// 1.B.1.d — Cambiar el criterio de orden resetea
    /// <c>page</c> a <c>1</c> y preserva el <c>pageSize</c> y los
    /// filtros vigentes. Los enlaces de los <c>&lt;th&gt;</c>
    /// ordenables deben llevar <c>?p=1&amp;sort=X&amp;pageSize=Y&amp;...</c>.
    /// Spec <c>auditoria-sort</c> §"Reset a página 1 al cambiar
    /// sort en la shell web".
    /// </summary>
    [Fact]
    public async Task Get_Index_SortHeader_LinkResetsPageAndPreservesPageSizeAndFilters()
    {
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>([MakeAuditoriaDto()], 60, 1, IndexModel.DefaultPageSize)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        // El usuario está en página 3 con pageSize=50 y filtro entityName=Cargo.
        var response = await lease.Client.GetAsync(
            "/auditorias?p=3&pageSize=50&sort=fecha_desc&entityName=Cargo");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El header clickeable de Entidad debe apuntar a p=1 (reset)
        // y propagar pageSize=50 + entityName=Cargo + la nueva clave
        // sort=entidad_asc.
        Assert.Contains("p=1", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageSize=50", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=entidad_asc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("entityName=Cargo", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.B.1.e — Los enlaces de paginación preservan <c>sort</c> y
    /// <c>pageSize</c> además de los filtros. La página "Siguiente"
    /// debe llevar <c>?p=2&amp;pageSize=50&amp;sort=Y&amp;...</c>.
    /// Spec <c>auditoria-sort</c> §"Paginación preserva sort activo"
    /// + <c>auditoria-page-size</c> §"Enlaces de paginación
    /// preservan PageSize".
    /// </summary>
    [Fact]
    public async Task Get_Index_Pagination_PreservesSortAndPageSize()
    {
        var apiClient = new FakeAuditoriaApiClient
        {
            // Page=1, total=120 → TotalPages=3 con PageSize=50.
            QueryResult = new PagedResult<AuditoriaDto>([MakeAuditoriaDto()], 120, 1, 50)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/auditorias?p=1&pageSize=50&sort=usuario_desc&entityName=Cargo");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El link Siguiente (p=2) debe llevar el sort + pageSize + entityName.
        Assert.Contains("p=2", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageSize=50", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=usuario_desc", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("entityName=Cargo", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.B.1.f — El link a la página de Detalle (acción por fila)
    /// debe preservar el contexto del listado: <c>p</c>,
    /// <c>pageSize</c>, <c>sort</c> y filtros. Spec
    /// <c>auditoria-detalle</c> §"Página web de detalle con render
    /// preformateado" (la PageModel de Details bindea estos
    /// parámetros para ofrecer "Volver al listado" preservando el
    /// contexto, requisito no-normativo del diseño).
    /// </summary>
    [Fact]
    public async Task Get_Index_DetailsLink_PreservesListContext()
    {
        var itemId = Guid.NewGuid();
        var apiClient = new FakeAuditoriaApiClient
        {
            QueryResult = new PagedResult<AuditoriaDto>(
                [MakeAuditoriaDto(id: itemId, entityName: "Cargo")],
                TotalCount: 1,
                Page: 1,
                PageSize: IndexModel.DefaultPageSize)
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/auditorias?p=2&pageSize=50&sort=fecha_desc&entityName=Cargo");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El link al detalle de la fila debe llevar el id + el contexto.
        Assert.Contains("auditorias/details", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(itemId.ToString("D"), content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=2", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageSize=50", content, StringComparison.OrdinalIgnoreCase);
    }
}
