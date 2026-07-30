using System.Net;
using System.Web;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Ocupaciones;
using SGV.Tests.Web.Persona;
using SGV.Tests.Web.Usuario;
using SGV.Web.Integration.Ocupaciones;
using SGV.Web.Integration.Personas;
using SGV.Web.Integration.Usuarios;
using Xunit;

namespace SGV.Tests.Web.Tests;

/// <summary>
/// Smoke de las 4 vistas migradas a la partial <c>_PersonaCard</c>
/// (issue #219, cambio <c>reusable-persona-card</c>). Slice 4 / PR 4.
/// <para>
/// Este test NO reemplaza la cobertura unitaria y de integración que
/// cada vista tiene en su propia carpeta (Details/Edit/Create). Su
/// objetivo es ser un guard de smoke transversal: recorre las cuatro
/// migraciones y verifica que la partial se renderiza exactamente
/// UNA vez y en el <c>Mode</c> correcto por vista. Si en algún
/// cambio futuro alguien rompe la migración (sin querer duplica el
/// render, cambia el Mode, o desconecta el partial) este test
/// detecta la regresión con un único failure.
/// </para>
/// <para>
/// Modo de la partial inferido por selectores del DOM:
/// <list type="bullet">
///   <item><b>readonly</b> (Details): emite <c>data-usuario-persona-card</c> y NO emite <c>data-usuario-persona-quitar</c>.</item>
///   <item><b>editable</b> (Edit/Form): emite <c>data-usuario-persona-card</c> Y <c>data-usuario-persona-quitar</c>.</item>
/// </list>
/// </para>
/// </summary>
[Collection("WebIntegration")]
public sealed class PersonaCardMigrationSmokeTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonaCardMigrationSmokeTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────────
    // Usuarios / Details → readonly
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_UsuariosDetails_RendersPersonaCardOnceInReadonlyMode()
    {
        var personaId = Guid.NewGuid();
        var personaDto = new PersonaDto(
            Id: personaId,
            Legajo: "L-1001",
            Nombres: "Ana",
            Apellidos: "García",
            Email: "ana.garcia@example.com",
            null,
            TipoDocumentoCodigo: "DNI",
            TipoDocumentoNombre: "Documento Nacional de Identidad",
            NumeroDocumento: "30123456",
            Telefono: "+54 11 5555-0000",
            IsActive: true);
        var usuario = BuildUsuario("u-smoke-details", personaId, "Ana", "García");
        var usuarioApiClient = FakeUsuarioApiClient.WithUsuarioList(usuario);
        var personaApiClient = FakePersonaApiClient.WithPersonaList(personaDto);

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            usuarioApiClient, personaApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/detalle/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPersonaCardRenderedOnce(content, expectedMode: PersonaCardMode.Readonly);
    }

    // ──────────────────────────────────────────────────
    // Usuarios / Edit → editable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_UsuariosEdit_RendersPersonaCardOnceInEditableMode()
    {
        var personaId = Guid.NewGuid();
        var usuario = BuildUsuario("u-smoke-edit", personaId, "Ana", "García");
        var personaApiClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-1001", "Ana", "García", "ana@example.com",
                null, "DNI", "DNI", "30123456", "+54 11 5555-0000", true));

        await using var lease = await _fixture.CreateUsuarioLeaseAsync(
            FakeUsuarioApiClient.WithUsuarioList(usuario),
            personaApiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync($"/seguridad/usuarios/editar/{usuario.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPersonaCardRenderedOnce(content, expectedMode: PersonaCardMode.Editable);
    }

    // ──────────────────────────────────────────────────
    // Ocupaciones / Details → readonly
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_OcupacionesDetails_RendersPersonaCardOnceInReadonlyMode()
    {
        var personaId = Guid.NewGuid();
        var dto = FakeOcupacionApiClient.BuildDto(
            id: Guid.NewGuid(),
            personaId: personaId,
            personaNombre: "Ana García",
            puestoId: Guid.NewGuid(),
            puestoNombre: "Analista",
            estado: OcupacionEstado.Vigente);
        var ocupacionApiClient = new FakeOcupacionApiClient { ObtenerPorIdResult = dto };
        var personaApiClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-2001", "Ana", "García", "ana@example.com",
                null, "DNI", "DNI", "30123456", "+54 11 5555-0000", true));

        await using var lease = await _fixture.CreateOcupacionFormLeaseAsync(
            ocupacionApiClient, personaApiClient, new SGV.Tests.Web.Puesto.FakePuestosApiClient(),
            adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/detalles/{dto.Id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPersonaCardRenderedOnce(content, expectedMode: PersonaCardMode.Readonly);
    }

    // ──────────────────────────────────────────────────
    // Ocupaciones / Edit → editable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_OcupacionesEdit_RendersPersonaCardOnceInEditableMode()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var dto = FakeOcupacionApiClient.BuildDto(
            id: Guid.NewGuid(),
            personaId: personaId,
            personaNombre: "Ana García",
            puestoId: puestoId,
            puestoNombre: "Analista",
            estado: OcupacionEstado.Vigente);
        var ocupacionApiClient = new FakeOcupacionApiClient { ObtenerPorIdResult = dto };
        var personaApiClient = FakePersonaApiClient.WithPersonaList(
            new PersonaDto(personaId, "L-2001", "Ana", "García", "ana@example.com",
                null, "DNI", "DNI", "30123456", "+54 11 5555-0000", true));
        var puestoApiClient = new SGV.Tests.Web.Puesto.FakePuestosApiClient();

        await using var lease = await _fixture.CreateOcupacionFormLeaseAsync(
            ocupacionApiClient, personaApiClient, puestoApiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/ocupaciones/editar/{dto.Id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPersonaCardRenderedOnce(content, expectedMode: PersonaCardMode.Editable);
    }

    // ──────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────

    private enum PersonaCardMode { Readonly, Editable }

    /// <summary>
    /// Aserción transversal: la partial <c>_PersonaCard</c> se
    /// renderiza EXACTAMENTE una vez y con el <c>Mode</c> esperado.
    /// Modo readonly: NO emite <c>data-usuario-persona-quitar</c>.
    /// Modo editable: emite <c>data-usuario-persona-quitar</c>.
    /// </summary>
    private static void AssertPersonaCardRenderedOnce(string content, PersonaCardMode expectedMode)
    {
        var cardOccurrences = CountOccurrences(content, "data-usuario-persona-card");

        Assert.True(
            cardOccurrences == 1,
            $"La partial _PersonaCard debería renderizarse exactamente una vez, " +
            $"pero se encontró {cardOccurrences} ocurrencias de 'data-usuario-persona-card'. " +
            "Esto sugiere que la vista no usa la partial, la usa más de una vez, " +
            "o alguien reintrodujo markup duplicado.");

        var quitarOccurrences = CountOccurrences(content, "data-usuario-persona-quitar");
        var hasEditableMarkers = quitarOccurrences >= 1;

        switch (expectedMode)
        {
            case PersonaCardMode.Readonly:
                Assert.True(
                    !hasEditableMarkers,
                    $"La vista readonly no debería emitir 'data-usuario-persona-quitar', " +
                    $"pero se encontraron {quitarOccurrences} ocurrencias. " +
                    "Verificar que la partial se invoque con Mode='readonly' (default).");
                break;
            case PersonaCardMode.Editable:
                Assert.True(
                    hasEditableMarkers,
                    "La vista editable debería emitir 'data-usuario-persona-quitar' " +
                    "(Quitar/Cambiar visibles), pero no se encontró ninguna ocurrencia. " +
                    "Verificar que la partial se invoque con Mode='editable' vía ViewData.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expectedMode), expectedMode, null);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static UsuarioDto BuildUsuario(string id, Guid personaId, string nombres, string apellidos)
        => new(
            Id: id,
            PersonaId: personaId,
            UserName: "agarcia",
            Email: "ana@example.com",
            Roles: new[] { "Consultor" },
            Nombres: nombres,
            Apellidos: apellidos);
}
