using System.Net;
using System.Net.Http;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Common;
using Xunit;

namespace SGV.Tests.Web.Vacantes;

[Collection("WebIntegration")]
public sealed class VacantesCreateEditForbidTests
{
    private readonly WebIntegrationFixture _fixture;

    public VacantesCreateEditForbidTests(WebIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Get_Create_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied()
    {
        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            new FakeVacanteApiClient(),
            adminRole: false);

        var response = await lease.Client.GetAsync("/organizacion/vacantes/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenMutationRole_RendersFormWithCatalogs()
    {
        var states = FakeVacanteApiClient.BuildStates();
        var puesto = new PuestoDto(Guid.NewGuid(), "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null);
        var apiClient = new FakeVacanteApiClient
        {
            ListarEstadosResult = states,
            ListarPuestosResult = [puesto]
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/vacantes/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nueva vacante", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analista", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Abierta", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Input.PuestoId", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Input.EstadoVacanteId", content, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.ListarPuestosCalls);
    }

    [Fact]
    public async Task Get_Create_WhenMutationRole_LoadsPuestoChangeDismissScript()
    {
        // Issue #265: el alert-danger superior y el `asp-validation-summary`
        // muestran el mensaje de PuestoOcupado hasta el próximo submit. El
        // handler que los limpia al cambiar el SELECT vive en
        // `/js/pages/vacantes-create.js`; este test protege contra
        // regresión de la referencia (no del comportamiento JS, que
        // requiere navegador).
        var apiClient = new FakeVacanteApiClient
        {
            ListarEstadosResult = FakeVacanteApiClient.BuildStates(),
            ListarPuestosResult = [new PuestoDto(
                Guid.NewGuid(), "P-001", "Analista", null,
                Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null)]
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/vacantes/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"Input_PuestoId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/js/pages/vacantes-create.js", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenCatalogLoadFails_ShowsRecoverableErrorAndDisablesSave()
    {
        var apiClient = new FakeVacanteApiClient
        {
            ListarEstadosException = new HttpRequestException("upstream returned 503")
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/vacantes/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudieron cargar los catálogos. Intentá nuevamente.", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled=\"disabled\">Guardar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenPuestoCatalogLoadFails_ShowsRecoverableErrorAndDisablesSave()
    {
        var apiClient = new FakeVacanteApiClient
        {
            ListarPuestosException = new HttpRequestException("upstream returned 503")
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/vacantes/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudieron cargar los catálogos. Intentá nuevamente.", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled=\"disabled\">Guardar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetails()
    {
        var puestoId = Guid.NewGuid();
        var estadoId = Guid.NewGuid();
        var created = FakeVacanteApiClient.BuildDetail(
            puestoId: puestoId,
            estadoVacanteId: estadoId,
            puestoNombre: "Analista");
        var apiClient = new FakeVacanteApiClient
        {
            CrearResult = VacanteCommandResult.Success(created),
            ListarEstadosResult = FakeVacanteApiClient.BuildStates(),
            ListarPuestosResult = [new PuestoDto(
                puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null)]
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/vacantes/crear");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);
        var response = await lease.Client.PostAsync(
            "/organizacion/vacantes/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.PuestoId"] = puestoId.ToString("D"),
                ["Input.EstadoVacanteId"] = estadoId.ToString("D"),
                ["Input.FechaApertura"] = "2026-02-01",
                ["Input.Motivo"] = "Cobertura",
                ["Input.Observaciones"] = "Urgente"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/organizacion/vacantes/detalles/{created.Id:D}", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var request = Assert.Single(apiClient.CrearCalls);
        Assert.Equal(puestoId, request.PuestoId);
        Assert.Equal(estadoId, request.EstadoVacanteId);
        Assert.Equal("Urgente", request.Observaciones);
    }

    [Fact]
    public async Task Post_Create_WhenApiReturnsFieldValidationError_ShowsFieldErrorAndPreservesInput()
    {
        var puestoId = Guid.NewGuid();
        var states = FakeVacanteApiClient.BuildStates();
        var estadoId = states[0].Id;
        const string motivo = "Cobertura con datos inválidos";
        const string fieldError = "El motivo no cumple las reglas de negocio.";
        var apiClient = new FakeVacanteApiClient
        {
            CrearResult = VacanteCommandResult.Failure(
                new VacanteError(ErrorCategoria.Validation, "ValidationFailed", "La vacante contiene datos inválidos."),
                new Dictionary<string, string[]> { ["Motivo"] = [fieldError] }),
            ListarEstadosResult = states,
            ListarPuestosResult = [new PuestoDto(
                puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null)]
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/vacantes/crear");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);
        var response = await lease.Client.PostAsync(
            "/organizacion/vacantes/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.PuestoId"] = puestoId.ToString("D"),
                ["Input.EstadoVacanteId"] = estadoId.ToString("D"),
                ["Input.FechaApertura"] = "2026-02-01",
                ["Input.Motivo"] = motivo,
                ["Input.Observaciones"] = "Conservar observaciones"
            }));
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-valmsg-for=\"Input.Motivo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fieldError, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{motivo}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.CrearCalls);
    }

    [Fact]
    public async Task Post_Create_WhenApiReturnsConflict_ShowsMessageAndPreservesInput()
    {
        var puestoId = Guid.NewGuid();
        var states = FakeVacanteApiClient.BuildStates();
        var estadoId = states[0].Id;
        const string motivo = "Cobertura del puesto conflictivo";
        const string conflictMessage = "El puesto ya tiene una vacante abierta.";
        var apiClient = new FakeVacanteApiClient
        {
            CrearResult = VacanteCommandResult.Failure(
                new VacanteError(ErrorCategoria.Conflict, "PuestoConVacanteAbierta", conflictMessage)),
            ListarEstadosResult = states,
            ListarPuestosResult = [new PuestoDto(
                puestoId, "P-001", "Analista", null, Guid.NewGuid(), "Ventas", Guid.NewGuid(), "Vendedor", null)]
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/vacantes/crear");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);
        var response = await lease.Client.PostAsync(
            "/organizacion/vacantes/crear",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.PuestoId"] = puestoId.ToString("D"),
                ["Input.EstadoVacanteId"] = estadoId.ToString("D"),
                ["Input.FechaApertura"] = "2026-02-01",
                ["Input.Motivo"] = motivo,
                ["Input.Observaciones"] = "Conservar observaciones"
            }));
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(conflictMessage, content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{motivo}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Single(apiClient.CrearCalls);
    }

    [Fact]
    public async Task Get_Edit_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied()
    {
        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            new FakeVacanteApiClient(),
            adminRole: false);

        var response = await lease.Client.GetAsync($"/organizacion/vacantes/editar/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Edit_WhenMutationRole_PrepopulatesStateAndObservations()
    {
        var id = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var apiClient = new FakeVacanteApiClient
        {
            ObtenerPorIdResult = FakeVacanteApiClient.BuildDetail(
                id: id,
                estadoVacanteId: stateId,
                estadoVacanteNombre: "En selección",
                observaciones: "Observación actual"),
            ListarEstadosResult = FakeVacanteApiClient.BuildStates()
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var response = await lease.Client.GetAsync($"/organizacion/vacantes/editar/{id:D}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Editar vacante", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Observación actual", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("En selección", content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(id, Assert.Single(apiClient.ObtenerPorIdCalls));
        Assert.Empty(apiClient.ListarPuestosCalls);
    }

    [Fact]
    public async Task Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails()
    {
        var id = Guid.NewGuid();
        var currentStateId = Guid.NewGuid();
        var targetStateId = Guid.NewGuid();
        var current = FakeVacanteApiClient.BuildDetail(
            id: id,
            estadoVacanteId: currentStateId,
            observaciones: "Antes");
        var updated = FakeVacanteApiClient.BuildDetail(
            id: id,
            estadoVacanteId: targetStateId,
            estadoVacanteNombre: "Cubierta",
            fechaCierre: new DateTime(2026, 2, 10),
            observaciones: "Después");
        var apiClient = new FakeVacanteApiClient
        {
            ObtenerPorIdResult = current,
            CambiarEstadoResult = VacanteCommandResult.Success(updated),
            ListarEstadosResult = FakeVacanteApiClient.BuildStates()
        };

        await using var lease = await _fixture.CreateVacanteLeaseAsync(
            apiClient,
            adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/vacantes/editar/{id:D}");
        var token = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);
        var response = await lease.Client.PostAsync(
            $"/organizacion/vacantes/editar/{id:D}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.PuestoId"] = current.PuestoId.ToString("D"),
                ["Input.FechaApertura"] = current.FechaApertura.ToString("yyyy-MM-dd"),
                ["Input.EstadoVacanteId"] = targetStateId.ToString("D"),
                ["Input.Motivo"] = current.Motivo,
                ["Input.Observaciones"] = "Después"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/organizacion/vacantes/detalles/{id:D}", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var call = Assert.Single(apiClient.CambiarEstadoCalls);
        Assert.Equal(id, call.Id);
        Assert.Equal(targetStateId, call.Request.EstadoVacanteId);
        Assert.Equal("Después", call.Request.Observaciones);
    }
}
