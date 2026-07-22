using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Seguridad;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Cobertura del botón "Habilidades" agregado a <c>Pages/Personas/Details.cshtml</c>
/// en Slice 3b del change <c>implementa-persona-habilidades</c>. Verifica los
/// tres escenarios del delta <c>persona-management</c> (R-PM-01):
/// <list type="number">
///   <item>Detalle activo + admin → botón visible con href correcto.</item>
///   <item>Detalle no consultable (404 recuperable) → botón NO visible.</item>
///   <item>Detalle activo + no admin → botón NO visible (la frontera de
///   autorización sigue bloqueando el subrecurso).</item>
/// </list>
/// </summary>
[Collection("WebIntegration")]
public sealed class DetailsHabilidadesButtonTests
{
    private readonly WebIntegrationFixture _fixture;

    public DetailsHabilidadesButtonTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // Patrón regex que matchea el <a> con href al subrecurso persona-skill
    // y la palabra "Habilidades" como contenido del botón. Evita matchear
    // links de la nav global (e.g. /organizacion/habilidades) y mantiene la
    // aserción observable contra el render del Details.
    private static readonly Regex HabilidadesButtonRegex = new(
        @"<a\b[^>]*href=""[^""]*?/personas/[0-9a-f-]{36}/habilidades""[^>]*>[\s\S]*?Habilidades[\s\S]*?</a>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public async Task Details_ActivePersona_Admin_RendersHabilidadesButtonWithCorrectHref()
    {
        // R-PM-01 escenario 1: persona activa + admin → botón visible con
        // href al subrecurso persona-skill.
        var persona = new PersonaDto(
            Guid.NewGuid(), "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/personas/detalle/{persona.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            HabilidadesButtonRegex.IsMatch(content),
            "Expected the Habilidades button to render with href to /personas/{id}/habilidades.");
    }

    [Fact]
    public async Task Details_NotFound_DoesNotRenderHabilidadesButton()
    {
        // R-PM-01 escenario 2: estado no consultable → acción oculta.
        var apiClient = FakePersonaApiClient.WithPersonaList();
        var missingId = Guid.NewGuid();

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync($"/personas/detalle/{missingId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            HabilidadesButtonRegex.IsMatch(content),
            "Expected the Habilidades button to NOT render when persona is not consultable.");
    }

    [Fact]
    public async Task Details_ActivePersona_NonAdmin_DoesNotRenderHabilidadesButton()
    {
        // R-PM-01 escenario 3: usuario autenticado sin rol Administrador →
        // la acción hacia habilidades MUST NOT renderizarse, aunque la
        // persona esté activa. El subrecurso sigue bloqueado por la
        // frontera de autorización vigente.
        var persona = new PersonaDto(
            Guid.NewGuid(), "L-001", "Ana", "García",
            null, null, null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: false);

        var response = await lease.Client.GetAsync($"/personas/detalle/{persona.Id}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            HabilidadesButtonRegex.IsMatch(content),
            "Expected the Habilidades button to NOT render when user is not administrator.");
    }
}
