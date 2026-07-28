using System.Net;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests del PageModel de <c>/organizacion/ocupaciones/detalles/{id}</c>
/// para Slice 3a del change #208: render readonly con datos del DTO,
/// acciones de ciclo de vida (Finalizar/Eliminar/Reactivar) gateadas por
/// Admin + estado, validación FechaFin (REQ-OCC-FORM-007), feedback de
/// 409 por colisión al reactivar (REQ-OCC-FORM-008), 401, 404, transporte
/// recuperable y gate de lectura anónimo.
/// </summary>
[Collection("WebIntegration")]
public sealed class OcupacionDetailsPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public OcupacionDetailsPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private async Task<WebClientLease> CreateLeaseAsync(IOcupacionApiClient ocupacion, bool adminRole = false)
        => await _fixture.CreateOcupacionLeaseAsync(ocupacion, adminRole);

    private static OcupacionDto SampleDto(
        Guid? id = null,
        DateOnly? fechaInicio = null,
        DateOnly? fechaFin = null,
        OcupacionEstado estado = OcupacionEstado.Vigente) =>
        FakeOcupacionApiClient.BuildDto(
            id: id,
            fechaInicio: fechaInicio ?? new DateOnly(2026, 1, 15),
            fechaFin: fechaFin,
            estado: estado);

    // ──────────────────────────────────────────────────
    // Acceso anónimo redirige a /auth/sign-in
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/auth/sign-in",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-003 / Scenario: Render readonly con DTO vigente
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenVigente_ShowsReadOnlyView()
    {
        var id = Guid.NewGuid();
        var dto = SampleDto(id: id);
        var apiClient = new FakeOcupacionApiClient { ObtenerPorIdResult = dto };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: false);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(dto.PersonaNombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(dto.PuestoNombre, content, StringComparison.OrdinalIgnoreCase);
        // La acción Editar NO debe estar visible para no-admin.
        Assert.DoesNotContain("Editar</a>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // 404 recuperable cuando el id no existe
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenIdNotFound_ShowsNotAvailableState()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeOcupacionApiClient { ObtenerPorIdResult = null };

        await using var lease = await CreateLeaseAsync(apiClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        // El bloque dl de campos NO debe estar presente.
        Assert.DoesNotContain("<dl", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-003 / Scenario: Vigente admin → acciones Finalizar/Eliminar + Edit
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenVigenteAdmin_ShowsFinalizarEliminarAndEdit()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = SampleDto(id: id)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Finalizar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Eliminar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"href=\"/organizacion/ocupaciones/editar/{id:D}\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-003 / Scenario: Finalizada admin → acción Reactivar
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenFinalizadaAdmin_ShowsReactivarAction()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = SampleDto(
                id: id,
                fechaFin: new DateOnly(2026, 6, 30),
                estado: OcupacionEstado.Finalizada)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Reactivar", content, StringComparison.OrdinalIgnoreCase);
        // No debe haber un botón Finalizar (vigente) ni un botón Eliminar (vigente)
        // — sólo Reactivar cuando el estado es Finalizada.
        Assert.DoesNotContain("formaction=\"?handler=Finalizar\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaction=\"?handler=Eliminar\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-007 / Scenario: Fecha válida → acepta
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Finalizar_WhenFechaFinValid_PrgWithSuccess()
    {
        var id = Guid.NewGuid();
        var current = SampleDto(id: id, fechaInicio: new DateOnly(2026, 1, 1));
        var finalizada = SampleDto(
            id: id,
            fechaInicio: new DateOnly(2026, 1, 1),
            fechaFin: new DateOnly(2026, 6, 30),
            estado: OcupacionEstado.Finalizada);

        var apiClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdHandler = _ => current,
            FinalizarResult = OcupacionCommandResult.Success(finalizada)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/detalles/{id:D}?handler=Finalizar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["id"] = id.ToString(),
                ["fechaFin"] = "2026-06-30"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var finalizeCall = Assert.Single(apiClient.FinalizarCalls);
        Assert.Equal(id, finalizeCall.Id);
        Assert.Equal(new DateOnly(2026, 6, 30), finalizeCall.Request.FechaFin);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-007 / Scenario: FechaFin < FechaInicio → bloquea cliente
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Finalizar_WhenFechaFinBeforeFechaInicio_BlocksAndWarns()
    {
        var id = Guid.NewGuid();
        var current = SampleDto(id: id, fechaInicio: new DateOnly(2026, 1, 1));
        var apiClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdHandler = _ => current
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/detalles/{id:D}?handler=Finalizar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["id"] = id.ToString(),
                ["fechaFin"] = "2025-12-31"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // El handler NO debe llamar al API cuando el cliente bloquea.
        Assert.Empty(apiClient.FinalizarCalls);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-003 / Scenario: Eliminar vigente → baja lógica
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Eliminar_WhenVigente_RedirectsToIndexWithFeedback()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeOcupacionApiClient
        {
            EliminarResult = new OcupacionCommandResult(true, Value: null, Error: null)
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/detalles/{id:D}?handler=Eliminar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["id"] = id.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("/organizacion/ocupaciones", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/detalles", location, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.EliminarCalls);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-008 / Scenario: Reactivar con colisión → feedback visible
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Reactivar_WhenConflict_PreservesFeedbackWithCode()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeOcupacionApiClient
        {
            ReactivarResult = OcupacionCommandResult.Failure(
                new OcupacionError(
                    ErrorCategoria.Conflict,
                    OcupacionErrorCodigo.PersonaYPuestoOcupados,
                    "El par ya existe."))
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/detalles/{id:D}?handler=Reactivar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["id"] = id.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // Tras PRG al Details el TempData persiste el feedback.
        var followUp = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var followContent = HttpUtility.HtmlDecode(await followUp.Content.ReadAsStringAsync());
        Assert.Contains("PersonaYPuestoOcupados", followContent, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.ReactivarCalls);
    }

    // ──────────────────────────────────────────────────
    // Gate admin para mutaciones
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Finalizar_WhenNotAdmin_Forbids()
    {
        var id = Guid.NewGuid();
        var apiClient = new FakeOcupacionApiClient();
        await using var lease = await CreateLeaseAsync(apiClient, adminRole: false);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/detalles/{id:D}?handler=Finalizar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["id"] = id.ToString(),
                ["fechaFin"] = "2026-06-30"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/error/403",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.FinalizarCalls);
    }

    // ──────────────────────────────────────────────────
    // Transporte recuperable en Finalizar
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Finalizar_WhenHttpRequestException_RedirectsWithTransportMessage()
    {
        var id = Guid.NewGuid();
        var current = SampleDto(id: id, fechaInicio: new DateOnly(2026, 1, 1));
        var apiClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdHandler = _ => current,
            FinalizarException = new HttpRequestException("api caída")
        };

        await using var lease = await CreateLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/detalles/{id:D}?handler=Finalizar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["id"] = id.ToString(),
                ["fechaFin"] = "2026-06-30"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var followUp = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var followContent = HttpUtility.HtmlDecode(await followUp.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo contactar al servicio", followContent, StringComparison.OrdinalIgnoreCase);
    }
}