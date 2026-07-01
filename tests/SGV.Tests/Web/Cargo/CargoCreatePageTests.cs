using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Web smoke tests for the Create page (PR2A) of Cargos, the Sidenav
/// "Nueva" entry, and the duplicate-Codigo conflict mapping. Uses
/// <see cref="SgvWebApplicationFactory"/> + <see cref="FakeCargoApiClient"/>
/// so MySQL is not required.
/// </summary>
public sealed class CargoCreatePageTests
{
    private static readonly Guid JuniorNivelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SeniorNivelId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // ──────────────────────────────────────────────
    // Task 19: GET carga el dropdown de niveles
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticated_LoadsNivelesDropdown()
    {
        var apiClient = new FakeCargoApiClient
        {
            NivelesResult = new List<NivelCargoDto>
            {
                new(JuniorNivelId, "JR", "Junior", 1, 1),
                new(SeniorNivelId, "SR", "Senior", 2, 2)
            }
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/cargos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Codigo\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Nombre\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.Descripcion\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"Input.NivelId\"", content, StringComparison.OrdinalIgnoreCase);

        // El catálogo debe popular el select
        Assert.Contains("Junior", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Senior", content, StringComparison.OrdinalIgnoreCase);

        // El helper del fake debe haber sido invocado exactamente una vez
        Assert.Equal(1, apiClient.NivelesCalls);
    }

    // ──────────────────────────────────────────────
    // Task 19 (recuperable): el catálogo caído muestra error y conserva el form
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenNivelesCatalogFails_ShowsRecoverableError()
    {
        var apiClient = new FakeCargoApiClient
        {
            NivelesException = new HttpRequestException("catalog down")
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/cargos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el catálogo", content, StringComparison.OrdinalIgnoreCase);
        // El form debe seguir visible para que el usuario pueda reintentar
        Assert.Contains("name=\"Input.Codigo\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Task 20: POST exitoso → PRG a Details
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation()
    {
        var nivelId = JuniorNivelId;
        var newCargoId = Guid.NewGuid();
        var apiClient = new FakeCargoApiClient
        {
            CreateResult = CargoCommandResult.Success(
                new CargoDto(newCargoId, "C-NEW", "Nuevo Cargo", "Desc", nivelId, "Junior"))
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-NEW",
            ["Input.Nombre"] = "Nuevo Cargo",
            ["Input.Descripcion"] = "Desc",
            ["Input.NivelId"] = nivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/cargos/detalles/{newCargoId}", location, StringComparison.OrdinalIgnoreCase);

        var posted = Assert.Single(apiClient.CreateCalls);
        Assert.Equal("C-NEW", posted.Codigo);
        Assert.Equal("Nuevo Cargo", posted.Nombre);
        Assert.Equal(nivelId, posted.NivelId);
    }

    // ──────────────────────────────────────────────
    // Task 21: POST con Codigo duplicado → error a nivel de campo
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm()
    {
        var nivelId = JuniorNivelId;
        var apiClient = new FakeCargoApiClient
        {
            CreateResult = CargoCommandResult.Failure(
                new CargoError(
                    CargoErrorType.Conflict,
                    "CodigoDuplicado",
                    "Ya existe un cargo activo con el código C-DUP."))
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-DUP",
            ["Input.Nombre"] = "Cargo Duplicado",
            ["Input.NivelId"] = nivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible con los valores enviados
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C-DUP", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cargo Duplicado", content, StringComparison.OrdinalIgnoreCase);

        // El error a nivel de campo "Input.Codigo" debe aparecer
        Assert.Contains("Ya existe un cargo activo con el código C-DUP.", content, StringComparison.OrdinalIgnoreCase);

        // El error de Codigo debe estar en el contenedor del campo Codigo, no en un alert general
        // El span del asp-validation-for para Input.Codigo se renderiza como
        // <span class="text-danger field-validation-error" data-valmsg-for="Input.Codigo" ...>...</span>.
        Assert.True(
            Regex.IsMatch(content, @"<span[^>]*data-valmsg-for=""Input\.Codigo""[^>]*>[\s\S]*?Ya existe un cargo activo", RegexOptions.IgnoreCase),
            "Expected the duplicate-Codigo conflict message to be rendered in the Input.Codigo field-validation span.");

        // No debe redirigir (se renderiza la misma página con el error)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        // El catálogo debe haber sido recargado para que el dropdown siga funcional
        Assert.Equal(2, apiClient.NivelesCalls);
    }

    // ──────────────────────────────────────────────
    // Task 22: Sidenav muestra entry "Nueva" con href correcto
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticated_SidenavShowsNuevaEntryWithActiveState()
    {
        var apiClient = new FakeCargoApiClient();

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync("/organizacion/cargos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El submenú "Cargos" debe contener un item "Nueva" con href correcto
        Assert.Contains("href=\"/organizacion/cargos/crear\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Nueva<", content, StringComparison.OrdinalIgnoreCase);

        // El grupo padre "Cargos" debe estar marcado como active (propagación por
        // StartsWithSegments: la variable Razor `cargosActive` se renderiza como
        // `active` cuando el path empieza con `/organizacion/cargos`).
        Assert.True(
            Regex.IsMatch(content, @"<a[^>]*aria-controls=""cargos""[^>]*class=""[^""]*\bactive\b[^""]*""", RegexOptions.IgnoreCase),
            "Expected the Cargos sidenav group toggle link to be marked as active when on /organizacion/cargos/crear.");
    }

    // ──────────────────────────────────────────────
    // Task 23: Validación server-side: Codigo vacío NO redirige
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenCodigoIsEmpty_ShowsValidationErrorAndDoesNotRedirect()
    {
        var apiClient = new FakeCargoApiClient();

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "",
            ["Input.Nombre"] = "Sin Código",
            ["Input.NivelId"] = JuniorNivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de validación de Codigo debe aparecer en su field-validation span
        // (asp-validation-for="Input.Codigo" → <span data-valmsg-for="Input.Codigo">).
        Assert.True(
            Regex.IsMatch(content, @"<span[^>]*data-valmsg-for=""Input\.Codigo""[^>]*>[\s\S]*?(?:obligatorio|requerido|required)", RegexOptions.IgnoreCase),
            "Expected the Input.Codigo required-field validation message to be rendered.");

        // El API client NO debe haber sido invocado (ModelState cortó antes)
        Assert.Empty(apiClient.CreateCalls);

        // No debe redirigir (se renderiza la misma página con el error)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    // ──────────────────────────────────────────────
    // Review fix #1: try/catch alrededor de CreateAsync para transport failures
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenHttpRequestException_ReloadsCatalogAndShowsGeneralError()
    {
        var apiClient = new FakeCargoApiClient
        {
            CreateException = new HttpRequestException("boom"),
            NivelesResult = new List<NivelCargoDto>
            {
                new(JuniorNivelId, "JR", "Junior", 1, 1)
            }
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-TRANSPORT",
            ["Input.Nombre"] = "Cargo Transport Fail",
            ["Input.NivelId"] = JuniorNivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados (preserva input).
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C-TRANSPORT", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cargo Transport Fail", content, StringComparison.OrdinalIgnoreCase);

        // El dropdown de niveles debe estar repoblado tras el fallo.
        Assert.Contains("Junior", content, StringComparison.OrdinalIgnoreCase);

        // No debe haber un mensaje de validación a nivel de campo sobre Codigo
        // (el <span> del asp-validation-for se renderiza siempre; verificamos
        // que su contenido no contenga un texto de error tras un fallo de
        // transporte).
        var codigoFieldSpan = Regex.Match(
            content,
            @"<span[^>]*data-valmsg-for=""Input\.Codigo""[^>]*>([\s\S]*?)</span>",
            RegexOptions.IgnoreCase);
        Assert.True(codigoFieldSpan.Success, "El field-validation span de Input.Codigo debe existir.");
        Assert.True(string.IsNullOrWhiteSpace(codigoFieldSpan.Groups[1].Value),
            $"El field-validation span de Input.Codigo debe estar vacío tras un error de transporte, pero contiene: '{codigoFieldSpan.Groups[1].Value}'.");

        // El catálogo se consultó una vez en GET + una vez tras el POST fallido = 2.
        Assert.Equal(2, apiClient.NivelesCalls);
    }

    [Fact]
    public async Task Post_Create_WhenTaskCanceledException_ReloadsCatalogAndShowsGeneralError()
    {
        var apiClient = new FakeCargoApiClient
        {
            CreateException = new TaskCanceledException("request canceled"),
            NivelesResult = new List<NivelCargoDto>
            {
                new(SeniorNivelId, "SR", "Senior", 2, 2)
            }
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-TIMEOUT",
            ["Input.Nombre"] = "Cargo Timeout",
            ["Input.NivelId"] = SeniorNivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("C-TIMEOUT", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cargo Timeout", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Senior", content, StringComparison.OrdinalIgnoreCase);

        // El catálogo se recargó tras el fallo.
        Assert.Equal(2, apiClient.NivelesCalls);
    }

    [Fact]
    public async Task Post_Create_WhenJsonException_ReloadsCatalogAndShowsGeneralError()
    {
        var apiClient = new FakeCargoApiClient
        {
            CreateException = new JsonException("malformed body"),
            NivelesResult = new List<NivelCargoDto>
            {
                new(JuniorNivelId, "JR", "Junior", 1, 1)
            }
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-BADJSON",
            ["Input.Nombre"] = "Cargo Bad Json",
            ["Input.NivelId"] = JuniorNivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("C-BADJSON", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cargo Bad Json", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Junior", content, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(2, apiClient.NivelesCalls);
    }

    // ──────────────────────────────────────────────
    // Review fix #2: FieldErrors roundtrip API 400 → form (ValidationProblemDetails)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnCodigo()
    {
        var apiClient = new FakeCargoApiClient
        {
            CreateResult = CargoCommandResult.Failure(
                new CargoError(CargoErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["codigo"] = new[] { "ya existe" }
                })
        };

        using var client = await CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-RT",
            ["Input.Nombre"] = "Cargo Roundtrip",
            ["Input.NivelId"] = JuniorNivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible (sin PRG).
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de field-error "ya existe" debe quedar dentro del
        // field-validation span de Input.Codigo (mapping via
        // CargoFormHelpers.ApplyFieldErrorsToModelState → prefijo "Input.").
        Assert.True(
            Regex.IsMatch(content, @"<span[^>]*data-valmsg-for=""Input\.Codigo""[^>]*>[\s\S]*?ya existe[\s\S]*?</span>", RegexOptions.IgnoreCase),
            "Expected the backend field-error message 'ya existe' to be rendered inside the Input.Codigo field-validation span.");
    }

    // ──────────────────────────────────────────────
    // Helpers de soporte
    // ──────────────────────────────────────────────

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

    private sealed class RecordingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
