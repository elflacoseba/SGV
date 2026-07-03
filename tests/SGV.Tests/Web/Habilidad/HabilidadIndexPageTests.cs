using System.Net;
using System.Net.Http.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests del módulo web de Habilidades para PR 3A: listado activo, baja lógica
/// confirmada y harness JS de <c>habilidades-index.js</c>.
/// </summary>
public sealed class HabilidadIndexPageTests : IClassFixture<HabilidadWebTestFixture>
{
    private readonly HabilidadWebTestFixture _fixture;

    public HabilidadIndexPageTests(HabilidadWebTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/organizacion/habilidades");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable()
    {
        var first = HabilidadWebTestFixture.BuildHabilidadDto("H-001", "Liderazgo", "Desc A", "Conductual");
        var second = HabilidadWebTestFixture.BuildHabilidadDto("H-002", "Programación", null, "Técnica");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(first, second);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Habilidades", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Listado de habilidades activas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Nombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Nombre, content, StringComparison.OrdinalIgnoreCase);

        // Las filas deben exponer las acciones Detalle, Editar y Eliminar.
        Assert.Contains($"/organizacion/habilidades/detalles/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/organizacion/habilidades/editar/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-habilidad-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-habilidad-delete-button", content, StringComparison.OrdinalIgnoreCase);

        // En vista activas: NO se exponen skills ni acciones de eliminadas.
        Assert.DoesNotContain("data-habilidad-reactivate-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Listado de habilidades eliminadas", content, StringComparison.OrdinalIgnoreCase);

        // Server-side: QueryAsync se invoca en vez de GetAllAsync.
        Assert.Empty(apiClient.GetAllCalls);
        Assert.NotEmpty(apiClient.QueryCalls);
    }

    [Fact]
    public async Task Get_Index_WhenSearchHasNoResults_ShowsEmptyState()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades?search=zzz");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron habilidades", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-habilidad-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"zzz\"", content, StringComparison.OrdinalIgnoreCase);

