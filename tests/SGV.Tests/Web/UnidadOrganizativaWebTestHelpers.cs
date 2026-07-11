using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

public sealed partial class UnidadOrganizativaWebTests
{
    private static PagedResult<UnidadOrganizativaDto> CreatePage(int page, int pageSize, int totalCount, params UnidadOrganizativaDto[] items)
        => new(items, totalCount, page, pageSize);

    private static UnidadOrganizativaDto CreateItem(string codigo, string nombre, string tipoNombre)
        => new(Guid.NewGuid(), codigo, nombre, Guid.NewGuid(), tipoNombre, null, null, null, null, null, null);

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

        public List<(Guid Id, ActualizarUnidadOrganizativaRequest Request)> UpdateCalls { get; } = [];

        public UnidadOrganizativaDeleteResult DeleteResult { get; set; } = new(false, HttpStatusCode.Conflict, null, null);

        public UnidadOrganizativaCommandResult ReactivateResult { get; set; } = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.NotFound, "NotImplemented", "Not yet implemented"));

        public UnidadOrganizativaCommandResult CommandResult { get; set; } = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.NotFound, "NotImplemented", "Not yet implemented"));

        public UnidadOrganizativaCommandResult? ChangeParentCommandResult { get; set; }

        public UnidadOrganizativaDto? GetByIdResult { get; set; }

        public IReadOnlyList<UnidadOrganizativaTreeNodeDto> TreeResult { get; set; } = [];

        public Exception? TreeException { get; set; }

        public IReadOnlyList<TipoUnidadOrganizativaDto> TiposResult { get; set; } = [];

        public List<Guid> ChangeParentCalls { get; } = [];

        public int TreeCalls { get; private set; }

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

        public Task<IReadOnlyList<UnidadOrganizativaDto>> GetAllActivasAsync(int pageSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UnidadOrganizativaDto>>([]);

        public Task<UnidadOrganizativaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(GetByIdResult);

        public Task<IReadOnlyList<UnidadOrganizativaTreeNodeDto>> GetTreeAsync(CancellationToken cancellationToken = default)
        {
            TreeCalls++;

            if (TreeException is not null)
            {
                throw TreeException;
            }

            return Task.FromResult(TreeResult);
        }

        public Task<IReadOnlyList<TipoUnidadOrganizativaDto>> GetTiposAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(TiposResult);

        public Task<UnidadOrganizativaCommandResult> CreateAsync(CrearUnidadOrganizativaRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(CommandResult);

        public Task<UnidadOrganizativaCommandResult> UpdateAsync(Guid id, ActualizarUnidadOrganizativaRequest request, CancellationToken cancellationToken = default)
        {
            UpdateCalls.Add((id, request));
            return Task.FromResult(CommandResult);
        }

        public Task<UnidadOrganizativaCommandResult> ChangeParentAsync(Guid id, CambiarUnidadPadreRequest request, CancellationToken cancellationToken = default)
        {
            ChangeParentCalls.Add(id);
            return Task.FromResult(ChangeParentCommandResult ?? CommandResult);
        }

        public Task<UnidadOrganizativaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalls.Add(id);
            return Task.FromResult(DeleteResult);
        }

        public Task<UnidadOrganizativaCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(ReactivateResult);
    }

    private sealed record DeleteScriptExecutionResult(
        int SubmitCount,
        bool PreventDefaultCalled,
        bool ShowCancelButton,
        string? ConfirmButtonText,
        string? CancelButtonText);
}
