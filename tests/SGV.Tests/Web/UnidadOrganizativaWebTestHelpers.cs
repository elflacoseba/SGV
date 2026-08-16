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
using SGV.Contracts.Seguridad;
using SGV.Contracts.Seguridad.Usuarios;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web;

/// <summary>
/// Suite web de Unidad Organizativa. Se une a <c>[Collection("WebIntegration")]</c>
/// y comparte un único <see cref="WebIntegrationFixture"/> raíz. El lease
/// devuelto por <see cref="CreateAuthenticatedClientAsync"/> retiene la factory
/// derivada y el <see cref="TestSentinel"/> hasta su <c>await using</c>; los
/// call sites consumen <c>lease.Client</c> dentro del scope.
/// </summary>
[Collection("WebIntegration")]
public sealed partial class UnidadOrganizativaWebTests
{
    private readonly WebIntegrationFixture _fixture;

    public UnidadOrganizativaWebTests(WebIntegrationFixture fixture) => _fixture = fixture;

    private static PagedResult<UnidadOrganizativaDto> CreatePage(int page, int pageSize, int totalCount, params UnidadOrganizativaDto[] items)
        => new(items, totalCount, page, pageSize);

    private static UnidadOrganizativaDto CreateItem(string codigo, string nombre, string tipoNombre)
        => new(Guid.NewGuid(), codigo, nombre, Guid.NewGuid(), tipoNombre, null, null, null, null, null, null);

    /// <summary>
    /// Lease autenticado contra el módulo de Unidad Organizativa. Construye la
    /// factory derivada con <see cref="SgvWebApplicationFactory.WithOverrides"/>
    /// sobre la raíz compartida del fixture y la envuelve en un
    /// <see cref="WebClientLease"/> con sentinel. Se hace así —y no vía el
    /// helper <see cref="WebIntegrationFixture.CreateUnidadOrganizativaLeaseAsync"/>—
    /// porque ese helper tipa su argumento con el fake público de Puesto, que
    /// sólo implementa dos métodos de la interfaz; los tests de UO consumen un
    /// fake privado con cobertura completa de la API (Query/Delete/Update/Reactivate/etc.).
    /// La cadena <c>root → override → lease</c> mantiene el contrato de
    /// propiedad: la root queda retenida por el fixture y la derivada queda
    /// retenida por el lease, sin factories huérfanas. El bootstrap pasa por
    /// <see cref="WebIntegrationFixture.CreateLeaseWithBootstrapAsync"/> para
    /// compartir el cleanup del composite infra (PR 2b-4 review).
    /// </summary>
    private Task<WebClientLease> CreateAuthenticatedClientAsync(FakeUnidadOrganizativaApiClient apiClient)
    {
        var authHandler = new WebTestBuilders.RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(AdminJwtTestHelper.BuildUserJwt(), DateTimeOffset.UtcNow.AddHours(1)))
            });

        return _fixture.CreateLeaseWithBootstrapAsync(
            f => f.WithOverrides(
                configureServices: services =>
                {
                    services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test");
                    // Alineado con WebIntegrationFixture.ConfigureBaseUrl (commit 11ff7bb5):
                    // AuthSessionFactory valida el JWT firmado por el auth handler con
                    // la signing key de IOptions<JwtOptions>; si el host de test no la
                    // sobreescribe, queda la del appsettings.Development.json — que
                    // coincide con AdminJwtTestHelper.SigningKey pero no con el resto
                    // del pipeline (Issuer/Audience) cuando se ejecuta fuera de
                    // Development. Forzar las tres opciones garantiza que el POST
                    // /auth/sign-in emita 302 Found y setee la cookie, en lugar de
                    // caer en la rama de "token inválido" del SignIn page model y
                    // retornar 200 OK — que es la causa de los 48 fallos UO
                    // observados (assertions downstream contra 302 Found y contra
                    // antiforgery token ausente en la redirect response).
                    services.Configure<JwtOptions>(o =>
                    {
                        o.SigningKey = AdminJwtTestHelper.SigningKey;
                        o.Issuer = AdminJwtTestHelper.Issuer;
                        o.Audience = AdminJwtTestHelper.Audience;
                    });
                },
                authApiHandler: authHandler,
                unidadOrganizativaApiClient: apiClient),
            WebIntegrationFixture.AuthenticateClientAsync);
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

        public UnidadOrganizativaArbolResponse TreeResult { get; set; } = new([], []);

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

        public Task<UnidadOrganizativaArbolResponse> GetTreeAsync(CancellationToken cancellationToken = default)
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