        Assert.NotEmpty(apiClient.QueryCalls);
    }

    [Fact]
    public async Task Get_Index_WhenQueryFails_ShowsVisibleError()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.QueryException = new HttpRequestException("boom");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenSegmentoEliminadas_RendersReactivarButtonOnly()
    {
        var first = HabilidadWebTestFixture.BuildHabilidadDto("H-DEL", "Eliminada", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(first);
        apiClient.QueryHandler = _ => new PagedResult<HabilidadDto>([first], 1, 1, 20);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Listado de habilidades eliminadas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-habilidad-reactivate-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-habilidad-reactivate-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-habilidad-delete-form", content, StringComparison.OrdinalIgnoreCase);

        // Anti-drift: el Index de Habilidades NO debe tener data-cargo-* ni "Nivel".
        Assert.DoesNotContain("Nivel", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-cargo-", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsPreservingFilters()
    {
        var first = HabilidadWebTestFixture.BuildHabilidadDto("H-001", "Liderazgo", "Desc A", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(first);
        apiClient.DeleteResult = new HabilidadDeleteResult(true, HttpStatusCode.NoContent, null, null);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, "/organizacion/habilidades");

        var formPost = await PostDeleteAsync(client, token, first.Id, page: 1, search: "lid", sort: "nombre_desc");

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains("/organizacion/habilidades", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=lid", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Id, apiClient.DeleteCalls);
    }

    [Fact]
    public async Task Post_Delete_WhenConflict_RedirectsWithErrorMessage()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.DeleteResult = new HabilidadDeleteResult(false, HttpStatusCode.Conflict, "CodigoDuplicado", "Conflicto");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, "/organizacion/habilidades");

        var formPost = await PostDeleteAsync(client, token, Guid.NewGuid(), page: 1, search: null, sort: null);

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains("/organizacion/habilidades", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var followUp = await client.GetAsync(formPost.Headers.Location!);
        var content = HttpUtility.HtmlDecode(await followUp.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo eliminar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Reactivate_WhenSuccessful_RedirectsToActivas()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.ReactivateResult = HabilidadCommandResult.Success(
            HabilidadWebTestFixture.BuildHabilidadDto("H-001", "Liderazgo", "Desc A", "Conductual"));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, "/organizacion/habilidades?status=eliminadas");

        var formPost = await PostReactivateAsync(client, token, Guid.NewGuid(), page: 1, search: null, sort: null);

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains("/organizacion/habilidades", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status=eliminadas", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.ReactivateCalls);
    }

    [Fact]
    public async Task Post_Reactivate_WhenCodigoDuplicado_ReturnsConflictAndStaysOnEliminadas()
    {
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.ReactivateResult = HabilidadCommandResult.Failure(
            new SGV.Aplicacion.Habilidades.Comandos.HabilidadError(
                SGV.Aplicacion.Habilidades.Comandos.HabilidadErrorType.Conflict,
                "CodigoDuplicado",
                "Ya existe una habilidad activa con el mismo código."));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);
        var token = await GetAntiforgeryTokenAsync(client, "/organizacion/habilidades?status=eliminadas");

        var formPost = await PostReactivateAsync(client, token, Guid.NewGuid(), page: 1, search: null, sort: "nombre_asc", status: "eliminadas");

        Assert.Equal(HttpStatusCode.Redirect, formPost.StatusCode);
        Assert.Contains("status=eliminadas", formPost.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var followUp = await client.GetAsync(formPost.Headers.Location!);
        var content = HttpUtility.HtmlDecode(await followUp.Content.ReadAsStringAsync());
        Assert.Contains("No se pudo reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ya existe una habilidad activa con el mismo código.", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_NoExponePlaceholdersDeCargosNiFiltroPorNivel()
    {
        // Anti-drift centralizado para Slice 3A.
        var first = HabilidadWebTestFixture.BuildHabilidadDto("H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(first);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-cargo-", content, StringComparison.OrdinalIgnoreCase);
        // El texto "Nivel" como columna o filtro no debe figurar en el catálogo maestro.
        Assert.DoesNotContain("Nivel", content, StringComparison.OrdinalIgnoreCase);
        // No hay filtro por nivel en la UI ni en el form de búsqueda.
        Assert.DoesNotContain("name=\"nivel\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"nivelId\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenSwitchingToEliminadasWithFilters_PreservesSearchAndSort()
    {
        // Spec CRITICAL-05 escenario 3: al cambiar de segmento de activas a
        // eliminadas, la búsqueda y el orden vigentes se preservan en el
        // request al API. Lo verificamos observando la query exacta que se
        // envía al IHabilidadApiClient cuando navegamos a
        // /organizacion/habilidades?status=eliminadas&search=lider&sort=nombre_desc.
        var first = HabilidadWebTestFixture.BuildHabilidadDto("H-DEL", "Eliminada", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(first);
        apiClient.QueryHandler = _ => new PagedResult<HabilidadDto>([first], 1, 1, 20);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync(
            "/organizacion/habilidades?status=eliminadas&search=lider&sort=nombre_desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotEmpty(apiClient.QueryCalls);
        var query = apiClient.QueryCalls[0];
        Assert.Equal("lider", query.Search);
        Assert.Equal("nombre_desc", query.Sort);
        Assert.Equal("eliminadas", query.Status);
    }

    [Fact]
    public async Task Get_Index_WhenAtListadoWithP2_ToggleLinkGeneratesP1AndPreservesFilters()
    {
        // Spec CRITICAL-05 escenario 3: al estar en /organizacion/habilidades
        // con búsqueda y orden aplicados, el link "Ver eliminadas" del
        // submenú debe resetear la página al cambiar de segmento y
        // preservar los filtros vigentes.
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.QueryHandler = _ => new PagedResult<HabilidadDto>([], 0, 1, 20);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync(
            "/organizacion/habilidades?search=lider&sort=nombre_desc&p=2");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Localizar el anchor de "Eliminadas" generado por
        // Url.Page("/Organizacion/Habilidades/Index", BuildToggleSegmentoRouteValues("eliminadas"))
        // y verificar que su href contiene p=1 (reset) y los filtros vigentes.
        var eliminadasAnchor = ExtractAnchorForHrefContaining(content, "status=eliminadas", "search=lider", "sort=nombre_desc");
        Assert.NotNull(eliminadasAnchor);

        Assert.Contains("status=eliminadas", eliminadasAnchor!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=lider", eliminadasAnchor!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", eliminadasAnchor!, StringComparison.OrdinalIgnoreCase);
        // Página reseteada a 1.
        Assert.Contains("p=1", eliminadasAnchor!, StringComparison.OrdinalIgnoreCase);
        // Y NO debe contener p=2 en ese anchor.
        Assert.DoesNotContain("p=2", eliminadasAnchor!, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractAnchorForHrefContaining(string content, params string[] requiredTokens)
    {
        var idx = 0;
        while ((idx = content.IndexOf("<a ", idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var anchorEnd = content.IndexOf('>', idx);
            if (anchorEnd < 0) break;
            var anchor = content[idx..(anchorEnd + 1)];
            var hrefStart = anchor.IndexOf("href=\"", StringComparison.OrdinalIgnoreCase);
            if (hrefStart >= 0)
            {
                var hrefValueStart = hrefStart + "href=\"".Length;
                var hrefValueEnd = anchor.IndexOf('"', hrefValueStart);
                if (hrefValueEnd > 0)
                {
                    var hrefValue = anchor[hrefValueStart..hrefValueEnd];
                    if (requiredTokens.All(t => hrefValue.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    {
                        return hrefValue;
                    }
                }
            }
            idx = anchorEnd + 1;
        }
        return null;
    }

    private static async Task<HttpResponseMessage> PostDeleteAsync(
        HttpClient client,
        string antiforgeryToken,
        Guid id,
        int page,
        string? search,
        string? sort)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(id.ToString()), "id" },
            { new StringContent(page.ToString()), "page" },
            { new StringContent(search ?? string.Empty), "search" },
            { new StringContent(sort ?? string.Empty), "sort" },
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" }
        };
        return await client.PostAsync("/organizacion/habilidades?handler=Delete", form);
    }

    private static async Task<HttpResponseMessage> PostReactivateAsync(
        HttpClient client,
        string antiforgeryToken,
        Guid id,
        int page,
        string? search,
        string? sort,
        string? status = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(id.ToString()), "id" },
            { new StringContent(page.ToString()), "page" },
            { new StringContent(search ?? string.Empty), "search" },
            { new StringContent(sort ?? string.Empty), "sort" },
            { new StringContent(status ?? string.Empty), "status" },
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" }
        };
        return await client.PostAsync("/organizacion/habilidades?handler=Reactivate", form);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await HabilidadWebTestFixture.ExtractAntiforgeryTokenAsync(response);
    }
}