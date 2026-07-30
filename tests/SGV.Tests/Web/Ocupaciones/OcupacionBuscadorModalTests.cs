using System.Net;
using System.Text.RegularExpressions;
using System.Web;
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
/// DOM contract del modal reutilizable de Personas invocado desde los
/// formularios de Create/Edit de Ocupaciones. Cubre REQ-OCC-PER-BUSC-01
/// (card + botón Buscar en lugar de <c>&lt;select&gt;</c>),
/// REQ-OCC-PER-BUSC-03 (modal declara <c>data-solo-sin-usuario="false"</c>)
/// y REQ-OCC-PER-BUSC-06 (estados del modal reutilizados del partial
/// compartido). Issue #216.
/// </summary>
[Collection("WebIntegration")]
public sealed class OcupacionBuscadorModalTests
{
    private readonly WebIntegrationFixture _fixture;

    public OcupacionBuscadorModalTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private static PersonaDto SamplePersona(string nombre = "Ana", string apellido = "García") =>
        new(Guid.NewGuid(), "L-001", nombre, apellido, null, null, null, null, null, null, true);

    private static PuestoDto SamplePuesto() =>
        new(Guid.NewGuid(), "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);

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
    // REQ-OCC-PER-BUSC-03: el modal root declara
    // `data-solo-sin-usuario="false"` (modal con id
    // `ocupacion-persona-buscador-modal`).
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Modal_DeclaresSoloSinUsuarioFalse()
    {
        await using var lease = await CreateLeaseAsync(new FakeOcupacionApiClient());

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var modalMatch = Regex.Match(
            content,
            @"<div(?=[^>]*id=""ocupacion-persona-buscador-modal"")[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(modalMatch.Success, "Modal root must be present with id='ocupacion-persona-buscador-modal'.");
        Assert.Contains("data-solo-sin-usuario=\"false\"", modalMatch.Value, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-PER-BUSC-01: ausencia de <select name="Input.PersonaId"> +
    // presencia de la card con botón Buscar.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_RendersPersonaCardSinSelectPersonaId()
    {
        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient(),
            FakePersonaApiClient.WithPersonaList(SamplePersona()),
            FakePuestosApiClient.WithPuestoList(SamplePuesto()));

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El <select> de Persona NO debe estar presente.
        Assert.DoesNotMatch(
            new Regex(@"<select[^>]*name=""Input\.PersonaId""", RegexOptions.IgnoreCase),
            content);

        // El campo se preserva en un hidden input (Issue #216 / contrato
        // del modelo).
        Assert.Matches(
            @"<input(?=[^>]*name=""Input\.PersonaId"")(?=[^>]*type=""hidden"")[^>]*>",
            content);

        // Slice 3 / issue #219: la card sale de la partial unificada
        // `_PersonaCard` (modo editable). En estado vacío sin PersonaId
        // la partial cae al "caso 6" (editable + DTO null + sin
        // FallbackDisplay): contenedor display vacío + empty state con
        // botón Buscar Persona — sin card div ni Quitar hasta que el
        // usuario seleccione una persona vía el modal.
        Assert.Contains("data-usuario-persona-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-display", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar Persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_RendersPersonaCardPrepopulated()
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
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Card presente + texto de la persona.
        Assert.Contains("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("García, Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DNI 12345678", content, StringComparison.OrdinalIgnoreCase);

        // El hidden input del modal está poblado.
        Assert.Matches(
            $@"<input(?=[^>]*name=""Input\.PersonaId"")(?=[^>]*value=""{personaId:D}"")[^>]*type=""hidden""[^>]*>",
            content);
    }

    [Fact]
    public async Task Modal_DeclaraDataUsuarioPersonaModalConApiUrlConsulta()
    {
        await using var lease = await CreateLeaseAsync(new FakeOcupacionApiClient());

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-usuario-persona-modal", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-api-url=\"/api/v1/personas/consulta\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Modal_EstadosInicialEmptyLoadingErrorReutilizados()
    {
        await using var lease = await CreateLeaseAsync(new FakeOcupacionApiClient());

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Estados del modal reutilizados del partial compartido
        // (REQ-OCC-PER-BUSC-06 / REQ-USB-05).
        Assert.Contains("Ingresá un texto para buscar personas.", content, StringComparison.Ordinal);
        Assert.Contains("No se encontraron personas con ese criterio.", content, StringComparison.Ordinal);
        Assert.Contains("No se pudo conectar con el servidor. Reintentá.", content, StringComparison.Ordinal);
        Assert.Contains("data-usuario-persona-estado-inicial", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-estado-empty", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-estado-loading", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-usuario-persona-estado-error", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // REQ-OCC-PER-BUSC-07: el script `usuario-persona-buscador.js` se
    // incluye desde Create.cshtml y Edit.cshtml en la sección scripts.
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_IncluyeScriptBuscadorEnSeccionScripts()
    {
        await using var lease = await CreateLeaseAsync(new FakeOcupacionApiClient());

        var response = await lease.Client.GetAsync("/organizacion/ocupaciones/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "<script src=\"/js/pages/usuario-persona-buscador.js\"></script>",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_IncluyeScriptBuscadorEnSeccionScripts()
    {
        var id = Guid.NewGuid();
        var personaId = Guid.NewGuid();
        var current = SampleDto(id: id, personaId: personaId, puestoId: Guid.NewGuid());

        await using var lease = await CreateLeaseAsync(
            new FakeOcupacionApiClient { ObtenerPorIdResult = current },
            FakePersonaApiClient.WithPersonaList(
                new PersonaDto(personaId, "L-001", "Ana", "García", null, null, null, null, null, null, true)));

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "<script src=\"/js/pages/usuario-persona-buscador.js\"></script>",
            content,
            StringComparison.OrdinalIgnoreCase);
    }
}