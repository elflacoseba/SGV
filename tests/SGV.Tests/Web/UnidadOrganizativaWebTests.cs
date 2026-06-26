using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

public sealed class UnidadOrganizativaWebTests
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

        var response = await client.GetAsync("/organizacion/unidades-organizativas");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersShellMenuAndInitialTable()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 24, CreateItem("A01", "Rectorado", "Institución"), CreateItem("B01", "Dirección de Talento", "Dirección")));

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unidades Organizativas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span class=\"menu-text\">Home</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<span class=\"menu-text\">Unidades Organizativas</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Vacantes</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Catálogos</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span class=\"menu-text\">Reclutamiento</span>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Página 1 de 3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rectorado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dirección de Talento", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-uo-delete-button", content, StringComparison.OrdinalIgnoreCase);
        var initialQuery = Assert.Single(apiClient.QueryCalls);
        Assert.Null(initialQuery.Search);
        Assert.Null(initialQuery.Sort);
    }

    [Fact]
    public async Task Get_Index_WhenSearchHasNoResults_ShowsEmptyState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/unidades-organizativas?search=zzz");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron unidades organizativas", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rectorado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("zzz", apiClient.QueryCalls[0].Search);
    }

    [Fact]
    public async Task Get_Index_WhenQueryFails_ShowsVisibleErrorAndKeepsSearchAvailable()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithFailure(new HttpRequestException("boom"));

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_WhenChangingPage_ShowsRequestedPageAndCurrentIndicator()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25, CreateItem("C01", "Facultad de Ingeniería", "Facultad"), CreateItem("C02", "Facultad de Ciencias", "Facultad")));

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/unidades-organizativas?page=2");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Página 2 de 3", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Facultad de Ingeniería", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Facultad de Ciencias", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, apiClient.QueryCalls[0].Page);
    }

    [Fact]
    public async Task Get_Index_WhenSortingVisiblePage_ReordersRowsAndKeepsCurrentPage()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 25,
                CreateItem("C03", "Beta", "Facultad"),
                CreateItem("C01", "Ágora", "Facultad"),
                CreateItem("C02", "Gamma", "Facultad")));

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/unidades-organizativas?page=2&sort=nombre_desc");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, apiClient.QueryCalls[0].Page);
        Assert.Equal("nombre_desc", apiClient.QueryCalls[0].Sort);

        var gammaIndex = content.IndexOf("Gamma", StringComparison.OrdinalIgnoreCase);
        var betaIndex = content.IndexOf("Beta", StringComparison.OrdinalIgnoreCase);
        var agoraIndex = content.IndexOf("Ágora", StringComparison.OrdinalIgnoreCase);

        Assert.True(gammaIndex >= 0 && betaIndex >= 0 && agoraIndex >= 0, "Expected sorted rows to be rendered.");
        Assert.True(gammaIndex < betaIndex && betaIndex < agoraIndex, "Rows were not rendered in descending name order.");
        Assert.Contains("Página 2 de 3", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Index_RendersDeleteConfirmationHookWithoutExecutingDelete()
    {
        var item = CreateItem("D01", "Secretaría General", "Secretaría");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 1, item));

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/unidades-organizativas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-uo-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(item.Id.ToString(), content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.DeleteCalls);
    }

    [Fact]
    public async Task DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecuteDeleteConfirmationScriptAsync(false);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.True(result.ShowCancelButton);
        Assert.Equal("Cancelar", result.CancelButtonText);
    }

    [Fact]
    public async Task DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce()
    {
        var result = await ExecuteDeleteConfirmationScriptAsync(true);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Sí, eliminar", result.ConfirmButtonText);
    }

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndRefreshRemovesRow()
    {
        var itemToDelete = CreateItem("E01", "Dirección Académica", "Dirección");
        var remainingItem = CreateItem("E02", "Dirección Financiera", "Dirección");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(2, 10, 11, itemToDelete),
            CreatePage(2, 10, 10),
            CreatePage(1, 10, 10, remainingItem));
        apiClient.DeleteResult = new UnidadOrganizativaDeleteResult(true, HttpStatusCode.NoContent, null, null);

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?page=2&search=dir&sort=nombre_desc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = itemToDelete.Id.ToString(),
            ["page"] = "2",
            ["search"] = "dir",
            ["sort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(itemToDelete.Id, Assert.Single(apiClient.DeleteCalls));
        Assert.Equal("/organizacion/unidades-organizativas?search=dir&sort=nombre_desc", response.Headers.Location?.OriginalString);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("La unidad organizativa se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dirección Académica", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dirección Financiera", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible()
    {
        var item = CreateItem("F01", "Departamento Legal", "Departamento");
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(
            CreatePage(1, 10, 1, item),
            CreatePage(1, 10, 1, item));
        apiClient.DeleteResult = new UnidadOrganizativaDeleteResult(false, HttpStatusCode.Conflict, "unidad-organizativa-en-uso", "La unidad organizativa tiene dependencias activas.");

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/unidades-organizativas?search=dep&sort=nombre_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/unidades-organizativas?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = item.Id.ToString(),
            ["page"] = "1",
            ["search"] = "dep",
            ["sort"] = "nombre_asc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/organizacion/unidades-organizativas?search=dep&sort=nombre_asc", response.Headers.Location?.OriginalString);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo eliminar la unidad organizativa", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("La unidad organizativa tiene dependencias activas.", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Departamento Legal", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    private static PagedResult<UnidadOrganizativaDto> CreatePage(int page, int pageSize, int totalCount, params UnidadOrganizativaDto[] items)
        => new(items, totalCount, page, pageSize);

    private static UnidadOrganizativaDto CreateItem(string codigo, string nombre, string tipoNombre)
        => new(Guid.NewGuid(), codigo, nombre, Guid.NewGuid(), tipoNombre, null, null, null, null);

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(FakeUnidadOrganizativaApiClient apiClient)
    {
        var authHandler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            unidadOrganizativaApiClient: apiClient);

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

    private static async Task<DeleteScriptExecutionResult> ExecuteDeleteConfirmationScriptAsync(bool isConfirmed)
    {
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js"));
        var harnessPath = Path.Combine(Path.GetTempPath(), $"uo-delete-confirmation-{Guid.NewGuid():N}.cjs");

        await File.WriteAllTextAsync(harnessPath, $$"""
const { wireUnidadOrganizativaDeleteConfirmation } = require({{JsonSerializer.Serialize(scriptPath)}});

let clickHandler = null;
let submitCount = 0;
let preventDefaultCalled = false;
let swalConfig = null;

const button = {
  getAttribute(name) {
    if (name === 'data-uo-item-name') {
      return 'Secretaría General';
    }

    return null;
  },
  addEventListener(type, handler) {
    if (type === 'click') {
      clickHandler = handler;
    }
  }
};

const form = {
  querySelector(selector) {
    return selector === '[data-uo-delete-button]' ? button : null;
  },
  submit() {
    submitCount += 1;
  }
};

const root = {
  querySelectorAll(selector) {
    return selector === '[data-uo-delete-form]' ? [form] : [];
  }
};

const Swal = {
  fire(config) {
    swalConfig = config;
    return Promise.resolve({ isConfirmed: {{(isConfirmed ? "true" : "false")}} });
  }
};

async function main() {
  wireUnidadOrganizativaDeleteConfirmation(root, Swal);

  if (!clickHandler) {
    throw new Error('Delete confirmation click handler was not wired.');
  }

  clickHandler({
    preventDefault() {
      preventDefaultCalled = true;
    }
  });

  await Promise.resolve();
  await Promise.resolve();

  process.stdout.write(JSON.stringify({
    submitCount,
    preventDefaultCalled,
    showCancelButton: Boolean(swalConfig && swalConfig.showCancelButton),
    confirmButtonText: swalConfig ? swalConfig.confirmButtonText : null,
    cancelButtonText: swalConfig ? swalConfig.cancelButtonText : null
  }));
}

main().catch(error => {
  process.stderr.write(error.stack || String(error));
  process.exit(1);
});
""");

        try
        {
            var startInfo = new ProcessStartInfo("node", $"\"{harnessPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var standardOutput = await process.StandardOutput.ReadToEndAsync();
            var standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"Node harness failed with exit code {process.ExitCode}: {standardError}");

            var result = JsonSerializer.Deserialize<DeleteScriptExecutionResult>(standardOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            Assert.NotNull(result);
            return result;
        }
        finally
        {
            if (File.Exists(harnessPath))
            {
                File.Delete(harnessPath);
            }
        }
    }

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class FakeUnidadOrganizativaApiClient : IUnidadOrganizativaApiClient
    {
        private readonly Queue<PagedResult<UnidadOrganizativaDto>> _pages = new();
        private readonly Exception? _queryException;

        private FakeUnidadOrganizativaApiClient(IEnumerable<PagedResult<UnidadOrganizativaDto>> pages, Exception? queryException)
        {
            foreach (var page in pages)
            {
                _pages.Enqueue(page);
            }

            _queryException = queryException;
        }

        public List<UnidadOrganizativaListQuery> QueryCalls { get; } = [];

        public List<Guid> DeleteCalls { get; } = [];

        public UnidadOrganizativaDeleteResult DeleteResult { get; set; } = new(false, HttpStatusCode.Conflict, null, null);

        public static FakeUnidadOrganizativaApiClient WithPages(params PagedResult<UnidadOrganizativaDto>[] pages)
            => new(pages, null);

        public static FakeUnidadOrganizativaApiClient WithFailure(Exception exception)
            => new([], exception);

        public Task<PagedResult<UnidadOrganizativaDto>> QueryAsync(UnidadOrganizativaListQuery query, CancellationToken cancellationToken = default)
        {
            QueryCalls.Add(query);

            if (_queryException is not null)
            {
                throw _queryException;
            }

            Assert.NotEmpty(_pages);
            return Task.FromResult(_pages.Dequeue());
        }

        public Task<UnidadOrganizativaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalls.Add(id);
            return Task.FromResult(DeleteResult);
        }
    }

    private sealed record DeleteScriptExecutionResult(
        int SubmitCount,
        bool PreventDefaultCalled,
        bool ShowCancelButton,
        string? ConfirmButtonText,
        string? CancelButtonText);
}
