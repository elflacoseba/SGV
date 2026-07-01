using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Web smoke tests for the Edit page (PR2B) of Cargos. Verifies the
/// get-by-id flow, the prepopulated form, the not-found recoverable
/// state, and the round-trip of update results (success, conflict on
/// Codigo, backend validation errors). Uses <see cref="SgvWebApplicationFactory"/>
/// + <see cref="FakeCargoApiClient"/> so MySQL is not required.
/// </summary>
public sealed class CargoEditPageTests : IClassFixture<CargoWebTestFixture>
{
    private readonly CargoWebTestFixture _fixture;

    public CargoEditPageTests(CargoWebTestFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // Task 2.1: GET anónimo redirige a sign-in
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAnonymous_RedirectsToSignIn()
    {
        using var client = _fixture.BaseFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync($"/organizacion/cargos/editar/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Task 2.2: GET autenticado prellena form y dropdown de niveles
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAuthenticated_PrepopulatesFormAndNiveles()
    {
        var cargoId = Guid.NewGuid();
        var nivelId = CargoWebTestFixture.SeniorNivelId;
        var cargo = new CargoDto(cargoId, "C-EDIT", "Cargo a Editar", "Descripción inicial", nivelId, "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.NivelesResult = new List<NivelCargoDto>
        {
            new(CargoWebTestFixture.JuniorNivelId, "JR", "Junior", 1, 1),
            new(CargoWebTestFixture.SeniorNivelId, "SR", "Senior", 2, 2)
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/cargos/editar/{cargoId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El form debe estar visible y prellenado con los valores del cargo
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"C-EDIT\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cargo a Editar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Descripción inicial", content, StringComparison.OrdinalIgnoreCase);

        // El dropdown de niveles debe estar repoblado
        Assert.Contains("Junior", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Senior", content, StringComparison.OrdinalIgnoreCase);

        // El catálogo se cargó una vez (en GET)
        Assert.Equal(1, apiClient.NivelesCalls);
    }

    // ──────────────────────────────────────────────
    // Task 2.3: GET con cargo inexistente muestra estado recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenCargoNotFound_ShowsRecoverableState()
    {
        var apiClient = FakeCargoApiClient.WithCargoList();
        var missingId = Guid.NewGuid();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/cargos/editar/{missingId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El estado recuperable debe indicar que el cargo no está disponible
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);

        // El form no debe estar visible (sin datos para editar)
        Assert.DoesNotContain("value=\"C-EDIT\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Task 2.4: POST exitoso → PRG a Edit con TempData de éxito
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenSuccessful_RedirectsToEditWithConfirmation()
    {
        var cargoId = Guid.NewGuid();
        var nivelId = CargoWebTestFixture.SeniorNivelId;
        var updatedCargo = new CargoDto(cargoId, "C-EDIT", "Cargo Editado", "Descripción actualizada", nivelId, "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(updatedCargo);
        apiClient.UpdateResult = CargoCommandResult.Success(updatedCargo);

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/cargos/editar/{cargoId}");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/cargos/editar/{cargoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-EDIT",
            ["Input.Nombre"] = "Cargo Editado",
            ["Input.Descripcion"] = "Descripción actualizada",
            ["Input.NivelId"] = nivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/cargos/editar/{cargoId}", location, StringComparison.OrdinalIgnoreCase);

        // El payload se envió correctamente al API
        var update = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(cargoId, update.Id);
        Assert.Equal("C-EDIT", update.Request.Codigo);
        Assert.Equal("Cargo Editado", update.Request.Nombre);
        Assert.Equal(nivelId, update.Request.NivelId);
        Assert.Equal("Descripción actualizada", update.Request.Descripcion);

        // Al refrescar, el TempData debe mostrar el mensaje de éxito
        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("se actualizó correctamente", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Task 2.5: POST con Codigo duplicado → error a nivel de campo
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenCodigoConflict_ShowsFieldErrorAndKeepsForm()
    {
        var cargoId = Guid.NewGuid();
        var nivelId = CargoWebTestFixture.JuniorNivelId;
        var cargo = new CargoDto(cargoId, "C-EDIT", "Cargo a Editar", null, nivelId, "Junior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.UpdateResult = CargoCommandResult.Failure(
            new CargoError(
                CargoErrorType.Conflict,
                "CodigoDuplicado",
                "Ya existe un cargo activo con el código C-DUP."));

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/cargos/editar/{cargoId}");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/cargos/editar/{cargoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-DUP",
            ["Input.Nombre"] = "Cargo Duplicado",
            ["Input.NivelId"] = nivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible con los valores enviados
        Assert.Contains("C-DUP", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cargo Duplicado", content, StringComparison.OrdinalIgnoreCase);

        // El error a nivel de campo "Input.Codigo" debe aparecer
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.CodigoKey)}""[^>]*>[\s\S]*?Ya existe un cargo activo", RegexOptions.IgnoreCase),
            "Expected the duplicate-Codigo conflict message to be rendered in the Input.Codigo field-validation span.");

        // El catálogo debe haber sido recargado (GET inicial + POST fallido)
        Assert.Equal(2, apiClient.NivelesCalls);
    }

    // ──────────────────────────────────────────────
    // Task 2.6: POST con FieldErrors del backend → errores a nivel de campo
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenValidationFails_ShowsFieldErrors()
    {
        var cargoId = Guid.NewGuid();
        var nivelId = CargoWebTestFixture.JuniorNivelId;
        var cargo = new CargoDto(cargoId, "C-EDIT", "Cargo a Editar", null, nivelId, "Junior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.UpdateResult = CargoCommandResult.Failure(
            new CargoError(CargoErrorType.Validation, "Validation", "validation failed"),
            new Dictionary<string, string[]>
            {
                ["codigo"] = new[] { "El código no puede superar los 50 caracteres." },
                ["nombre"] = new[] { "El nombre es obligatorio." }
            });

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/cargos/editar/{cargoId}");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/cargos/editar/{cargoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = new string('x', 51),
            ["Input.Nombre"] = string.Empty,
            ["Input.NivelId"] = nivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);

        // Los mensajes de field-error deben estar en sus spans correspondientes
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.CodigoKey)}""[^>]*>[\s\S]*?El código no puede superar", RegexOptions.IgnoreCase),
            $"Expected the codigo field-error to be rendered in the {CargoFormKeys.CodigoKey} field-validation span.");

        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.NombreKey)}""[^>]*>[\s\S]*?El nombre es obligatorio", RegexOptions.IgnoreCase),
            $"Expected the nombre field-error to be rendered in the {CargoFormKeys.NombreKey} field-validation span.");

        // El catálogo se recargó tras el fallo (GET inicial + POST fallido)
        Assert.Equal(2, apiClient.NivelesCalls);
    }

    // ──────────────────────────────────────────────
    // Task 2.7: POST con HttpRequestException → error recuperable sin 500
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenTransportFails_ShowsRecoverableError()
    {
        var cargoId = Guid.NewGuid();
        var nivelId = CargoWebTestFixture.SeniorNivelId;
        var cargo = new CargoDto(cargoId, "C-EDIT", "Cargo a Editar", null, nivelId, "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.UpdateException = new HttpRequestException("network down");

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/cargos/editar/{cargoId}");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/cargos/editar/{cargoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-EDIT",
            ["Input.Nombre"] = "Cargo Editado",
            ["Input.NivelId"] = nivelId.ToString()
        }));

        // El handler debe haber atrapado la excepción de transporte y
        // respondido 200 con el formulario re-renderizado, no propagado
        // como 500.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible para que el usuario pueda reintentar.
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);

        // El banner rojo de error recuperable debe estar visible.
        Assert.Contains("No se pudo contactar al servicio de cargos", content, StringComparison.OrdinalIgnoreCase);

        // El catálogo se recarga también en el camino de transporte-failure.
        Assert.Equal(2, apiClient.NivelesCalls);

        // El payload se envió antes de que la fake lanzara la excepción.
        var update = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(cargoId, update.Id);
        Assert.Equal("C-EDIT", update.Request.Codigo);
    }
}
