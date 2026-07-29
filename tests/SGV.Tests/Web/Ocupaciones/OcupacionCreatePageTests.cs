using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using SGV.Tests.Web.Puesto;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using SGV.Web.Pages.Organizacion.Ocupaciones;
using Xunit;

namespace SGV.Tests.Web.Ocupaciones;

/// <summary>
/// Tests del PageModel de <c>/organizacion/ocupaciones/crear</c> para
/// Slice 3a del change #208: render del form con catálogos Persona/Puesto,
/// PRG al Index tras éxito, mapeo de 409 (<c>PersonaYPuestoOcupados</c> /
/// <c>PuestoOcupado</c>) a los campos del <c>ModelState</c>, errores 400
/// con <c>FieldErrors</c>, 401/403/404, transporte recuperable,
/// pre-carga desde query string, gate admin, y re-render seguro del input
/// tras 409.
/// </summary>
[Collection("WebIntegration")]
public sealed class OcupacionCreatePageTests
{
    private readonly WebIntegrationFixture _fixture;

    public OcupacionCreatePageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────────
    // Helpers — builders determinísticos
    // ──────────────────────────────────────────────────

    private static PersonaDto SamplePersona(string nombre = "Ana", string apellido = "García") =>
        new(Guid.NewGuid(), "L-001", nombre, apellido, null, null, null, null, null, null, true);

    private static PuestoDto SamplePuesto(string codigo = "P-001", string nombre = "Analista") =>
        new(Guid.NewGuid(), codigo, nombre, null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);

