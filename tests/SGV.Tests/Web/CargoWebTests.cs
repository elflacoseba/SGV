using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Tests del shell y de las páginas base del módulo web de Cargos.
/// PR 1 cubre: redirección anónima, presencia en el sidenav y seams.
/// Las pruebas end-to-end del listado, baja y detalle viven en PR 2 y PR 3.
/// </summary>
public sealed class CargoWebTests
{
    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/organizacion/cargos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Details_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync($"/organizacion/cargos/detalles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Sidenav_WhenAuthenticated_ExposesCargosModule()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/");
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
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/");
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
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/organizacion/habilidades");
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
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/organizacion/habilidades/crear");
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

    private static async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: handler);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var signInResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(signInResponse);

        var loginResponse = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!"
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(content, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

        Assert.True(match.Success, "Antiforgery token was not rendered.");
        return match.Groups[1].Value;
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}