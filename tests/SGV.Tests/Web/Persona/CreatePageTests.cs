using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Comun;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests web del módulo Personas para PR 4/4: formulario de creación
/// (<c>Create</c>). Espejo de <c>CargoCreatePageTests</c>: cubre
/// autorización, carga inicial, éxito (PRG a Details), 400 (FieldErrors),
/// 409 (unicidad) y fallos de transporte.
/// </summary>
[Collection("WebIntegration")]
public sealed class CreatePageTests
{
    private readonly WebIntegrationFixture _fixture;

    public CreatePageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: GET requiere rol Administrador → Forbid/redirect 403
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        await using var lease = await _fixture.CreatePersonaLeaseAsync(new FakePersonaApiClient());

        var response = await lease.Client.GetAsync("/personas/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 2: GET retorna formulario vacío
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticatedAsAdmin_RendersEmptyForm()
    {
        await using var lease = await _fixture.CreatePersonaLeaseAsync(new FakePersonaApiClient(), adminRole: true);

        var response = await lease.Client.GetAsync("/personas/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nueva persona", content, StringComparison.OrdinalIgnoreCase);

        // El formulario debe tener los inputs esperados (espejo _Form.cshtml).
        Assert.Contains($"name=\"{PersonaFormKeys.LegajoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.NombresKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.ApellidosKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.EmailKey}\"", content, StringComparison.OrdinalIgnoreCase);
        // Issue #147 PR3: el <input> legacy para TipoDocumento (string) se
        // reemplazó por un <select name="Input.TipoDocumentoId"> poblado
        // desde GetTiposDocumentoAsync. Sigue siendo un control bindable,
        // solo cambia el nombre del campo wire.
        Assert.Contains($"name=\"{PersonaFormKeys.TipoDocumentoIdKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.NumeroDocumentoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PersonaFormKeys.TelefonoKey}\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 3: POST 201 redirige a Details
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToDetailsWithFeedback()
    {
        var newId = Guid.NewGuid();
        var apiClient = new FakePersonaApiClient
        {
            CreateResult = PersonaCommandResult.Success(
                new PersonaDto(newId, "L-NEW", "Nueva", "Persona", null, null, null, null, null, null, true))
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-NEW",
            ["Input.Nombres"] = "Nueva",
            ["Input.Apellidos"] = "Persona"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/personas/detalle/{newId}", location, StringComparison.OrdinalIgnoreCase);

        var posted = Assert.Single(apiClient.CreateCalls);
        Assert.Equal("L-NEW", posted.Legajo);
        Assert.Equal("Nueva", posted.Nombres);
        Assert.Equal("Persona", posted.Apellidos);
    }

    // ──────────────────────────────────────────────
    // T-XX 4: POST 400 mapea FieldErrors al ModelState con prefijo "Input."
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnInputFields()
    {
        var apiClient = new FakePersonaApiClient
        {
            CreateResult = PersonaCommandResult.Failure(
                new PersonaError(PersonaErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["legajo"] = new[] { "El legajo es obligatorio." },
                    ["email"] = new[] { "Email inválido." }
                })
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-RT",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García"
        }));

        // El form debe re-renderizarse con el mensaje en el field-validation span correspondiente.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PersonaFormKeys.LegajoKey)}""[^>]*>[\s\S]*?El legajo es obligatorio[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'El legajo es obligatorio' to be rendered inside the {PersonaFormKeys.LegajoKey} field-validation span.");
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PersonaFormKeys.EmailKey)}""[^>]*>[\s\S]*?Email inválido[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'Email inválido' to be rendered inside the {PersonaFormKeys.EmailKey} field-validation span.");
    }

    // ──────────────────────────────────────────────
    // T-XX 5: POST 409 preserva datos con feedback claro
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenConflictOnLegajo_RendersGeneralFeedbackAndKeepsForm()
    {
        // AC: 409 (Legajo/Email/NumeroDocumento duplicado) → el PageModel
        // agrega el mensaje del campo afectado al ModelState[string.Empty]
        // para que aparezca en el ValidationSummary, preservando el input
        // del usuario.
        var apiClient = new FakePersonaApiClient
        {
            CreateResult = PersonaCommandResult.Failure(
                new PersonaError(
                    PersonaErrorType.Conflict,
                    "LegajoDuplicado",
                    "Ya existe una persona activa con el legajo L-DUP.",
                    Categoria: ErrorCategoria.Conflict))
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-DUP",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados.
        Assert.Contains("Nueva persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-DUP", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de unicidad aparece en el summary (model error de clave vacía).
        Assert.Contains("Ya existe una persona activa con el legajo L-DUP.", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 6: POST fallo de transporte → estado recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenTransportFails_ShowsRecoverableErrorAndKeepsForm()
    {
        // AC: el PageModel captura HttpRequestException / TaskCanceledException
        // vía TransportFailureClassifier y muestra un mensaje recuperable,
        // preservando el input del usuario.
        var apiClient = new FakePersonaApiClient
        {
            CreateException = new HttpRequestException("boom")
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-TRANSPORT",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados.
        Assert.Contains("Nueva persona", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L-TRANSPORT", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de transporte es visible.
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 7: GET carga el catálogo y renderiza <select> con N opciones
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenCatalogHasFourTipos_RendersSelectWithFourOptions()
    {
        // AC: spec persona-management § "Formulario Create carga TiposDocumento".
        // El PageModel llama a GetTiposDocumentoAsync una vez, y la vista
        // renderiza un <select name="Input.TipoDocumentoId"> con las 4 opciones
        // (DNI/LE/LC/Pasaporte) + el placeholder "Seleccionar tipo…".
        var seed = new List<TipoDocumentoDto>
        {
            new(Guid.Parse("71000000-0000-0000-0000-000000000001"), "DNI", "Documento Nacional de Identidad", "^\\d{7,8}$", 7, 8),
            new(Guid.Parse("71000000-0000-0000-0000-000000000002"), "LE", "Libreta de Enrolamiento", "^\\d{6,8}$", 6, 8),
            new(Guid.Parse("71000000-0000-0000-0000-000000000003"), "LC", "Libreta Cívica", "^\\d{6,8}$", 6, 8),
            new(Guid.Parse("71000000-0000-0000-0000-000000000004"), "Pasaporte", "Pasaporte", "^[A-Za-z]{3}\\d{6}$", 9, 9)
        };
        var apiClient = new FakePersonaApiClient
        {
            TiposDocumentoResult = seed
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/personas/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, apiClient.GetTiposDocumentoCalls);

        // El <select name="Input.TipoDocumentoId"> está presente.
        Assert.Contains("name=\"Input.TipoDocumentoId\"", content, StringComparison.OrdinalIgnoreCase);

        // El placeholder inicial sin selección está presente.
        Assert.Contains("Seleccionar tipo", content, StringComparison.OrdinalIgnoreCase);

        // Las 4 opciones se renderean (4 <option value="...">).
        var optionCount = Regex.Matches(content, "<option[^>]*value=\"71000000", RegexOptions.IgnoreCase).Count;
        Assert.Equal(4, optionCount);

        // Los nombres visibles están presentes.
        Assert.Contains(">DNI<", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">LE<", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">LC<", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Pasaporte<", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenCatalogEmpty_RendersSelectWithOnlyPlaceholder()
    {
        // AC: si el catálogo cae (transport failure) o está vacío, el
        // <select> sigue rendereándose con sólo el placeholder. La vista
        // NO debe tirar 500 ni propagar la excepción.
        var apiClient = new FakePersonaApiClient
        {
            TiposDocumentoResult = Array.Empty<TipoDocumentoDto>()
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/personas/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"Input.TipoDocumentoId\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Seleccionar tipo", content, StringComparison.OrdinalIgnoreCase);
        // Sólo el placeholder — sin opciones del catálogo.
        var optionCount = Regex.Matches(content, "<option[^>]*value=\"71000000", RegexOptions.IgnoreCase).Count;
        Assert.Equal(0, optionCount);
    }

    [Fact]
    public async Task Post_Create_WhenBackendReturnsPatronNoCumplido_RendersErrorSpanAndPreservesForm()
    {
        // AC: spec persona-management § "Feedback de validación server-side
        // en Create/Edit". Cuando el backend responde 400 con FieldErrors
        // (e.g. PATRON_NO_CUMPLIDO sobre NumeroDocumento), el form se
        // re-renderiza preservando los valores y el mensaje de error es
        // visible bajo el campo NumeroDocumento.
        var apiClient = new FakePersonaApiClient
        {
            TiposDocumentoResult = new[]
            {
                new TipoDocumentoDto(Guid.Parse("71000000-0000-0000-0000-000000000001"), "DNI", "DNI", "^\\d{7,8}$", 7, 8)
            },
            CreateResult = PersonaCommandResult.Failure(
                new PersonaError(PersonaErrorType.Validation, "PATRON_NO_CUMPLIDO",
                    "El número de documento no cumple el patrón del tipo seleccionado.",
                    Categoria: ErrorCategoria.Validation),
                new Dictionary<string, string[]>
                {
                    ["numeroDocumento"] = new[] { "El número de documento no cumple el patrón del tipo seleccionado." }
                })
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-PATRON",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García",
            ["Input.TipoDocumentoId"] = "71000000-0000-0000-0000-000000000001",
            ["Input.NumeroDocumento"] = "12A45678"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form preserva los valores enviados y muestra el mensaje bajo
        // el campo NumeroDocumento (asp-validation-for="Input.NumeroDocumento").
        Assert.Contains("L-PATRON", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ana", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("12A45678", content, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PersonaFormKeys.NumeroDocumentoKey)}""[^>]*>[\s\S]*?no cumple el patrón[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend 'PATRON_NO_CUMPLIDO' message to be rendered inside the {PersonaFormKeys.NumeroDocumentoKey} field-validation span.");

        // El catálogo se vuelve a cargar en el path de Page() — el form
        // mantiene la opción "DNI" seleccionada vía asp-for binding.
        Assert.Equal(2, apiClient.GetTiposDocumentoCalls);
    }

    [Fact]
    public async Task Post_Create_WithValidTipoDocumentoId_ExecutesCommandAndInvokesCatalog()
    {
        // AC: cuando el form se submite con TipoDocumentoId válido y el
        // backend responde éxito, el command se ejecuta y el catálogo se
        // cargó exactamente una vez en el GET inicial.
        var newId = Guid.NewGuid();
        var dniId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        var apiClient = new FakePersonaApiClient
        {
            TiposDocumentoResult = new[]
            {
                new TipoDocumentoDto(dniId, "DNI", "DNI", "^\\d{7,8}$", 7, 8)
            },
            CreateResult = PersonaCommandResult.Success(
                new PersonaDto(newId, "L-OK", "Ana", "García", null, dniId, "DNI", "DNI", "12345678", null, true))
        };

        await using var lease = await _fixture.CreatePersonaLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/personas/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/personas/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Legajo"] = "L-OK",
            ["Input.Nombres"] = "Ana",
            ["Input.Apellidos"] = "García",
            ["Input.TipoDocumentoId"] = dniId.ToString(),
            ["Input.NumeroDocumento"] = "12345678"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/personas/detalle/{newId}", location, StringComparison.OrdinalIgnoreCase);

        Assert.Single(apiClient.CreateCalls);
        var posted = apiClient.CreateCalls[0];
        Assert.Equal("L-OK", posted.Legajo);
        Assert.Equal(dniId, posted.TipoDocumentoId);

        // El catálogo se cargó en el GET inicial (no en el POST porque el
        // POST fue éxito y PRG-redirected sin pasar por Page()).
        Assert.Equal(1, apiClient.GetTiposDocumentoCalls);
    }
}