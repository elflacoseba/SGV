using System.Net;
using System.Web;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
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
        Assert.Null(query.UserId);
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
            "/auditorias?entityName=Cargo&operation=Alta&userId=u-42");
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
            "/auditorias?p=2&pageSize=20&entityName=Cargo&operation=Alta&userId=u-7");
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
        Assert.Contains("userId=u-7", content, StringComparison.OrdinalIgnoreCase);
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
}
