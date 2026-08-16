using System.Net;
using System.Text.Json;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web;

public sealed partial class UnidadOrganizativaWebTests
{
    [Fact]
    public async Task Post_ReactivateFromEdit_WhenSuccessful_RedirectsToDetails()
    {
        var unitId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(unitId, "R01", "Unidad Reactivada", Guid.NewGuid(), "Dirección", null, null, null, null, null, null));
        apiClient.GetByIdResult = null; // Initially null (deleted)

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}?returnPage=1&returnSearch=test&returnSort=nombre_asc&returnView=tree");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["returnPage"] = "1",
            ["returnSearch"] = "test",
            ["returnSort"] = "nombre_asc",
            ["returnView"] = "tree"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/organizacion/unidades-organizativas/detalles/{unitId}", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returnPage=1", response.Headers.Location?.OriginalString);
        Assert.Contains("returnSearch=test", response.Headers.Location?.OriginalString);
        Assert.Contains("returnSort=nombre_asc", response.Headers.Location?.OriginalString);
        Assert.Contains("returnView=tree", response.Headers.Location?.OriginalString);

        apiClient.GetByIdResult = new UnidadOrganizativaDto(unitId, "R01", "Unidad Reactivada", Guid.NewGuid(), "Dirección", null, null, null, null, null, null);

        var detailsResponse = await client.GetAsync(response.Headers.Location!);
        var detailsContent = HttpUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("se reactivó correctamente", detailsContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unidad Reactivada", detailsContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ReactivateFromEdit_WhenConflict_ShowsFeedback()
    {
        var unitId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.ReactivateResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado",
                "Ya existe una unidad activa con el mismo código.", Categoria: ErrorCategoria.Conflict));
        apiClient.GetByIdResult = null;

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}?handler=Reactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        }));

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("código", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content);
    }

    // ──────────────────────────────────────────────
    // Phase 4: Edit — PUT / PATCH flow
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAuthenticated_LoadsCatalogsAndData()
    {
        var unitId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", Guid.NewGuid(), "Departamento",
            null, null, null, parentId, "RECT", "Rectorado");
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(Guid.NewGuid(), "DIR", "Dirección")];
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse([new UnidadOrganizativaTreeNodeDto(Guid.NewGuid(), "RECT", "Rectorado", Guid.NewGuid(), "Institución", [])], []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Editar unidad organizativa", content);
        Assert.Contains("DEPT01", content);
        Assert.Contains("Departamento Test", content);
        Assert.Contains("Dirección", content);
        Assert.Contains("Rectorado", content);
        Assert.Contains("Guardar cambios", content);
    }

    [Fact]
    public async Task Get_Edit_WhenNotFound_ShowsRecoverableState()
    {
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = null;

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{Guid.NewGuid()}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reactivar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content);
    }

    [Fact]
    public async Task Post_Edit_WhenSuccessfulWithoutParentChange_RedirectsToDetails()
    {
        var unitId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", tipoId, "Departamento",
            null, null, null, parentId, "RECT", "Rectorado");
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(unitId, "DEPT01", "Departamento Test Updated", tipoId, "Departamento",
                null, null, null, parentId, "RECT", "Rectorado"));
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "DEPT01",
            ["Input.Nombre"] = "Departamento Test Updated",
            ["Input.TipoUnidadOrganizativaId"] = tipoId.ToString(),
            ["Input.UnidadPadreId"] = parentId.ToString(),
            ["OriginalUnidadPadreId"] = parentId.ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Contains($"/organizacion/unidades-organizativas/detalles/{unitId}", postResponse.Headers.Location?.OriginalString);
        Assert.Empty(apiClient.ChangeParentCalls);
    }

    [Fact]
    public async Task Post_Edit_WhenSuccessfulWithParentChange_PreservesListContextInDetails()
    {
        var unitId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var oldParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        var updatedUnit = new UnidadOrganizativaDto(unitId, "DEPT01", "Departamento Test Updated", tipoId, "Departamento",
                null, null, null, newParentId, "NEW", "New Parent");
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", tipoId, "Departamento",
            null, null, null, oldParentId, "OLD", "Old Parent");
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Success(updatedUnit);
        apiClient.ChangeParentCommandResult = UnidadOrganizativaCommandResult.Success(updatedUnit);
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}?p=2&search=test&sort=nombre_desc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "DEPT01",
            ["Input.Nombre"] = "Departamento Test Updated",
            ["Input.TipoUnidadOrganizativaId"] = tipoId.ToString(),
            ["Input.UnidadPadreId"] = newParentId.ToString(),
            ["OriginalUnidadPadreId"] = oldParentId.ToString(),
            ["ReturnPage"] = "2",
            ["ReturnSearch"] = "test",
            ["ReturnSort"] = "nombre_desc"
        }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        var redirectLocation = postResponse.Headers.Location?.OriginalString;
        Assert.Contains($"/organizacion/unidades-organizativas/detalles/{unitId}", redirectLocation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(unitId, Assert.Single(apiClient.ChangeParentCalls));

        apiClient.GetByIdResult = updatedUnit;

        var detailsResponse = await client.GetAsync(postResponse.Headers.Location!);
        var detailsContent = HttpUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("New Parent", detailsContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/organizacion/unidades-organizativas?p=2&search=test&sort=nombre_desc\"", detailsContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Edit_WhenParentChangeFails_RedirectsToEditWithWarning()
    {
        var unitId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var oldParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", tipoId, "Departamento",
            null, null, null, oldParentId, "OLD", "Old Parent");
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(unitId, "DEPT01", "Departamento Test Updated", tipoId, "Departamento",
                null, null, null, oldParentId, "OLD", "Old Parent"));
        apiClient.ChangeParentCommandResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "ParentChangeFailed", "No se pudo actualizar la unidad padre."));
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];

        // Re-set GetByIdResult for the follow-up GET after redirect
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test Updated", tipoId, "Departamento",
            null, null, null, oldParentId, "OLD", "Old Parent");

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}?p=1&search=test&sort=nombre_asc");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "DEPT01",
            ["Input.Nombre"] = "Departamento Test Updated",
            ["Input.TipoUnidadOrganizativaId"] = tipoId.ToString(),
            ["Input.UnidadPadreId"] = newParentId.ToString(),
            ["OriginalUnidadPadreId"] = oldParentId.ToString(),
            ["ReturnPage"] = "1",
            ["ReturnSearch"] = "test",
            ["ReturnSort"] = "nombre_asc"
        }));

        // Should redirect back to Edit with a warning
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Contains($"/organizacion/unidades-organizativas/editar/{unitId}", postResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(unitId, Assert.Single(apiClient.ChangeParentCalls));

        // Follow redirect to verify warning is shown
        var followResponse = await client.GetAsync(postResponse.Headers.Location!);
        var followContent = HttpUtility.HtmlDecode(await followResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, followResponse.StatusCode);
        Assert.Contains("no se pudo actualizar la unidad padre", followContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Departamento Test Updated", followContent);
    }

    [Fact]
    public async Task Post_Edit_WhenConflict_ShowsErrorAndKeepsCatalogs()
    {
        var unitId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", tipoId, "Departamento",
            null, null, null, parentId, "RECT", "Rectorado");
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "Conflict", "La unidad tiene dependencias activas.", Categoria: ErrorCategoria.Conflict));
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse([new UnidadOrganizativaTreeNodeDto(Guid.NewGuid(), "RECT", "Rectorado", Guid.NewGuid(), "Institución", [])], []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "DEPT01",
            ["Input.Nombre"] = "Departamento Test",
            ["Input.TipoUnidadOrganizativaId"] = tipoId.ToString(),
            ["Input.UnidadPadreId"] = parentId.ToString(),
            ["OriginalUnidadPadreId"] = parentId.ToString()
        }));

        var content = HttpUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("tiene dependencias activas", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dirección", content); // catalogs still rendered
        Assert.Contains("Rectorado", content); // tree still rendered
    }

    [Fact]
    public async Task Post_Edit_WhenValidationFails_ShowsFieldErrorsAndKeepsCatalogs()
    {
        var unitId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", tipoId, "Departamento",
            null, null, null, null, null, null);
        // PR3: Codigo es inmutable en Edit; el backend ya no debería devolver
        // errores de campo sobre Codigo. Asertamos el camino de field-error
        // usando un campo editable (nombre) — paridad con
        // Puestos/Post_Edit_WhenBackendReturnsFieldErrors.
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Failure(
            new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, "ValidationError", "One or more fields are invalid."),
            new Dictionary<string, string[]> { ["nombre"] = ["El nombre es obligatorio."] });
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse([new UnidadOrganizativaTreeNodeDto(Guid.NewGuid(), "RECT", "Rectorado", Guid.NewGuid(), "Institución", [])], []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "DEPT01",
            ["Input.Nombre"] = "Departamento Test",
            ["Input.TipoUnidadOrganizativaId"] = tipoId.ToString(),
            ["OriginalUnidadPadreId"] = ""
        }));

        var content = HttpUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("El nombre es obligatorio.", content);
        Assert.Contains("Dirección", content); // catalogs still loaded
        Assert.Contains("Rectorado", content); // tree still loaded
    }

    // ──────────────────────────────────────────────
    // Phase 4b: PR3 — Codigo input hidden in Edit (Codigo is immutable)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_OcultaInputCodigo()
    {
        var unitId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", tipoId, "Departamento",
            null, null, null, null, null, null);
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];
        apiClient.TreeResult = new UnidadOrganizativaArbolResponse([new UnidadOrganizativaTreeNodeDto(Guid.NewGuid(), "RECT", "Rectorado", Guid.NewGuid(), "Institución", [])], []);

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var response = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Triangulación negativa: el input editable de Codigo NO debe aparecer
        // en el HTML de Edit (campo inmutable post-create).
        Assert.DoesNotContain("name=\"Input.Codigo\"", content, StringComparison.OrdinalIgnoreCase);

        // Triangulación positiva: el código SÍ se muestra al usuario como
        // texto de identificación (header read-only), y los demás campos
        // editables sí renderizan sus inputs.
        Assert.Contains("DEPT01", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Nombre\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.TipoUnidadOrganizativaId\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Edit_NoEnviaCodigoEnPayload()
    {
        var unitId = Guid.NewGuid();
        var tipoId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var apiClient = FakeUnidadOrganizativaApiClient.WithPages(CreatePage(1, 10, 0));
        apiClient.GetByIdResult = new UnidadOrganizativaDto(
            unitId, "DEPT01", "Departamento Test", tipoId, "Departamento",
            null, null, null, parentId, "RECT", "Rectorado");
        apiClient.CommandResult = UnidadOrganizativaCommandResult.Success(
            new UnidadOrganizativaDto(unitId, "DEPT01", "Departamento Test Updated", tipoId, "Departamento",
                null, null, null, parentId, "RECT", "Rectorado"));
        apiClient.TiposResult = [new TipoUnidadOrganizativaDto(tipoId, "DIR", "Dirección")];

        await using var lease = await CreateAuthenticatedClientAsync(apiClient);
        var client = lease.Client;

        var getResponse = await client.GetAsync($"/organizacion/unidades-organizativas/editar/{unitId}");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        // Tampering simulado: cliente malicioso agrega Input.Codigo al form
        // (e.g., devtools edit). El backend NO debe propagar este campo al
        // request porque ActualizarUnidadOrganizativaRequest no tiene Codigo.
        var postResponse = await client.PostAsync($"/organizacion/unidades-organizativas/editar/{unitId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "HACKED",
            ["Input.Nombre"] = "Departamento Test Updated",
            ["Input.TipoUnidadOrganizativaId"] = tipoId.ToString(),
            ["Input.UnidadPadreId"] = parentId.ToString(),
            ["OriginalUnidadPadreId"] = parentId.ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var update = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(unitId, update.Id);

        // Triangulación negativa: la serialización del payload NO contiene
        // ninguna propiedad "codigo" — sea de Input o del DTO. Esto es un
        // regression guard: si alguien añade Codigo a
        // ActualizarUnidadOrganizativaRequest, este test rompe.
        var json = JsonSerializer.Serialize(update.Request);
        Assert.DoesNotContain("codigo", json, StringComparison.OrdinalIgnoreCase);

        // Triangulación positiva: los campos editables sí están presentes
        // y poblados desde el form.
        Assert.Equal("Departamento Test Updated", update.Request.Nombre);
        Assert.Equal(tipoId, update.Request.TipoUnidadOrganizativaId);
        Assert.Equal(parentId, update.Request.UnidadPadreId);
    }
}
