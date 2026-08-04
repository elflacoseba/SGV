using System.Net;
using System.Web;
using SGV.Contracts.Auditoria;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Auditoria;

/// <summary>
/// Tests S3-b del módulo de auditoría (Slice B del change
/// <c>2026-07-31-ajustes-listado-auditoria</c> / issue #248): seam
/// tests de la Razor Page <c>Pages/Auditorias/Details</c>
/// ejecutados contra <see cref="SgvWebApplicationFactory"/> con un
/// <see cref="FakeAuditoriaApiClient"/> inyectado en el contenedor
/// del host.
///
/// Cubre la matriz del task 1.B.1 (RED):
///   - 200 OK con old/new/changed rendereados dentro de <c>&lt;pre&gt;</c>.
///   - 404 → estado legible (no crash).
///   - Falla de transporte (HttpRequestException) → banner
///     recuperable preservando el <c>id</c> consultado.
///   - No-admin → redirect a <c>/error/403</c> por el
///     <c>[Authorize(Roles = Administrador)]</c> de la PageModel.
///
/// Spec: <c>auditoria-detalle</c> §"Página web de detalle con render
/// preformateado" + §"Endpoint de detalle API protegido por
/// Administrador".
/// </summary>
/// <remarks>
/// El contrato del cliente HTTP tipado
/// (<see cref="FakeAuditoriaApiClient.GetDetalleHandler"/>) ya cubre:
///   - 200: <see cref="AuditoriaDetalleDto"/> con old/new/changed poblados
///   - 404: <c>null</c> (mapeo via <c>GetDetalleAsync</c> → <c>null</c>)
///   - HttpRequestException: propagado por el <c>Fake</c>
/// Los escenarios de la Razor Page se modelan contra esos tres caminos.
/// </remarks>
[Collection("WebIntegration")]
public sealed class AuditoriasDetailsTests
{
    private readonly WebIntegrationFixture _fixture;

    public AuditoriasDetailsTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private async Task<WebClientLease> CreateAuditoriaLeaseAsync(
        FakeAuditoriaApiClient apiClient, bool adminRole = true)
        => await _fixture.CreateAuditoriaLeaseAsync(apiClient, adminRole);

    private static AuditoriaDetalleDto MakeAuditoriaDetalleDto(
        Guid? id = null,
        string entityName = "Cargo",
        string entityId = "42",
        string operation = "Modificacion",
        DateTime? occurredAt = null,
        string? userId = "u-admin",
        string? userName = "u-admin-name",
        Guid? correlationId = null,
        string? oldValuesJson = "{\"Nombre\":\"Antes\"}",
        string? newValuesJson = "{\"Nombre\":\"Después\"}",
        string? changedPropertiesJson = "[\"Nombre\"]")
        => new(
            id ?? Guid.NewGuid(),
            entityName,
            entityId,
            operation,
            occurredAt ?? new DateTime(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc),
            userId,
            userName,
            correlationId,
            changedPropertiesJson,
            oldValuesJson,
            newValuesJson);

    // ====================================================================
    // 1.B.1.d — 200 OK con JSON rendereado en <pre>
    // ====================================================================

