using System.Net;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests del PageModel de <c>/organizacion/ocupaciones/detalles/{id}</c>
/// para Slice 3a del change #208: render readonly con datos del DTO,
/// acciones de ciclo de vida (Finalizar/Eliminar/Reactivar) gateadas por
/// Admin + estado, validación FechaFin (REQ-OCC-FORM-007), feedback de
/// 409 por colisión al reactivar (REQ-OCC-FORM-008), 401, 404, transporte
/// recuperable y gate de lectura anónimo.
/// <para>
/// Slice 3 del change <c>reusable-persona-card</c> (issue #219): extiende
/// la cobertura para la migración de <c>Ocupaciones/Details.cshtml</c> a
/// la partial unificada <c>_PersonaCard</c>. Cubre el enriquecimiento
/// opcional vía <see cref="IPersonaApiClient.GetByIdAsync"/>, el fallback
/// silencioso a <c>PersonaNombre</c> cuando el fetch falla, y la
/// preservación del contrato <c>data-*</c> del binding JS.
/// </para>
/// </summary>
[Collection("WebIntegration")]
public sealed class OcupacionDetailsPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public OcupacionDetailsPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private async Task<WebClientLease> CreateLeaseAsync(
        IOcupacionApiClient ocupacion,
        IPersonaApiClient? persona = null,
        bool adminRole = false)
    {
        if (persona is null)
        {
            return await _fixture.CreateOcupacionLeaseAsync(ocupacion, adminRole);
        }

        // Reutiliza el form lease (que ya inyecta persona/puestos) y le
        // descarta los puestos. Slice 3 sólo necesita el cliente de
        // persona para triangular el enriquecimiento de la card.
        var puestos = new SGV.Tests.Web.Puesto.FakePuestosApiClient();
        return await _fixture.CreateOcupacionFormLeaseAsync(
            ocupacion,
            persona,
            puestos,
            adminRole);
    }

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
        // REQ-DET-BTN-004: el href del botón Editar se genera vía Url.Page
        // con parámetros de paginación preservados (p/search/sort).
        Assert.Contains($"href=\"/organizacion/ocupaciones/editar/{id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p=1", content, StringComparison.OrdinalIgnoreCase);
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

    // ──────────────────────────────────────────────────
    // Slice 3 / issue #219 — Migración de Ocupaciones/Details
    // a la partial unificada `_PersonaCard` (modo readonly). El
    // PageModel inyecta IPersonaApiClient, llama
    // GetByIdAsync(Ocupacion.PersonaId) y expone el resultado en
    // OcupacionDetailsViewModel.Persona. Sobre 404 o falla de
    // transporte cae al fallback PersonaNombre sin marcar IsNotFound.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedPersonaCardWithLink()
    {
        // GIVEN: OcupacionDto con PersonaId válido y GetByIdAsync devuelve DTO completo.
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var personaDto = new PersonaDto(
            Id: personaId,
            Legajo: "L-3210",
            Nombres: "Ana",
            Apellidos: "García",
            Email: "ana.garcia@example.com",
            TipoDocumentoId: Guid.NewGuid(),
            TipoDocumentoCodigo: "DNI",
            TipoDocumentoNombre: "Documento Nacional de Identidad",
            NumeroDocumento: "30123456",
            Telefono: "+54 11 5555-3210",
            IsActive: true);

        var dto = FakeOcupacionApiClient.BuildDto(
            id: id,
            personaId: personaId,
            personaNombre: "Ana García");
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = dto };
        var personaClient = FakePersonaApiClient.WithPersonaList(personaDto);

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // PER-CARD-01/02: la card enriquecida emite contenedor + card.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);

        // PER-CARD-02: Email, Teléfono y badge de Estado están presentes.
        Assert.Contains("ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+54 11 5555-3210", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Activa", content, StringComparison.OrdinalIgnoreCase);

        // PER-CARD-10: el Nombre se enlaza al detalle de Persona.
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);

        // PER-CARD-03: ShowStatusBadge=true → el badge "Estado" está en la card.
        Assert.Contains("Estado", content, StringComparison.OrdinalIgnoreCase);

        // Details es readonly → sin botones mutables (PER-CARD-01/04).
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);

        // El API de Persona se llamó exactamente una vez con el PersonaId resuelto.
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
    }

    [Fact]
    public async Task Get_Details_WhenPersonaApiReturns404_FallsBackToPersonaNombreWithLink()
    {
        // GIVEN: OcupacionDto con PersonaId válido pero GetByIdAsync devuelve null (404).
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var dto = FakeOcupacionApiClient.BuildDto(
            id: id,
            personaId: personaId,
            personaNombre: "Ana García");
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = dto };
        // Fake vacío → GetByIdAsync devuelve null.
        var personaClient = new FakePersonaApiClient();

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // PER-CARD-06: el PageModel cae al fallback con PersonaNombre, sin
        // marcar IsNotFound (la ocupación sí existe; sólo se degrada la card).
        Assert.DoesNotContain("La ocupación solicitada no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Detalle de ocupación", content, StringComparison.OrdinalIgnoreCase);

        // Fallback del partial readonly: contenedor display + PersonaNombre con link.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana García", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);

        // Sin card enriquecida (DTO null → rama fallback).
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activa", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Inactiva", content, StringComparison.OrdinalIgnoreCase);

        // Readonly → sin botones mutables.
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);

        // El API de Persona se llamó exactamente una vez.
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
    }

    [Fact]
    public async Task Get_Details_WhenPersonaApiThrows_FallsBackToPersonaNombreWithoutIsNotFound()
    {
        // GIVEN: GetByIdAsync lanza HttpRequestException (falla de transporte).
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var dto = FakeOcupacionApiClient.BuildDto(
            id: id,
            personaId: personaId,
            personaNombre: "Ana García");
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = dto };
        var personaClient = new FakePersonaApiClient
        {
            GetByIdException = new HttpRequestException("upstream persona unavailable")
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // PER-CARD-06: el PageModel degrada silenciosamente. NO marca
        // IsNotFound — la ocupación sí existe, sólo se cae a PersonaNombre.
        Assert.DoesNotContain("La ocupación solicitada no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Detalle de ocupación", content, StringComparison.OrdinalIgnoreCase);

        // Fallback visible con PersonaNombre + link al detalle de Persona.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana García", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"href=\"/personas/detalle/{personaId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);

        // El API de Persona se invocó una vez (la falla no aborta el render).
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
    }

    [Fact]
    public async Task Get_Details_WhenPersonaIdIsEmpty_FallsBackToPersonaNombreWithoutCallingApi()
    {
        // GIVEN: OcupacionDto con PersonaId = Guid.Empty. PageModel NO
        // debe invocar IPersonaApiClient.GetByIdAsync y debe caer al
        // fallback con PersonaNombre.
        var id = Guid.NewGuid();
        var dto = FakeOcupacionApiClient.BuildDto(
            id: id,
            personaId: Guid.Empty,
            personaNombre: "Persona desconocida");
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = dto };
        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(Guid.NewGuid(), "L-001", "Cualquiera", "Cualquiera", null, null, null, null, null, null, true));

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // PersonaId.Empty → no se consulta el API de Persona.
        Assert.Empty(personaClient.GetByIdCalls);

        // Render correcto con fallback y nombre del DTO wire.
        Assert.Contains("Persona desconocida", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
    }
}