    private static OcupacionDto SampleDto(
        Guid? personaId = null,
        Guid? puestoId = null,
        string personaNombre = "Ana García",
        string puestoNombre = "Analista",
        OcupacionEstado estado = OcupacionEstado.Vigente) =>
        FakeOcupacionApiClient.BuildDto(
            personaId: personaId,
            puestoId: puestoId,
            personaNombre: personaNombre,
            puestoNombre: puestoNombre,
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
    // REQ-OCC-FORM-001 / Scenario: Render — los 5 campos visibles
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAdmin_RendersAllFiveFieldsWithCatalogs()
    {
        var persona = SamplePersona();
        var puesto = SamplePuesto();
        var personaClient = FakePersonaApiClient.WithPersonaList(persona);
        var puestosClient = FakePuestosApiClient.WithPuestoList(puesto);
        var ocupacionClient = new FakeOcupacionApiClient();

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains("name=\"Input.PersonaId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.PuestoId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Single(
            Regex.Matches(
                content,
                @"<select\b[^>]*\bname=""Input\.PuestoId""",
                RegexOptions.IgnoreCase));
        Assert.Contains("name=\"Input.FechaInicio\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.TipoAsignacion\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Observaciones\"", content, StringComparison.OrdinalIgnoreCase);

        // Catálogo de puestos sigue siendo por dropdown (Issue #216 sólo
        // toca el campo PersonaId).
        Assert.Contains("Analista", content, StringComparison.OrdinalIgnoreCase);
        Assert.Single(puestosClient.GetAllCalls);

        // Issue #216 (OCC-PER-BUSC-02): el catálogo completo de personas ya
        // NO se carga; en su lugar la card se enriquece con GetByIdAsync
        // cuando hay persona precargada. Sin query string, no se consulta.
        Assert.Empty(personaClient.GetAllCalls);
        Assert.Empty(personaClient.GetByIdCalls);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-PER-BUSC-02 / OCC-PER-BUSC-05 — IOcupacionForm enriquecido
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_LoadCatalogsAsync_NoLlamaPersonaGetAllAsync()
    {
        var personaClient = FakePersonaApiClient.WithPersonaList(SamplePersona());
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(personaClient.GetAllCalls);
        Assert.Empty(personaClient.GetByIdCalls);
    }

    [Fact]
    public async Task Get_Create_WithPersonaIdQuery_InvocaGetByIdYPopulaCard()
    {
        var personaId = Guid.NewGuid();
        var persona = new PersonaDto(personaId, "L-001", "Ana", "García", "ana@example.com", Guid.NewGuid(), "DNI", "DNI", "12345678", null, true);
        var personaClient = FakePersonaApiClient.WithPersonaList(persona);
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?personaId={personaId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
        Assert.Empty(personaClient.GetAllCalls);
    }

    [Fact]
    public async Task Get_Create_WithUnknownPersonaId_NoLanzaYQuedaVacia()
    {
        var unknownId = Guid.NewGuid();
        var personaClient = FakePersonaApiClient.WithPersonaList();
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?personaId={unknownId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(unknownId, Assert.Single(personaClient.GetByIdCalls));
        Assert.Empty(personaClient.GetAllCalls);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-001 / Scenario: Pre-carga desde query string
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenQueryHasPreloadedPersonaIdAndPuestoId_RendersSelectedValues()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var persona = new PersonaDto(personaId, "L-002", "Juan", "Pérez", null, null, null, null, null, null, true);
        var puesto = new PuestoDto(puestoId, "P-X", "Seleccionado", null, Guid.NewGuid(), "X", Guid.NewGuid(), "X", null);
        var personaClient = FakePersonaApiClient.WithPersonaList(persona);
        var puestosClient = FakePuestosApiClient.WithPuestoList(puesto);
        var ocupacionClient = new FakeOcupacionApiClient();

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?personaId={personaId:D}&puestoId={puestoId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Issue #216 (OCC-PER-BUSC-05): PersonaId precargado por query
        // string se renderea como hidden input, NO como option selected.
        Assert.Matches(
            $@"<input(?=[^>]*name=""{OcupacionFormKeys.PersonaIdKey}"")(?=[^>]*value=""{personaId:D}"")[^>]*type=""hidden""[^>]*>",
            content);

        // PuestoId sigue siendo un <select> (Issue #216 sólo toca PersonaId).
        Assert.Contains(
            $"<option selected=\"selected\" value=\"{puestoId:D}\"",
            content,
            StringComparison.OrdinalIgnoreCase);

        // El API de personas se invoca exactamente una vez vía GetByIdAsync.
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-001 / Scenario: Usuario no-admin → Forbid → AccessDenied
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenNotAdmin_RedirectsToAccessDenied()
    {
        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            adminRole: false);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/error/403",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/auth/sign-in",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-006 / Scenario: POST éxito → PRG al Index con feedback
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToIndexWithFeedback()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var newOcupacionId = Guid.NewGuid();
        var ocupacionClient = new FakeOcupacionApiClient
        {
            CrearResult = OcupacionCommandResult.Success(SampleDto(
                personaId: personaId,
                puestoId: puestoId,
                personaNombre: "Ana García",
                puestoNombre: "Analista"))
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/organizacion/ocupaciones/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = puestoId.ToString(),
                ["Input.FechaInicio"] = "2026-02-01",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Permanente).ToString(),
                ["Input.Observaciones"] = string.Empty
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("/organizacion/ocupaciones", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/crear", location, StringComparison.OrdinalIgnoreCase);

        // El request al API debe llevar los valores correctos.
        var sent = Assert.Single(ocupacionClient.CrearCalls);
        Assert.Equal(personaId, sent.PersonaId);
        Assert.Equal(puestoId, sent.PuestoId);
        Assert.Equal(new DateOnly(2026, 2, 1), sent.FechaInicio);
        Assert.Equal(OcupacionTipoAsignacion.Permanente, sent.TipoAsignacion);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-005 / Scenario: 409 PersonaYPuestoOcupados → ambos campos
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenPersonaYPuestoOcupadosConflict_MapsErrorToBothFields()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var conflictMessage = "El par persona-puesto ya tiene una ocupación vigente.";
        var ocupacionClient = new FakeOcupacionApiClient
        {
            CrearResult = OcupacionCommandResult.Failure(
                new OcupacionError(
                    ErrorCategoria.Conflict,
                    OcupacionErrorCodigo.PersonaYPuestoOcupados,
                    conflictMessage))
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/organizacion/ocupaciones/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = puestoId.ToString(),
                ["Input.FechaInicio"] = "2026-02-01",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Permanente).ToString()
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje de conflicto debe aparecer en los spans de validación de
        // PersonaId y PuestoId (no en un alert general).
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{OcupacionFormKeys.PersonaIdKey}""[^>]*>[\s\S]*?{Regex.Escape(conflictMessage)}[\s\S]*?</span>", RegexOptions.IgnoreCase),
            "Expected PersonaYPuestoOcupados message to render in PersonaId field-validation span.");
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{OcupacionFormKeys.PuestoIdKey}""[^>]*>[\s\S]*?{Regex.Escape(conflictMessage)}[\s\S]*?</span>", RegexOptions.IgnoreCase),
            "Expected PersonaYPuestoOcupados message to render in PuestoId field-validation span.");

        // Issue #216 (OCC-PER-BUSC-02): la recarga post-conflict ya no
        // invoca GetAllAsync de Persona. Puesto sí se recarga.
        Assert.Empty(personaClient.GetAllCalls);
        Assert.Equal<int>(2, puestosClient.GetAllCalls.Count);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-005 / Scenario: 409 PuestoOcupado → sólo PuestoId
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenPuestoOcupadoConflict_MapsErrorToPuestoIdOnly()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var conflictMessage = "El puesto ya tiene otra ocupación vigente.";
        var ocupacionClient = new FakeOcupacionApiClient
        {
            CrearResult = OcupacionCommandResult.Failure(
                new OcupacionError(
                    ErrorCategoria.Conflict,
                    OcupacionErrorCodigo.PuestoOcupado,
                    conflictMessage))
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/organizacion/ocupaciones/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = puestoId.ToString(),
                ["Input.FechaInicio"] = "2026-02-01",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Interina).ToString()
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje aparece en PuestoId.
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{OcupacionFormKeys.PuestoIdKey}""[^>]*>[\s\S]*?{Regex.Escape(conflictMessage)}[\s\S]*?</span>", RegexOptions.IgnoreCase),
            "Expected PuestoOcupado message to render in PuestoId field-validation span.");
        // El span de PersonaId NO debe contener el mensaje (sólo el placeholder
        // inicial o vacío).
        var personaSpanRegex = new Regex(
            $@"<span[^>]*data-valmsg-for=""{OcupacionFormKeys.PersonaIdKey}""[^>]*>([\s\S]*?)</span>",
            RegexOptions.IgnoreCase);
        var personaSpanMatch = personaSpanRegex.Match(content);
        Assert.True(personaSpanMatch.Success, "PersonaId field-validation span must be present.");
        Assert.DoesNotContain(conflictMessage, personaSpanMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-004 / Scenario: 400 con FieldErrors → ModelState por clave
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenValidationFailureWithFieldErrors_AppliesEachErrorToItsKey()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var ocupacionClient = new FakeOcupacionApiClient
        {
            CrearResult = OcupacionCommandResult.Failure(
                new OcupacionError(ErrorCategoria.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["PersonaId"] = new[] { "La persona está inactiva." }
                })
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/organizacion/ocupaciones/crear",
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

        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{OcupacionFormKeys.PersonaIdKey}""[^>]*>[\s\S]*?La persona está inactiva\.[\s\S]*?</span>", RegexOptions.IgnoreCase),
            "Expected FieldError message to render in PersonaId field-validation span.");
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-006 / Scenario: Transporte recuperable → mensaje general
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenHttpRequestException_ReloadsCatalogsAndShowsTransportMessage()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var ocupacionClient = new FakeOcupacionApiClient
        {
            CrearException = new HttpRequestException("api caída")
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/organizacion/ocupaciones/crear",
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

        // El mensaje de transporte debe ser visible (no en un span de campo).
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);

        // El form debe seguir visible con los valores enviados (preserva input).
        Assert.Contains("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);

        // Issue #216 (OCC-PER-BUSC-02): la recarga post-transporte ya no
        // invoca GetAllAsync de Persona. Puesto sí se recarga.
        Assert.Empty(personaClient.GetAllCalls);
        Assert.Equal<int>(2, puestosClient.GetAllCalls.Count);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-004 / Scenario: Re-render seguro — input preservado tras 409
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenConflict_PreservesUserInputInForm()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var personaClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true));
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        var ocupacionClient = new FakeOcupacionApiClient
        {
            CrearResult = OcupacionCommandResult.Failure(
                new OcupacionError(
                    ErrorCategoria.Conflict,
                    OcupacionErrorCodigo.PersonaYPuestoOcupados,
                    "Duplicado."))
        };

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync(
            "/organizacion/ocupaciones/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = personaId.ToString(),
                ["Input.PuestoId"] = puestoId.ToString(),
                ["Input.FechaInicio"] = "2026-03-15",
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Temporal).ToString(),
                ["Input.Observaciones"] = "Mi observación importante"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados.
        Assert.Contains("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mi observación importante", content, StringComparison.OrdinalIgnoreCase);
        // La fecha debe estar preservada en el input.
        Assert.Contains("value=\"2026-03-15\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-FORM-001 / Scenario: Modelo inválido → ModelState por campo
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenModelStateInvalid_DoesNotCallApiAndShowsValidationErrors()
    {
        var personaClient = FakePersonaApiClient.WithPersonaList(SamplePersona());
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());
        var ocupacionClient = new FakeOcupacionApiClient();

        await using var lease = await CreateLeaseAsync(ocupacionClient, personaClient, puestosClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var antiforgery = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        // Falta FechaInicio (Required).
        var response = await lease.Client.PostAsync(
            "/organizacion/ocupaciones/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["Input.PersonaId"] = Guid.NewGuid().ToString(),
                ["Input.PuestoId"] = Guid.NewGuid().ToString(),
                ["Input.TipoAsignacion"] = ((int)OcupacionTipoAsignacion.Permanente).ToString()
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Empty(ocupacionClient.CrearCalls);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Fecha de inicio", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Issue #216 (OCC-PER-BUSC-05): precarga con personaId que existe
    // pero el API de GetByIdAsync lanza HttpRequestException — el form
    // debe seguir visible y caer a card vacía sin error fatal.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WithPersonaIdQueryAndGetByIdTransportFailure_FallsBackToEmpty()
    {
        var personaId = Guid.NewGuid();
        var personaClient = new FakePersonaApiClient
        {
            GetByIdException = new HttpRequestException("persona caído")
        };
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?personaId={personaId:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.PersonaId\"", content, StringComparison.OrdinalIgnoreCase);
    }
}