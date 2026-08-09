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
using SGV.Contracts.Vacantes.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Persona;
using SGV.Tests.Web.Puesto;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Organizacion;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Vacantes;
using SGV.Tests.Web.Vacantes;
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
        IVacanteApiClient? vacante = null,
        bool adminRole = true)
    {
        return await _fixture.CreateOcupacionFormLeaseAsync(
            ocupacion,
            persona ?? new FakePersonaApiClient(),
            puestos ?? new FakePuestosApiClient(),
            vacante ?? new FakeVacanteApiClient(),
            adminRole);
    }

    [Fact]
    public async Task Get_Create_WithoutPuestoId_MuestraHintInicial()
    {
        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            puestos: FakePuestosApiClient.WithPuestoList(SamplePuesto()));

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Seleccione un Puesto para verificar su disponibilidad", content, StringComparison.OrdinalIgnoreCase);
    }

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

    // ──────────────────────────────────────────────────
    // Slice 3 / issue #219 — Migración de Ocupaciones/_Form
    // a la partial unificada `_PersonaCard` (modo editable). El
    // form gana Email, Teléfono y badge de Estado de Persona,
    // además de los botones Quitar y Cambiar (binding JS vigente).
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WithPreloadedPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar()
    {
        var personaId = Guid.NewGuid();
        var personaDto = new PersonaDto(
            Id: personaId,
            Legajo: "L-7711",
            Nombres: "Ana",
            Apellidos: "García",
            Email: "ana.garcia@example.com",
            TipoDocumentoId: Guid.NewGuid(),
            TipoDocumentoCodigo: "DNI",
            TipoDocumentoNombre: "Documento Nacional de Identidad",
            NumeroDocumento: "30123456",
            Telefono: "+54 11 5555-7711",
            IsActive: true);
        var personaClient = FakePersonaApiClient.WithPersonaList(personaDto);
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?personaId={personaId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // PER-CARD-01/02: la card enriquecida emite contenedor + card.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);

        // PER-CARD-02: Email, Teléfono y badge de Estado presentes.
        Assert.Contains("ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+54 11 5555-7711", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Activa", content, StringComparison.OrdinalIgnoreCase);

        // PER-CARD-01/04: Quitar y Cambiar presentes en editable.
        Assert.Contains("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-buscar", content, StringComparison.OrdinalIgnoreCase);
        // Razor preserva whitespace entre el texto del botón y el cierre,
        // así que el texto "Quitar" / "Cambiar" aparece antes de </button>
        // con whitespace de por medio.
        Assert.Matches(@"<button[^>]*data-usuario-persona-quitar[^>]*>\s*Quitar\s*</button>", content);
        Assert.Matches(@"<button[^>]*data-usuario-persona-buscar[^>]*>\s*Cambiar\s*</button>", content);

        // PER-CARD-05: el botón Buscar apunta al modal con id compartido.
        Assert.Contains("data-bs-target=\"#ocupacion-persona-buscador-modal\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"ocupacion-persona-buscador-modal\"", content, StringComparison.OrdinalIgnoreCase);

        // El hidden del display (binding JS) está presente.
        Assert.Contains("data-usuario-persona-display-input", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WithoutPersonaId_RendersEditableEmptyCardWithBuscarPersona()
    {
        var personaClient = FakePersonaApiClient.WithPersonaList();
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // PER-CARD-01 (caso 6): empty state puro → contenedor display
        // vacío + empty visible con botón Buscar Persona.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);

        // Empty state NO debe traer Quitar (es empty puro).
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WithUnknownPersonaId_RendersEmptyStateWithoutQuitarCambiar()
    {
        // PersonaId precargado pero GetByIdAsync devuelve null. Como
        // `EnriquecerPersonaAsync` setea `PersonaDisplay = null` cuando
        // el DTO es null (no hay fallback display derivado para
        // Ocupaciones como en Usuarios), el partial cae al "caso 6"
        // (editable + DTO null + sin FallbackDisplay): empty state con
        // Buscar Persona. Sin Quitar/Cambiar hasta que el DTO cargue.
        var personaId = Guid.NewGuid();
        var personaClient = new FakePersonaApiClient(); // sin DTOS → GetByIdAsync = null
        var puestosClient = FakePuestosApiClient.WithPuestoList(SamplePuesto());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?personaId={personaId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El API de Persona se invocó con el id resuelto.
        Assert.Equal(personaId, Assert.Single(personaClient.GetByIdCalls));

        // Empty state con Buscar Persona, sin card ni Quitar.
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana.garcia@example.com", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Issue #266: el `<span asp-validation-for="Input.PersonaId">`
    // muestra "Debe escoger una persona" hasta el próximo submit. El
    // handler que lo limpia al cambiar el hidden (elegir o quitar
    // persona desde el modal) vive en
    // `/js/pages/ocupaciones-form.js`; este test protege contra
    // regresión de la referencia (no del comportamiento JS, que
    // requiere navegador).
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenMutationRole_LoadsPersonaChangeDismissScript()
    {
        var persona = SamplePersona();
        var puesto = SamplePuesto();
        var personaClient = FakePersonaApiClient.WithPersonaList(persona);
        var puestosClient = FakePuestosApiClient.WithPuestoList(puesto);

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient);

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"Input_PersonaId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/ocupaciones-form.js", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // T2.1 / T2.2 / T2.3 — change `invertir-flujo-cubrir` (S2).
    // El `Create` de Ocupación acepta `?vacanteId={guid}` y resuelve la
    // Vacante vía `IVacanteApiClient.ObtenerPorIdAsync`. El estado de la
    // Vacante determina el render: Abierta → form rendereado con PuestoId
    // bloqueado + hint; Cubierta → error legible "Esta Vacante ya está
    // cubierta."; Inexistente → "La Vacante no existe.".
    // Spec: web-ocupaciones-crear-editar / REQ-OCC-FORM-001.
    // ──────────────────────────────────────────────────

    private static VacanteDetailDto SampleVacanteAbierta(
        Guid vacanteId,
        Guid puestoId,
        string puestoNombre = "Analista",
        string estadoVacanteNombre = "Abierta")
        => FakeVacanteApiClient.BuildDetail(
            id: vacanteId,
            puestoId: puestoId,
            puestoNombre: puestoNombre,
            estadoVacanteNombre: estadoVacanteNombre);

    [Fact]
    public async Task Get_Create_WithVacanteIdAbierta_RendereaFormConPuestoIdBloqueadoYVgHint()
    {
        var vacanteId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var vacanteApi = new FakeVacanteApiClient
        {
            ObtenerPorIdResult = SampleVacanteAbierta(vacanteId, puestoId, "Analista Senior")
        };
        var personaClient = FakePersonaApiClient.WithPersonaList(SamplePersona());
        var puestosClient = FakePuestosApiClient.WithPuestoList(
            new PuestoDto(puestoId, "P-001", "Analista Senior", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null));

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            personaClient,
            puestosClient,
            vacanteApi);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?vacanteId={vacanteId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // La Vacante se resolvió vía el API client.
        Assert.Equal(vacanteId, Assert.Single(vacanteApi.ObtenerPorIdCalls));

        // El form se renderea (no se cortocircuita a error).
        Assert.Contains("name=\"Input.VacanteId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nueva ocupación", content, StringComparison.OrdinalIgnoreCase);

        // Hidden que propaga el id hacia el POST.
        Assert.Matches(
            $@"<input(?=[^>]*name=""Input\.VacanteId"")(?=[^>]*value=""{vacanteId:D}"")[^>]*type=""hidden""[^>]*>",
            content);

        // El dropdown de PuestoId queda bloqueado. Independientemente
        // de cómo Razor serialice el boolean (puede omitir el atributo
        // cuando es false, o emitir `disabled="True"` cuando es true),
        // exigimos que aparezca el literal `disabled` en el mismo
        // `<select>` que tenga `name="Input.PuestoId"`.
        var selectMatch = Regex.Match(
            content,
            @"<select\b[^>]*\bname=""Input\.PuestoId""[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(selectMatch.Success, "Expected <select name=\"Input.PuestoId\"> to be rendered.");
        Assert.Contains(
            "disabled",
            selectMatch.Value,
            StringComparison.OrdinalIgnoreCase);

        // Hint informativo mencionando la Vacante.
        Assert.Contains("Esta Vacante", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analista Senior", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WithVacanteIdCubierta_MuestraError_VacanteYaCubierta()
    {
        var vacanteId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var vacanteApi = new FakeVacanteApiClient
        {
            ObtenerPorIdResult = SampleVacanteAbierta(vacanteId, puestoId, estadoVacanteNombre: "Cubierta")
        };

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            FakePersonaApiClient.WithPersonaList(SamplePersona()),
            FakePuestosApiClient.WithPuestoList(SamplePuesto()),
            vacanteApi);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?vacanteId={vacanteId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El error legible aparece en el HTML.
        Assert.Contains("Esta Vacante ya está cubierta.", content, StringComparison.OrdinalIgnoreCase);

        // El form NO se renderea: el hidden de VacanteId solo está presente
        // cuando se muestra el form completo; verificar ausencia.
        Assert.DoesNotContain("name=\"Input.VacanteId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            @"<select\b[^>]*\bname=""Input\.PuestoId""[^>]*\bdisabled\b",
            content);
    }

    [Fact]
    public async Task Get_Create_WithVacanteInexistente_MuestraError_VacanteNoExiste()
    {
        var vacanteId = Guid.NewGuid();
        var vacanteApi = new FakeVacanteApiClient
        {
            ObtenerPorIdResult = null
        };

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            FakePersonaApiClient.WithPersonaList(SamplePersona()),
            FakePuestosApiClient.WithPuestoList(SamplePuesto()),
            vacanteApi);

        var response = await lease.Client.GetAsync(
            $"/organizacion/ocupaciones/crear?vacanteId={vacanteId:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El API client fue invocado con el id.
        Assert.Equal(vacanteId, Assert.Single(vacanteApi.ObtenerPorIdCalls));

        // El error legible aparece en el HTML.
        Assert.Contains("La Vacante no existe.", content, StringComparison.OrdinalIgnoreCase);

        // El form NO se renderea.
        Assert.DoesNotContain("name=\"Input.VacanteId\"", content, StringComparison.OrdinalIgnoreCase);
    }
}