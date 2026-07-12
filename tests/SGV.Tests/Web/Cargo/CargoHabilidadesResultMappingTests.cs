using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Cargo;

public sealed partial class CargoHabilidadesPageTests
{
    [Fact]
    public async Task PostAsignar_BackendReturnsConflict_RendersConflictMessage()
    {
        // Conflict (409) se propaga tal cual desde el backend:
        // ApplySkillFailureToModelState mapea el type a un ModelState
        // error con key vacía que aparece en el validation summary.
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(CargoSkillErrorType.Conflict, "Conflicto", "Conflicto de versión."));

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            BuildAsignarForm(antiforgeryToken, skillId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        // Mensaje propagado tal cual desde el mensaje del error.
        Assert.Contains("Conflicto", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsignar_BackendReturnsUnauthorized_RendersSessionExpiredMessage()
    {
        // Unauthorized (401) — la página mapea a un mensaje
        // hardcoded local: "Su sesión expiró. Vuelva a iniciar
        // sesión." (independiente del mensaje del backend para evitar
        // filtrar detalles del upstream).
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(CargoSkillErrorType.Unauthorized, "Unauthorized", "Token expirado."));

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            BuildAsignarForm(antiforgeryToken, skillId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Su sesión expiró", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsignar_BackendReturnsForbidden_RendersAccessDeniedMessage()
    {
        // Forbidden (403) — la página mapea a un mensaje hardcoded
        // local: "No tiene permisos para modificar las habilidades del
        // cargo." (evita propagar el mensaje upstream porque podría
        // contener detalles de la autorización interna).
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(CargoSkillErrorType.Forbidden, "Forbidden", "Acceso denegado."));

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            BuildAsignarForm(antiforgeryToken, skillId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("No tiene permisos para modificar las habilidades", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAsignar_BackendReturnsTransport_RendersServiceUnavailableMessage()
    {
        // Transport (>=500 sin RFC ProblemDetails) — la página
        // traduce a un mensaje accionable hardcoded: "El servicio no
        // respondió correctamente. Intentá nuevamente." Coherente con
        // el camino IsTransportFailure(Exception) que también usa
        // error recuperable (no stack trace) para el caso de
        // excepción HTTP, pero este branch cubre el equivalente
        // cuando el cliente API devuelve un 5xx con un
        // CargoSkillErrorType.Transport en lugar de tirar excepción.
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(CargoSkillErrorType.Transport, "ServiceUnavailable", "Servicio caído."));

        await using var lease = await _fixture.CreateCargoLeaseAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await lease.Client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await lease.Client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            BuildAsignarForm(antiforgeryToken, skillId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("El servicio no respondió correctamente", content, StringComparison.OrdinalIgnoreCase);
    }
}
