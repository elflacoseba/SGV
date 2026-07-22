using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Cobertura del botón "Habilidades" agregado a <c>Pages/Personas/Index.cshtml</c>
/// en PR A del change <c>agrega-navegacion-personas-habilidades</c>. Verifica los
/// escenarios del delta <c>persona-management</c>:
/// <list type="number">
///   <item>Fila activa + admin → botón visible con href al subrecurso persona-skill
///   y el icono <c>ti ti-stars</c> (REQ-PM-NEW, REQ-PM-NEW-ADMIN).</item>
///   <item>Fila activa + no admin → botón NO visible: la frontera de autorización
///   preserva el gating admin-only del subrecurso (REQ-PM-NEW-ADMIN).</item>
///   <item>El href del botón conserva <c>page</c>/<c>search</c>/<c>sort</c> del
///   listado para permitir el regreso al contexto original
///   (REQ-PM-NEW-CONTEXT).</item>
/// </list>
/// Espejo de <see cref="DetailsHabilidadesButtonTests"/> sobre la fila del
/// listado (no la card de detalle).
/// </summary>
[Collection("WebIntegration")]
public sealed class PersonasIndexHabilidadesButtonTests
{
    private readonly WebIntegrationFixture _fixture;

    public PersonasIndexHabilidadesButtonTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // Patrón regex: <a> con href a /personas/{guid}/habilidades. Matchea el
    // anchor completo del botón para que un cambio menor (por ejemplo
    // agregar otro botón con el mismo href) rompa este test de forma
    // específica. El icono ti ti-stars se valida por separado en el test
    // del escenario positivo.
    //
    // El href puede llevar query string opcional (?p=&search=&sort=&returnStatus=)
    // porque el helper BuildHabilidadesRouteValues preserva el contexto del
    // listado (REQ-PM-NEW-CONTEXT). El grupo (?:\?[^"]*)? admite ese sufijo.
    private static readonly Regex HabilidadesButtonAnchorRegex = new(
        @"<a\b[^>]*href=""[^""]*?/personas/[0-9a-f-]{36}/habilidades(?:\?[^""]*)?""[^>]*>[\s\S]*?</a>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public async Task Index_ActiveRow_Admin_RendersHabilidadesButton()
    {
        // REQ-PM-NEW + REQ-PM-NEW-ADMIN escenario 1:
        // persona activa + admin → botón visible con href al subrecurso.
        var persona = new PersonaDto(
            Guid.NewGuid(), "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            HabilidadesButtonAnchorRegex.IsMatch(content),
            "Expected the Habilidades button to render with href to /personas/{id}/habilidades for admin in active row.");

        // El icono ti ti-stars debe estar presente en el render (consistencia
        // visual con el botón equivalente en Details.cshtml línea 82-86).
        Assert.Contains("ti ti-stars", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_ActiveRow_NonAdmin_HidesHabilidadesButton()
    {
        // REQ-PM-NEW-ADMIN escenario 2: usuario autenticado sin rol
        // Administrador → la acción hacia habilidades MUST NOT renderizarse,
        // aunque la persona esté activa. El subrecurso sigue bloqueado
        // por la frontera de autorización vigente en PersonaHabilidades.
        var persona = new PersonaDto(
            Guid.NewGuid(), "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: false);

        var response = await lease.Client.GetAsync("/personas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            HabilidadesButtonAnchorRegex.IsMatch(content),
            "Expected the Habilidades button to NOT render when user is not administrator.");
    }

    [Fact]
    public async Task Index_WhenContextFiltersPresent_HabilidadesHrefPreservesThem()
    {
        // REQ-PM-NEW-CONTEXT: BuildHabilidadesRouteValues MUST conservar
        // page, search, sort y status del listado al construir el href
        // hacia PersonaHabilidades para permitir volver al contexto
        // original. El test verifica que el href generado incluye los
        // filtros del listado como query string del subrecurso.
        //
        // El search "García" matchea la persona sembrada para que la fila
        // se renderice; sin coincidencia el listado cae al estado vacío
        // (Mock del QueryAsync del FakePersonaApiClient filtra
        // server-side) y el botón no se expondría.
        //
        // p=1 (no p=3) porque el fake tiene sólo 1 persona y pageSize=10;
        // con p=3 el Skip(20) deja la página vacía.
        var persona = new PersonaDto(
            Guid.NewGuid(), "L-001", "Juan", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync(
            "/personas?p=1&search=garc%C3%ADa&sort=apellidos_asc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El href generado por BuildHabilidadesRouteValues debe apuntar al
        // subrecurso con el id correcto y propagar los filtros del
        // listado como query string (?p=1&search=...&sort=apellidos_asc).
        var hrefPattern = new Regex(
            $@"href=""(?<href>[^""]*?/personas/{persona.Id}/habilidades\?[^""]*?)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var match = hrefPattern.Match(content);

        Assert.True(
            match.Success,
            "Expected the Habilidades href to /personas/{id}/habilidades with context query string. " +
            $"Content head (first 800 chars): {content.Substring(0, Math.Min(800, content.Length))}");

        var href = match.Groups["href"].Value;

        Assert.Contains("p=1", href, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=", href, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=apellidos_asc", href, StringComparison.OrdinalIgnoreCase);
    }
}