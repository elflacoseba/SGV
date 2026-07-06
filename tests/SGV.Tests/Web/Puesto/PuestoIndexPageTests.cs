using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Web;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Tests del módulo web de Puestos para PR 2: listado activo, baja lógica
/// confirmada y harness JS de <c>puestos-index.js</c>. Cubre los escenarios
/// "Carga inicial con 6 columnas", "Toggle Eliminadas deshabilitado",
/// "Búsqueda con/sin resultados", "Error visible", "POST Delete éxito/409/404",
/// "POST Reactivate éxito/409 por código" y "Harness SweetAlert2".
/// Espejo de <c>CargoIndexPageTests</c>.
/// </summary>
public sealed class PuestoIndexPageTests : IClassFixture<PuestoWebTestFixture>
{
    private readonly PuestoWebTestFixture _fixture;

    public PuestoIndexPageTests(PuestoWebTestFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────────
    // Tarea 2.1.1: render inicial del listado activo con 6 columnas
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAuthenticated_RendersActivePuestosTable()
    {
        var first = PuestoWebTestFixture.BuildPuestoDto("P-001", "Analista", "Desc A");
        var second = PuestoWebTestFixture.BuildPuestoDto("P-002", "Líder de Proyecto", null);
        var apiClient = FakePuestosApiClient.WithPuestoList(first, second);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Puestos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Listado de puestos activos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Nombre, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ventas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vendedor", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Codigo, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Nombre, content, StringComparison.OrdinalIgnoreCase);

        // El endpoint /api/v1/puestos debe haber sido consultado una vez.
        Assert.NotEmpty(apiClient.GetAllCalls);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.2: Puesto superior renderiza link con contexto preservado
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenPuestoHasSuperior_RendersLinkPreservingContext()
    {
        var superior = PuestoWebTestFixture.BuildPuestoDto("SUP-01", "Superior", null);
        var child = PuestoWebTestFixture.BuildPuestoDto("CHD-01", "Dependiente", null, superior.Id);
        var apiClient = FakePuestosApiClient.WithPuestoList(superior, child);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/puestos?search=dep&sort=nombre_asc&status=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // La celda "Puesto superior" de la fila dependiente debe contener
        // un <a> al detalle del superior, con p/search/sort preservados vía
        // returnStatus (espejo del patrón de CargoIndex).
        Assert.Contains(
            $"href=\"/organizacion/puestos/detalles/{superior.Id}",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"returnStatus=", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.3: toggle Eliminadas deshabilitado con tooltip
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_ToggleEliminadas_IsDisabledAndShowsTooltip()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList(
            PuestoWebTestFixture.BuildPuestoDto("P-001", "Analista", null));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El control Activas|Eliminadas debe estar presente con el link
        // Activas funcional y el link Eliminadas deshabilitado.
        Assert.Contains(">Activas</a>", content, StringComparison.OrdinalIgnoreCase);

        // Eliminadas está deshabilitado: tooltip declarado por atributo
        // data-bs-title en el anchor con clase .disabled (espejo del patrón
        // de Bootstrap para toggles deshabilitados).
        Assert.Contains(
            "data-bs-title=\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Próximamente",
            content,
            StringComparison.OrdinalIgnoreCase);

        // El link Eliminadas debe llevar la clase Bootstrap de deshabilitado
        // (no es un <button disabled> HTML, sino un <a class="... disabled">).
        Assert.Contains("disabled", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.4: listado sin resultados (lista vacía desde el backend)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenListIsEmpty_ShowsEmptyState()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron puestos", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-puesto-delete-button", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.5: búsqueda sin resultados
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenSearchHasNoResults_ShowsEmptyState()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList(
            PuestoWebTestFixture.BuildPuestoDto("P-001", "Analista", null));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/puestos?search=zzzzz");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se encontraron puestos", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"zzzzz\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.6: error visible cuando falla la carga inicial
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenApiFails_ShowsVisibleError()
    {
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.GetAllException = new HttpRequestException("boom");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/puestos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el listado", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"search\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Buscar", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.7: POST Delete éxito → PRG preservando filtros + LastDeletedId
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndKeepsLastDeletedId()
    {
        var toDelete = PuestoWebTestFixture.BuildPuestoDto("DEL-01", "Analista Senior", "Desc", null);
        var remaining = PuestoWebTestFixture.BuildPuestoDto("DEL-02", "Analista Junior", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(toDelete, remaining);
        apiClient.DeleteResult = new PuestoDeleteResult(true, HttpStatusCode.NoContent, null, null);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/puestos?p=1&search=ana&sort=nombre_desc");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/puestos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = toDelete.Id.ToString(),
            ["page"] = "1",
            ["search"] = "ana",
            ["sort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(toDelete.Id, Assert.Single(apiClient.DeleteCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("search=ana", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_desc", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"deletedId={toDelete.Id}", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se eliminó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);

        // El banner debe ofrecer reactivar con el id persistido
        // vía TempData["LastDeletedId"] → query string deletedId.
        Assert.Contains("formaction=\"?handler=Reactivate\"", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{toDelete.Id}\"", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.8: POST Delete 409 → feedback + fila visible
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible()
    {
        var puesto = PuestoWebTestFixture.BuildPuestoDto("CONF-01", "Con Conflicto", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(puesto);
        apiClient.DeleteResult = new PuestoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.Conflict,
            Code: "PuestoEnOcupacion",
            Message: "El puesto tiene una ocupación activa.");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/puestos?search=conf&sort=codigo_asc");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/puestos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1",
            ["search"] = "conf",
            ["sort"] = "codigo_asc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("search=conf", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=codigo_asc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo eliminar el puesto", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("El puesto tiene una ocupación activa.", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(puesto.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.9: POST Delete 404 → feedback recuperable
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Delete_WhenNotFound_ShowsFeedbackAndKeepsRowVisible()
    {
        var puesto = PuestoWebTestFixture.BuildPuestoDto("NF-01", "A Borrar", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList(puesto);
        apiClient.DeleteResult = new PuestoDeleteResult(
            Succeeded: false,
            StatusCode: HttpStatusCode.NotFound,
            Code: "PuestoNoEncontrado",
            Message: "El puesto no existe.");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/puestos");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/puestos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("ya no está disponible", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(puesto.Nombre, refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.10: POST Reactivate éxito → Activas + LastDeletedId limpio
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Reactivate_WhenSuccessful_RedirectsToActivasClearsLastDeletedId()
    {
        var puesto = PuestoWebTestFixture.BuildPuestoDto("REACT-01", "A Reactivar", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.ReactivateResult = PuestoCommandResult.Success(
            new PuestoDto(puesto.Id, puesto.Codigo, puesto.Nombre, puesto.Descripcion,
                PuestoWebTestFixture.SampleUnidadOrganizativaId, "Ventas",
                PuestoWebTestFixture.SampleCargoId, "Vendedor", null));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/puestos?status=eliminadas&search=react&sort=nombre_asc&deletedId=" + puesto.Id);
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/puestos?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1",
            ["search"] = "react",
            ["sort"] = "nombre_asc",
            ["status"] = "eliminadas"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(puesto.Id, Assert.Single(apiClient.ReactivateCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.DoesNotContain("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search=react", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sort=nombre_asc", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se reactivó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.11: POST Reactivate 409 (código duplicado) → permanece en origen
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Post_Reactivate_WhenConflictByCodigo_ShowsFeedbackAndKeepsContext()
    {
        var puesto = PuestoWebTestFixture.BuildPuestoDto("CONF-REACT-01", "Conflicto React", null, null);
        var apiClient = FakePuestosApiClient.WithPuestoList();
        apiClient.ReactivateResult = PuestoCommandResult.Failure(
            new PuestoError(PuestoErrorType.Conflict, "CodigoDuplicado",
                "Ya existe un puesto activo con el mismo código."));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/puestos?status=eliminadas");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/puestos?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["id"] = puesto.Id.ToString(),
            ["page"] = "1",
            ["status"] = "eliminadas"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(puesto.Id, Assert.Single(apiClient.ReactivateCalls));

        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("status=eliminadas", location, StringComparison.OrdinalIgnoreCase);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("No se pudo reactivar el puesto", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CodigoDuplicado", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1.12: preservación de contexto tras Delete (status=eliminadas)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_StatusEliminadas_PreservesSegmentAndShowsForwardCompatBehavior()
    {
        // El toggle Eliminadas está deshabilitado (decisión locked #2), pero
        // el backend expone solo lista plana. Cuando el usuario llega con
        // status=eliminadas en query (forward-compat), la página debe seguir
        // renderizando OK y el endpoint GetAllAsync debe ser consultado.
        var apiClient = FakePuestosApiClient.WithPuestoList(
            PuestoWebTestFixture.BuildPuestoDto("P-001", "Analista", null));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/puestos?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(apiClient.GetAllCalls);
        Assert.Contains("Puestos", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Tarea 2.1: usuario anónimo es redirigido a sign-in
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Index_WhenAnonymous_RedirectsToSignIn()
    {
        var factory = _fixture.BaseFactory;
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/organizacion/puestos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Harness JS — SweetAlert2 confirmación de Delete
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecutePuestoDeleteConfirmationScriptAsync(false);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.True(result.ShowCancelButton);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.True(result.ReverseButtons);
    }

    [Fact]
    public async Task DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce()
    {
        var result = await ExecutePuestoDeleteConfirmationScriptAsync(true);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Sí, eliminar", result.ConfirmButtonText);
        Assert.Equal("¿Eliminar puesto?", result.Title);
    }

    [Fact]
    public async Task ReactivateConfirmationScript_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecutePuestoReactivateConfirmationScriptAsync(false);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.True(result.ShowCancelButton);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.True(result.ReverseButtons);
        Assert.Equal("question", result.Icon);
    }

    [Fact]
    public async Task ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce()
    {
        var result = await ExecutePuestoReactivateConfirmationScriptAsync(true);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Sí, reactivar", result.ConfirmButtonText);
        Assert.Equal("¿Reactivar puesto?", result.Title);
    }

    // ──────────────────────────────────────────────────
    // Helpers JS harness (espejo del patrón de CargoIndexPageTests)
    // ──────────────────────────────────────────────────

    private static async Task<PuestoScriptExecutionResult> ExecutePuestoDeleteConfirmationScriptAsync(bool isConfirmed)
    {
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SGV.Web/wwwroot/js/pages/puestos-index.js"));
        var harnessPath = Path.Combine(Path.GetTempPath(), $"puesto-delete-confirmation-{Guid.NewGuid():N}.cjs");

        await File.WriteAllTextAsync(harnessPath, $$"""
const { wirePuestoDeleteConfirmation } = require({{JsonSerializer.Serialize(scriptPath)}});

let clickHandler = null;
let submitCount = 0;
let preventDefaultCalled = false;
let swalConfig = null;

const button = {
  getAttribute(name) {
    if (name === 'data-puesto-item-name') {
      return 'Analista';
    }

    if (name === 'data-puesto-item-code') {
      return 'P-001';
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
    return selector === '[data-puesto-delete-button]' ? button : null;
  },
  submit() {
    submitCount += 1;
  }
};

const root = {
  querySelectorAll(selector) {
    return selector === '[data-puesto-delete-form]' ? [form] : [];
  }
};

const Swal = {
  fire(config) {
    swalConfig = config;
    return Promise.resolve({ isConfirmed: {{(isConfirmed ? "true" : "false")}} });
  }
};

async function main() {
  wirePuestoDeleteConfirmation(root, Swal);

  if (!clickHandler) {
    throw new Error('Puesto delete confirmation click handler was not wired.');
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
    cancelButtonText: swalConfig ? swalConfig.cancelButtonText : null,
    title: swalConfig ? swalConfig.title : null
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

            var result = JsonSerializer.Deserialize<PuestoScriptExecutionResult>(standardOutput, new JsonSerializerOptions
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

    private static async Task<PuestoScriptExecutionResult> ExecutePuestoReactivateConfirmationScriptAsync(bool isConfirmed)
    {
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SGV.Web/wwwroot/js/pages/puestos-index.js"));
        var harnessPath = Path.Combine(Path.GetTempPath(), $"puesto-reactivate-confirmation-{Guid.NewGuid():N}.cjs");

        await File.WriteAllTextAsync(harnessPath, $$"""
const { wirePuestoReactivateConfirmation } = require({{JsonSerializer.Serialize(scriptPath)}});

let clickHandler = null;
let submitCount = 0;
let preventDefaultCalled = false;
let swalConfig = null;

const button = {
  getAttribute(name) {
    if (name === 'data-puesto-item-name') {
      return 'Analista Eliminado';
    }

    if (name === 'data-puesto-item-code') {
      return 'P-001';
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
    return selector === '[data-puesto-reactivate-button]' ? button : null;
  },
  submit() {
    submitCount += 1;
  }
};

const root = {
  querySelectorAll(selector) {
    return selector === '[data-puesto-reactivate-form]' ? [form] : [];
  }
};

const Swal = {
  fire(config) {
    swalConfig = config;
    return Promise.resolve({ isConfirmed: {{(isConfirmed ? "true" : "false")}} });
  }
};

async function main() {
  wirePuestoReactivateConfirmation(root, Swal);

  if (!clickHandler) {
    throw new Error('Puesto reactivate confirmation click handler was not wired.');
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
    cancelButtonText: swalConfig ? swalConfig.cancelButtonText : null,
    title: swalConfig ? swalConfig.title : null,
    icon: swalConfig ? swalConfig.icon : null
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

            var result = JsonSerializer.Deserialize<PuestoScriptExecutionResult>(standardOutput, new JsonSerializerOptions
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

    private sealed record PuestoScriptExecutionResult(
        int SubmitCount,
        bool PreventDefaultCalled,
        bool ShowCancelButton,
        bool ReverseButtons,
        string? ConfirmButtonText,
        string? CancelButtonText,
        string? Title,
        string? Icon);
}
