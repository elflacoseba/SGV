using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
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

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Tests del módulo web de Cargos para PR 2: listado activo, baja lógica
/// confirmada y harness JS de <c>cargos-index.js</c>. Cubre los escenarios
/// "Cuando carga inicial", "Sin resultados", "Falla la consulta", "Confirmación
/// cancelada/confirmada" y "Baja exitosa/con conflicto".
/// </summary>
public sealed class CargoIndexPageTests
{
    // ──────────────────────────────────────────────
    // Task 2.1: carga inicial con tabla de cargos activos
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActiveCargosTable()
    {
        var first = CreateCargo("C-001", "Analista", "Descripción A", "Junior");
        var second = CreateCargo("C-002", "Líder de Proyecto", null, "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(first, second);

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cargos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Listado de cargos activos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Nombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Junior", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Nombre, content, StringComparison.OrdinalIgnoreCase);

        // Las filas deben exponer las acciones Detalle y Eliminar
        Assert.Contains($"/organizacion/cargos/detalles/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-cargo-delete-form", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-cargo-delete-button", content, StringComparison.OrdinalIgnoreCase);

        // El alcance de PR 2 prohibe create/edit/skills/eliminados
        Assert.DoesNotContain(">Crear<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Editar<", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Habilidades", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Eliminadas", content, StringComparison.OrdinalIgnoreCase);

        Assert.NotEmpty(apiClient.GetAllCalls);
    }

    // ──────────────────────────────────────────────
    // Task 2.2: listado sin resultados activos
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenSearchHasNoResults_ShowsEmptyState()
    {
        var apiClient = FakeCargoApiClient.WithCargoList();

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/cargos?search=zzz");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron cargos", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-cargo-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"zzz\"", content, StringComparison.OrdinalIgnoreCase);

        Assert.NotEmpty(apiClient.GetAllCalls);
    }

    // ──────────────────────────────────────────────
    // Task 2.3: fallo en la consulta del listado
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenQueryFails_ShowsVisibleError()
    {
        var apiClient = FakeCargoApiClient.WithFailure(new HttpRequestException("boom"));

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Task 2.4: harness de cargos-index.js (SweetAlert2)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecuteDeleteConfirmationScriptAsync(false);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.True(result.ShowCancelButton);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.True(result.ReverseButtons);
    }

    [Fact]
    public async Task DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce()
    {
        var result = await ExecuteDeleteConfirmationScriptAsync(true);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Sí, eliminar", result.ConfirmButtonText);
    }

    // ──────────────────────────────────────────────
    // Task 2.5: POST ?handler=Delete — éxito y conflicto
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndRefreshRemovesRow()
    {
        var toDelete = CreateCargo("DEL-01", "Analista a Eliminar", "Desc", "Junior");
        var remaining = CreateCargo("DEL-02", "Otro Cargo", null, "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(toDelete, remaining);
        apiClient.DeleteResult = new CargoDeleteResult(true, HttpStatusCode.NoContent, null, null);

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos?p=1&search=ana&sort=nombre_desc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = toDelete.Id.ToString(),
            ["page"] = "1",
            ["search"] = "ana",
            ["sort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(toDelete.Id, Assert.Single(apiClient.DeleteCalls));

        // Tras el borrado se vuelve al listado preservando filtros/sort
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("search=ana", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", location, StringComparison.OrdinalIgnoreCase);

        // Al refrescar el listado, el cargo eliminado ya no aparece
        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(toDelete.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(remaining.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible()
    {
        var cargo = CreateCargo("CONF-01", "Cargo con Puestos", "Desc", "Junior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.DeleteResult = new CargoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.Conflict,
            Code: "CargoConPuestosActivos",
            Message: "El cargo tiene puestos activos asociados.");

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos?search=c&sort=codigo_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = cargo.Id.ToString(),
            ["page"] = "1",
            ["search"] = "c",
            ["sort"] = "codigo_asc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("search=c", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=codigo_asc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo eliminar el cargo", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("El cargo tiene puestos activos asociados.", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cargo.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Delete_WhenNotFound_ShowsFeedbackAndKeepsRowVisible()
    {
        var cargo = CreateCargo("NF-01", "Cargo a Borrar", null, "Junior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.DeleteResult = new CargoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.NotFound,
            Code: "CargoNoEncontrado",
            Message: "El cargo no existe.");

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = cargo.Id.ToString(),
            ["page"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("ya no está disponible", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cargo.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Helpers de soporte
    // ──────────────────────────────────────────────

    private static CargoDto CreateCargo(string codigo, string nombre, string? descripcion, string? nivelNombre)
        => new(Guid.NewGuid(), codigo, nombre, descripcion, Guid.NewGuid(), nivelNombre);

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(FakeCargoApiClient apiClient)
    {
        var authHandler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1)))
            });

        var factory = new SgvWebApplicationFactory().WithOverrides(
            configureServices: services => services.Configure<SgvApiOptions>(options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            cargoApiClient: apiClient);

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
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SGV.Web/wwwroot/js/pages/cargos-index.js"));
        var harnessPath = Path.Combine(Path.GetTempPath(), $"cargo-delete-confirmation-{Guid.NewGuid():N}.cjs");

        await File.WriteAllTextAsync(harnessPath, $$"""
const { wireCargoDeleteConfirmation } = require({{JsonSerializer.Serialize(scriptPath)}});

let clickHandler = null;
let submitCount = 0;
let preventDefaultCalled = false;
let swalConfig = null;

const button = {
  getAttribute(name) {
    if (name === 'data-cargo-item-name') {
      return 'Analista';
    }

    if (name === 'data-cargo-item-code') {
      return 'C-001';
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
    return selector === '[data-cargo-delete-button]' ? button : null;
  },
  submit() {
    submitCount += 1;
  }
};

const root = {
  querySelectorAll(selector) {
    return selector === '[data-cargo-delete-form]' ? [form] : [];
  }
};

const Swal = {
  fire(config) {
    swalConfig = config;
    return Promise.resolve({ isConfirmed: {{(isConfirmed ? "true" : "false")}} });
  }
};

async function main() {
  wireCargoDeleteConfirmation(root, Swal);

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
    reverseButtons: Boolean(swalConfig && swalConfig.reverseButtons),
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
            return result!;
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

    private sealed record DeleteScriptExecutionResult(
        int SubmitCount,
        bool PreventDefaultCalled,
        bool ShowCancelButton,
        bool ReverseButtons,
        string? ConfirmButtonText,
        string? CancelButtonText);
}