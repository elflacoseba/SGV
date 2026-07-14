using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Web smoke tests for the Create page (PR2A) of Cargos, the Sidenav
/// "Nueva" entry, and the duplicate-Codigo conflict mapping. Uses
/// <see cref="SgvWebApplicationFactory"/> + <see cref="FakeCargoApiClient"/>
/// so MySQL is not required.
/// </summary>
[Collection("WebIntegration")]
public sealed class CargoCreatePageTests
{
    private readonly WebIntegrationFixture _fixture;

    public CargoCreatePageTests(WebIntegrationFixture fixture) => _fixture = fixture;

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
                new(CargoWebTestFixture.JuniorNivelId, "JR", "Junior", 1, 1),
                new(CargoWebTestFixture.SeniorNivelId, "SR", "Senior", 2, 2)
            }
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{CargoFormKeys.CodigoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{CargoFormKeys.NombreKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{CargoFormKeys.DescripcionKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{CargoFormKeys.NivelIdKey}\"", content, StringComparison.OrdinalIgnoreCase);

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

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el catálogo", content, StringComparison.OrdinalIgnoreCase);
        // El form debe seguir visible para que el usuario pueda reintentar
        Assert.Contains($"name=\"{CargoFormKeys.CodigoKey}\"", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        await using var lease = await _fixture.CreateCargoLeaseAsync(new FakeCargoApiClient());

        var response = await lease.Client.GetAsync("/organizacion/cargos/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Task 20: POST exitoso → PRG a Details
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation()
    {
        var nivelId = CargoWebTestFixture.JuniorNivelId;
        var newCargoId = Guid.NewGuid();
        var apiClient = new FakeCargoApiClient
        {
            CreateResult = CargoCommandResult.Success(
                new CargoDto(newCargoId, "C-NEW", "Nuevo Cargo", "Desc", nivelId, "Junior"))
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
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
        var nivelId = CargoWebTestFixture.JuniorNivelId;
        var apiClient = new FakeCargoApiClient
        {
            CreateResult = CargoCommandResult.Failure(
                new CargoError(
                    CargoErrorType.Conflict,
                    "CodigoDuplicado",
                    "Ya existe un cargo activo con el código C-DUP.",
                    Categoria: ErrorCategoria.Conflict))
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
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
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.CodigoKey)}""[^>]*>[\s\S]*?Ya existe un cargo activo", RegexOptions.IgnoreCase),
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

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/cargos/crear");
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

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "",
            ["Input.Nombre"] = "Sin Código",
            ["Input.NivelId"] = CargoWebTestFixture.JuniorNivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de validación de Codigo debe aparecer en su field-validation span
        // (asp-validation-for="Input.Codigo" → <span data-valmsg-for="Input.Codigo">).
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.CodigoKey)}""[^>]*>[\s\S]*?(?:obligatorio|requerido|required)", RegexOptions.IgnoreCase),
            $"Expected the {CargoFormKeys.CodigoKey} required-field validation message to be rendered.");

        // El API client NO debe haber sido invocado (ModelState cortó antes)
        Assert.Empty(apiClient.CreateCalls);

        // No debe redirigir (se renderiza la misma página con el error)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    // ──────────────────────────────────────────────
    // Bug fix: NivelId vacío en el form de Crear mostraba
    // "The value '' is invalid." (mensaje genérico del model binder de
    // .NET) en lugar del mensaje de [Required] en español. Causa raíz:
    // CargoInputModel.NivelId era Guid (no-nullable); el binder fallaba
    // al convertir "" a Guid antes de evaluar [Required]. Tras hacerlo
    // Guid?, el binder mapea "" → null y [Required] se dispara con
    // "Debe escoger un Nivel.".
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenNivelIdIsEmpty_ShowsSpanishRequiredMessageAndDoesNotRedirect()
    {
        var apiClient = new FakeCargoApiClient();

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-SIN-NIVEL",
            ["Input.Nombre"] = "Cargo Sin Nivel",
            // Input.NivelId ausente (empty string) — equivalente a no elegir opción en el select.
            ["Input.NivelId"] = string.Empty
        }));

        // El form debe re-renderizarse (no PRG) porque ModelState es inválido.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible con los valores enviados.
        Assert.Contains("Nuevo cargo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C-SIN-NIVEL", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cargo Sin Nivel", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de validación debe aparecer en el field-validation span de
        // Input.NivelId (asp-validation-for="Input.NivelId" → <span data-valmsg-for="Input.NivelId">).
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.NivelIdKey)}""[^>]*>[\s\S]*?Debe escoger un Nivel[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the {CargoFormKeys.NivelIdKey} required-field validation message 'Debe escoger un Nivel.' to be rendered.");

        // El mensaje genérico en inglés del model binder de .NET NO debe aparecer.
        Assert.DoesNotContain("The value &#39;&#39; is invalid", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The value '' is invalid", content, StringComparison.OrdinalIgnoreCase);

        // El API client NO debe haber sido invocado (ModelState cortó antes).
        Assert.Empty(apiClient.CreateCalls);
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
                new(CargoWebTestFixture.JuniorNivelId, "JR", "Junior", 1, 1)
            }
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-TRANSPORT",
            ["Input.Nombre"] = "Cargo Transport Fail",
            ["Input.NivelId"] = CargoWebTestFixture.JuniorNivelId.ToString()
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
            $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.CodigoKey)}""[^>]*>([\s\S]*?)</span>",
            RegexOptions.IgnoreCase);
        Assert.True(codigoFieldSpan.Success, $"El field-validation span de {CargoFormKeys.CodigoKey} debe existir.");
        Assert.True(string.IsNullOrWhiteSpace(codigoFieldSpan.Groups[1].Value),
            $"El field-validation span de {CargoFormKeys.CodigoKey} debe estar vacío tras un error de transporte, pero contiene: '{codigoFieldSpan.Groups[1].Value}'.");

        // El catálogo se consultó una vez en GET + una vez tras el POST fallido = 2.
        Assert.Equal(2, apiClient.NivelesCalls);
    }

    [Fact]
    public async Task Post_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        var apiClient = new FakeCargoApiClient();
        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-DENY",
            ["Input.Nombre"] = "Cargo Denegado",
            ["Input.NivelId"] = CargoWebTestFixture.JuniorNivelId.ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.CreateCalls);
    }

    [Fact]
    public async Task Post_Create_WhenTaskCanceledException_ReloadsCatalogAndShowsGeneralError()
    {
        var apiClient = new FakeCargoApiClient
        {
            CreateException = new TaskCanceledException("request canceled"),
            NivelesResult = new List<NivelCargoDto>
            {
                new(CargoWebTestFixture.SeniorNivelId, "SR", "Senior", 2, 2)
            }
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-TIMEOUT",
            ["Input.Nombre"] = "Cargo Timeout",
            ["Input.NivelId"] = CargoWebTestFixture.SeniorNivelId.ToString()
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
                new(CargoWebTestFixture.JuniorNivelId, "JR", "Junior", 1, 1)
            }
        };

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-BADJSON",
            ["Input.Nombre"] = "Cargo Bad Json",
            ["Input.NivelId"] = CargoWebTestFixture.JuniorNivelId.ToString()
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

        await using var lease = await _fixture.CreateCargoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/cargos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/cargos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "C-RT",
            ["Input.Nombre"] = "Cargo Roundtrip",
            ["Input.NivelId"] = CargoWebTestFixture.JuniorNivelId.ToString()
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
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(CargoFormKeys.CodigoKey)}""[^>]*>[\s\S]*?ya existe[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'ya existe' to be rendered inside the {CargoFormKeys.CodigoKey} field-validation span.");
    }
}