    /// <summary>
    /// 1.B.1.d — Un administrador que solicita el detalle de un
    /// registro existente recibe 200 OK con los bloques
    /// <c>&lt;pre&gt;</c> rendereando <c>OldValuesJson</c>,
    /// <c>NewValuesJson</c> y <c>ChangedPropertiesJson</c>. La
    /// página expone el header con <c>EntityName</c>,
    /// <c>Operation</c>, <c>OccurredAt</c>, <c>UserName</c>,
    /// <c>CorrelationId</c> y <c>EntityId</c>.
    /// </summary>
    [Fact]
    public async Task Get_Details_WhenRecordExists_RendersPreformattedJsonAndHeader()
    {
        var id = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var apiClient = new FakeAuditoriaApiClient
        {
            GetDetalleResult = MakeAuditoriaDetalleDto(
                id: id,
                entityName: "Cargo",
                entityId: "42",
                operation: "Modificacion",
                correlationId: correlationId,
                oldValuesJson: "{\"Nombre\":\"Antes\"}",
                newValuesJson: "{\"Nombre\":\"Después\"}",
                changedPropertiesJson: "[\"Nombre\"]")
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/auditorias/details?id={id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Header con metadatos clave.
        Assert.Contains("Cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Modificacion", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("42", content, StringComparison.OrdinalIgnoreCase);

        // UserName resuelto por el LEFT JOIN (no el fallback "—").
        Assert.Contains("u-admin-name", content, StringComparison.OrdinalIgnoreCase);

        // CorrelationId rendereado.
        Assert.Contains(correlationId.ToString("D"), content, StringComparison.OrdinalIgnoreCase);

        // JSON blocks dentro de <pre class="bg-light p-2">.
        Assert.Contains("<pre", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bg-light p-2", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Antes", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Después", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nombre", content, StringComparison.OrdinalIgnoreCase);

        // El cliente HTTP fue invocado exactamente una vez con el id correcto.
        Assert.Equal(new[] { id }, apiClient.GetDetalleCalls.ToArray());
    }

    // ====================================================================
    // 1.B.1.e — 404 → estado legible (no crash)
    // ====================================================================

    /// <summary>
    /// 1.B.1.e — Cuando el cliente devuelve <c>null</c> (404
    /// upstream), la Razor Page muestra un estado legible de "no
    /// encontrado" (NO un crash, NO un screen de error 500). El
    /// cliente es invocado una sola vez con el id solicitado.
    /// </summary>
    [Fact]
    public async Task Get_Details_WhenRecordMissing_ShowsNotFoundState()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeAuditoriaApiClient
        {
            GetDetalleHandler = _ => null
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/auditorias/details?id={id:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El estado legible NO contiene los bloques <pre> con JSON.
        Assert.DoesNotContain("bg-light p-2", content, StringComparison.OrdinalIgnoreCase);

        // Tampoco expone el wire enrichcido (no EntityId ni old/new).
        // La página debe mostrar un mensaje "no disponible" legible.
        Assert.True(
            content.Contains("no está disponible", StringComparison.OrdinalIgnoreCase)
                || content.Contains("no encontrado", StringComparison.OrdinalIgnoreCase)
                || content.Contains("no disponible", StringComparison.OrdinalIgnoreCase),
            "Detalles: la Razor Page debe mostrar un estado legible 404 cuando el backend responde 404.");

        Assert.Equal(new[] { id }, apiClient.GetDetalleCalls.ToArray());
    }

    // ====================================================================
    // 1.B.1.f — Fallo de transporte → banner recuperable preservando id
    // ====================================================================

    /// <summary>
    /// 1.B.1.f — Una <see cref="HttpRequestException"/> propagada por
    /// el cliente HTTP se traduce a un banner de error recuperable
    /// (con <see cref="TransportFailureClassifier"/>) sin perder el
    /// <c>id</c> consultado en la URL. La página sigue devolviendo
    /// 200 OK para que el banner sea visible (patrón del shell).
    /// </summary>
    [Fact]
    public async Task Get_Details_WhenTransportFails_ShowsRecoverableBanner()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeAuditoriaApiClient
        {
            GetDetalleException = new HttpRequestException("upstream offline")
        };

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/auditorias/details?id={id:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // Banner de error recuperable (sin stack traces, copy canónica).
        Assert.True(
            content.Contains("alert-danger", StringComparison.OrdinalIgnoreCase)
                || content.Contains("alert alert-danger", StringComparison.OrdinalIgnoreCase),
            "Detalles: la Razor Page debe renderizar un alert-danger para falla de transporte.");
        Assert.True(
            content.Contains("No se pudo contactar al servicio", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Intentá nuevamente", StringComparison.OrdinalIgnoreCase),
            "Detalles: el banner debe usar wording recuperable canonical (TransportFailureClassifier).");

        // El id se preserva en la URL que el usuario ya tiene
        // (puede reintentar el mismo Details con F5 sin re-armar el
        // request); el CTA "Volver al listado" debe existir para
        // permitir recuperar el flujo incluso sin volver a tipear.
        Assert.Contains(
            "Volver al listado",
            content,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(new[] { id }, apiClient.GetDetalleCalls.ToArray());
    }

    // ====================================================================
    // 1.B.1.g — No-admin → redirect a /error/403
    // ====================================================================

    /// <summary>
    /// 1.B.1.g — Un usuario autenticado sin rol <c>Administrador</c>
    /// es rechazado por el <c>[Authorize(Roles = Administrador)]</c>
    /// de la PageModel; el shell redirige a la página de 403
    /// configurada en la cookie auth (<c>/error/403</c>). El
    /// cliente API NO se invoca.
    /// </summary>
    [Fact]
    public async Task Get_Details_WhenNonAdmin_RedirectsToAccessDenied()
    {
        var apiClient = new FakeAuditoriaApiClient();

        await using var lease = await CreateAuditoriaLeaseAsync(apiClient, adminRole: false);

        var response = await lease.Client.GetAsync(
            $"/auditorias/details?id={Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/error/403",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);

        // El guard corta antes de invocar al cliente API: la
        // autorización por acción es la primera línea de defensa.
        Assert.Empty(apiClient.GetDetalleCalls);
    }
}
