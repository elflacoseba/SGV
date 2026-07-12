using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Tests del shell y de las páginas base del módulo web de Cargos.
/// PR 1 cubre: redirección anónima, presencia en el sidenav y seams.
/// Las pruebas end-to-end del listado, baja y detalle viven en PR 2 y PR 3.
/// </summary>
[Collection("WebIntegration")]
public sealed class CargoWebTests
{
    private readonly WebIntegrationFixture _fixture;

    public CargoWebTests(WebIntegrationFixture fixture) => _fixture = fixture;
    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/cargos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync($"/organizacion/cargos/detalles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticated_ExposesCargosModule()
    {
        await using var lease = await _fixture.CreateAuthOnlyLeaseAsync();

        var response = await lease.Client.GetAsync("/");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Contains("<span class=\"menu-text\">Cargos</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/cargos\"", content, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("<span class=\"menu-text\">Vacantes</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Reclutamiento</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Cat&aacute;logos</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Catálogos</span>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticated_ExposesHabilidadesModule()
    {
        await using var lease = await _fixture.CreateAuthOnlyLeaseAsync();

        var response = await lease.Client.GetAsync("/");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El grupo Habilidades debe aparecer con icono y submenú Listado + Nueva.
        Assert.Contains("<span class=\"menu-text\">Habilidades</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/habilidades\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/habilidades/crear\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ti ti-star", content, StringComparison.OrdinalIgnoreCase);

        // Y NO debe mostrar placeholders no especificados.
        Assert.DoesNotContain("<span class=\"menu-text\">Vacantes</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Reclutamiento</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Cat&aacute;logos</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Catálogos</span>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Sidenav_WhenAtHabilidadesIndex_MarksListadoActive()
    {
        // Spec CRITICAL-04: al estar en /organizacion/habilidades el item
        // Listado del submenú Habilidades debe llevar la clase "active",
        // pero el item Nueva NO debe llevarlo.
        await using var lease = await _fixture.CreateAuthOnlyLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/habilidades");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(
            LinkHasActive(content, "/organizacion/habilidades", classIncludesOnlyActive: true),
            "Listado should be marked active when at /organizacion/habilidades");

        Assert.False(
            LinkHasActive(content, "/organizacion/habilidades/crear"),
            "Nueva should NOT be active when at /organizacion/habilidades");
    }

    [Fact]
    public async Task Get_Sidenav_WhenAtHabilidadesCrear_MarksNuevaActive()
    {
        // Spec CRITICAL-04: al estar en /organizacion/habilidades/crear el
        // item Nueva del submenú Habilidades debe llevar la clase "active"
        // y el item Listado NO debe llevarla.
        await using var lease = await _fixture.CreateAuthOnlyLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/habilidades/crear");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(
            LinkHasActive(content, "/organizacion/habilidades/crear"),
            "Nueva should be marked active when at /organizacion/habilidades/crear");

        Assert.False(
            LinkHasActive(content, "/organizacion/habilidades"),
            "Listado should NOT be active when at /organizacion/habilidades/crear");
    }

    private static bool LinkHasActive(string content, string href, bool classIncludesOnlyActive = false)
    {
        // Localiza el <a ... href="..."> específico y devuelve true si su
        // atributo class contiene "active".
        var hrefToken = $"href=\"{href}\"";
        var idx = content.IndexOf(hrefToken, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        // Retrocede hasta encontrar la apertura '<a '.
        var anchorStart = content.LastIndexOf("<a ", idx, StringComparison.OrdinalIgnoreCase);
        if (anchorStart < 0)
        {
            return false;
        }

        var anchorEnd = content.IndexOf('>', idx);
        if (anchorEnd < 0)
        {
            return false;
        }

        var anchor = content[anchorStart..(anchorEnd + 1)];
        var hasActive = anchor.Contains(" active\"", StringComparison.OrdinalIgnoreCase)
            || anchor.Contains("\"active ", StringComparison.OrdinalIgnoreCase)
            || anchor.Contains("active ", StringComparison.OrdinalIgnoreCase)
            || anchor.Contains(" active ", StringComparison.OrdinalIgnoreCase);
        return hasActive;
    }
}
