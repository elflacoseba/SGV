using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using SGV.Tests.Web.Puesto;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests del PageModel de <c>/organizacion/ocupaciones/editar/{id}</c>
/// para Slice 3a del change #208: render de ocupaciones vigentes con
/// pre-poblamiento desde el API, gate admin, bloqueo de edición para
/// finalizadas/eliminadas, 404 recuperable, mapeo de 409 a los campos
/// del <c>ModelState</c>, errores de transporte y PRG al Details tras
/// éxito.
/// </summary>
[Collection("WebIntegration")]
public sealed class OcupacionEditPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public OcupacionEditPageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────

    private static PuestoDto SamplePuesto(string codigo = "P-001", string nombre = "Analista") =>
        new(Guid.NewGuid(), codigo, nombre, null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);

    private static OcupacionDto SampleDto(
        Guid? id = null,
        Guid? personaId = null,
        Guid? puestoId = null,
        OcupacionEstado estado = OcupacionEstado.Vigente) =>
        FakeOcupacionApiClient.BuildDto(
            id: id,
            personaId: personaId,
            puestoId: puestoId,
            personaNombre: "Ana García",
            puestoNombre: "Analista",
            fechaInicio: new DateOnly(2026, 1, 15),
            estado: estado);

    private async Task<WebClientLease> CreateLeaseAsync(
        IOcupacionApiClient ocupacion,
        IPersonaApiClient? persona = null,
        IPuestosApiClient? puestos = null,
        bool adminRole = true)
    {
        return await _fixture.CreateOcupacionFormLeaseAsync(
            ocupacion,
            persona ?? new FakePersonaApiClient(),
            puestos ?? new FakePuestosApiClient(),
            adminRole);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-002 / Scenario: Edición válida (vigente)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenVigente_PrepopulatesFormFromApi()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: puestoId);

        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = current
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El form de edición se renderiza.
        Assert.Contains("Editar ocupación", content, StringComparison.OrdinalIgnoreCase);

        // Issue #216 (OCC-PER-BUSC-04): PersonaId se renderea como hidden
        // input pre-poblado con el id de la persona vinculada (NO como
        // option selected).
        Assert.Matches(
            $@"<input(?=[^>]*name=""{OcupacionFormKeys.PersonaIdKey}"")(?=[^>]*value=""{personaId:D}"")[^>]*type=""hidden""[^>]*>",
            content);

        // PuestoId sigue siendo un <select> (Issue #216 sólo toca PersonaId).
        Assert.Contains(
            $"<option selected=\"selected\" value=\"{puestoId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Single(
            Regex.Matches(
                content,
                @"<select\b[^>]*\bname=""Input\.PuestoId""",
                RegexOptions.IgnoreCase));

        // PersonaVinculada se enriquece vía GetByIdAsync — una sola
        // invocación con el id resuelto.
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
        Assert.Empty(personaClient.GetAllCalls);

        var byIdCall = Assert.Single(ocupacionClient.ObtenerPorIdCalls);
        Assert.Equal(id, byIdCall);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-PER-BUSC-04 / REQ-OCC-PER-BUSC-02 — Edit enriquece la card
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenVigente_LoadCatalogsAsync_CallsPersonaGetByIdAsync()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: puestoId);

        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, Guid.NewGuid(), "DNI", "DNI", "12345678", null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = current };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
        Assert.Empty(personaClient.GetAllCalls);
    }

    [Fact]
    public async Task Get_Edit_WhenPersonaNotFound_FallsBackToEmpty()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: Guid.NewGuid());

        // Fake sin personas: GetByIdAsync devuelve null (no falla el render).
        var personaClient = FakePersonaApiClient.WithPersonaList();
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = current };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-002 / Scenario: Finalizada — bloquea edición
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenFinalizada_ShowsRecoverableBlockedState()
    {
        var id = Guid.NewGuid();
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = SampleDto(id: id, estado: OcupacionEstado.Finalizada)
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Estado recuperable: mensaje de bloqueo + link Volver.
        Assert.Contains("no está disponible para edición", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finalizada", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        // El form de edición NO debe estar presente.
        Assert.DoesNotContain("name=\"Input.PersonaId\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenEliminada_ShowsRecoverableBlockedState()
    {
        var id = Guid.NewGuid();
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = SampleDto(id: id, estado: OcupacionEstado.Eliminada)
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("eliminada", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"Input.PersonaId\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-002 / Scenario: Id inexistente → 404 recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenIdDoesNotExist_ShowsNotAvailableState()
    {
        var id = Guid.NewGuid();
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = null
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Gate admin
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenNotAdmin_RedirectsToAccessDenied()
    {
        var id = Guid.NewGuid();
        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            adminRole: false);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/error/403",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-006 / Scenario: POST éxito → PRG al Details con feedback
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenSuccessful_RedirectsToDetailsWithFeedback()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: puestoId);

        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var updated = SampleDto(
            id: id,
            personaId: personaId,
            puestoId: puestoId);
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = current,
            ActualizarResult = OcupacionCommandResult.Success(updated)
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/editar/{id:D}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = puestoId.ToString(),
                ["Input.FechaInicio"] = "2026-02-01",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Interina).ToString()
            }));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected Redirect but got {response.StatusCode}. Location: {response.Headers.Location?.OriginalString ?? "(none)"}\nBody (first 1000 chars):\n{body[..Math.Min(1000, body.Length)]}");

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/ocupaciones/detalles/{id:D}", location, StringComparison.OrdinalIgnoreCase);

        var sent = Assert.Single(ocupacionClient.ActualizarCalls);
        Assert.Equal(id, sent.Id);
        Assert.Equal(personaId, sent.Request.PersonaId);
        Assert.Equal(puestoId, sent.Request.PuestoId);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-005 / Scenario: 409 PuestoOcupado → ModelState en PuestoId
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenPuestoOcupadoConflict_MapsErrorToPuestoId()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var originalPuestoId = Guid.NewGuid();
        var newPuestoId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: originalPuestoId);

        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(newPuestoId, "P-002", "Vendedor", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var conflictMessage = "El puesto cambió a ocupado por otra ocupación.";
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = current,
            ActualizarResult = OcupacionCommandResult.Failure(
                new OcupacionError(
                    ErrorCategoria.Conflict,
                    OcupacionErrorCodigo.PuestoOcupado,
                    conflictMessage))
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/editar/{id:D}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = newPuestoId.ToString(),
                ["Input.FechaInicio"] = "2026-02-01",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Interina).ToString()
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El error aparece en PuestoId.
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{OcupacionFormKeys.PuestoIdKey}""[^>]*>[\s\S]*?{Regex.Escape(conflictMessage)}[\s\S]*?</span>", RegexOptions.IgnoreCase),
            "Expected PuestoOcupado message to render in PuestoId field-validation span.");
    }

    // ──────────────────────────────────────────────────
    // POST con HttpRequestException → mensaje de transporte
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenHttpRequestException_ReloadsCatalogsAndShowsTransportMessage()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: puestoId);

        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdResult = current,
            ActualizarException = new HttpRequestException("api caída")
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/editar/{id:D}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = puestoId.ToString(),
                ["Input.FechaInicio"] = "2026-02-01",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Permanente).ToString()
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // POST bloqueado cuando la ocupación dejó de ser vigente
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenOcupacionBecameFinalizada_BlocksUpdate()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();

        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        // El handler de OcupacionApiClient.GetById devuelve vigente durante
        // el GET, pero Finalizada durante el POST (otra transición ocurrió).
        bool finalized = false;
        var vigente = SampleDto(id: id, personaId: personaId, puestoId: puestoId);
        var finalizada = SampleDto(id: id, personaId: personaId, puestoId: puestoId, estado: OcupacionEstado.Finalizada);
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdHandler = _ => finalized ? finalizada : vigente
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        finalized = true;

        var response = await lease.Client.PostAsync(
            $"/organizacion/ocupaciones/editar/{id:D}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = puestoId.ToString(),
                ["Input.FechaInicio"] = "2026-02-01",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Permanente).ToString()
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("finalizada", content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(ocupacionClient.ActualizarCalls);
    }

    // ──────────────────────────────────────────────────
    // GET con HttpRequestException → estado recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenHttpRequestException_ShowsTransportMessage()
    {
        var id = Guid.NewGuid();
        var ocupacionClient = new FakeOcupacionApiClient
        {
            ObtenerPorIdException = new HttpRequestException("api caída")
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Slice 3 / issue #219 — Migración de Ocupaciones/_Form
    // a la partial unificada `_PersonaCard` (modo editable) en
    // el flujo Edit. El form gana Email, Teléfono, badge de
    // Estado de Persona, además de los botones Quitar/Cambiar.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenVigenteWithPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: puestoId);
        var personaDto = new PersonaDto(
            Id: personaId,
            Legajo: "L-8800",
            Nombres: "Ana",
            Apellidos: "García",
            Email: "ana.garcia@example.com",
            TipoDocumentoId: Guid.NewGuid(),
            TipoDocumentoCodigo: "DNI",
            TipoDocumentoNombre: "Documento Nacional de Identidad",
            NumeroDocumento: "30123456",
            Telefono: "+54 11 5555-8800",
            IsActive: true);

        var personaClient = FakePersonaApiClient.WithPersonaList(personaDto);
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = current };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // PER-CARD-01/02: card enriquecida presente en Edit.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+54 11 5555-8800", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Activa", content, StringComparison.OrdinalIgnoreCase);

        // PER-CARD-01/04: Quitar y Cambiar presentes en editable.
        Assert.Contains("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
        // Razor preserva whitespace entre el texto del botón y el cierre,
        // así que el texto "Quitar" / "Cambiar" aparece antes de </button>
        // con whitespace de por medio.
        Assert.Matches(@"<button[^>]*data-usuario-persona-quitar[^>]*>\s*Quitar\s*</button>", content);
        Assert.Matches(@"<button[^>]*data-usuario-persona-buscar[^>]*>\s*Cambiar\s*</button>", content);

        // PER-CARD-05: botón Buscar apunta al modal compartido.
        Assert.Contains("data-bs-target=\"#ocupacion-persona-buscador-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"ocupacion-persona-buscador-modal\"", content, StringComparison.OrdinalIgnoreCase);

        // El hidden de binding JS persiste con el display correcto.
        Assert.Contains("data-usuario-persona-display-input", content, StringComparison.OrdinalIgnoreCase);

        // PersonaVinculada se enriquece vía GetByIdAsync.
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
    }

    [Fact]
    public async Task Get_Edit_WhenPersonaNotFound_RendersEmptyStateWithoutQuitarCambiar()
    {
        // PersonaId resuelto en el DTO de la Ocupacion pero
        // GetByIdAsync devuelve null. Como
        // `EnriquecerPersonaAsync` setea `PersonaDisplay = null` cuando
        // el DTO es null, el partial cae al "caso 6" (editable + DTO
        // null + sin FallbackDisplay): empty state con Buscar Persona.
        // Sin Quitar/Cambiar hasta que el DTO cargue.
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: Guid.NewGuid());
        var personaClient = new FakePersonaApiClient(); // GetByIdAsync = null
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = current };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));

        // Empty state con Buscar Persona, sin card ni Quitar.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Issue #266: Edit comparte el mismo bug de "validation message
    // pegado" que Create. El form reusa `_Form.cshtml`, así que el
    // hidden `Input_PersonaId` está presente y el script de dismissal
    // (`ocupaciones-form.js`) funciona tal cual. Este test protege
    // contra regresión de la referencia desde Edit.cshtml.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenMutationRole_LoadsPersonaChangeDismissScript()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: puestoId);

        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));
        var ocupacionClient = new FakeOcupacionApiClient { ObtenerPorIdResult = current };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"Input_PersonaId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/ocupaciones-form.js", content, StringComparison.OrdinalIgnoreCase);
    }
}