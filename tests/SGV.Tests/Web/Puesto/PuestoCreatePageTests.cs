using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Cargo;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Web smoke tests para la página Create del módulo Puestos (PR 3A).
/// Espejo de <c>CargoCreatePageTests</c> ajustado a:
/// <list type="bullet">
///   <item>6 campos renderizados (Codigo, Nombre, Descripcion, UnidadOrganizativaId, CargoId, PuestoSuperiorId).</item>
///   <item>3 catálogos cargados en paralelo vía <c>Task.WhenAll</c>.</item>
///   <item>POST éxito redirige al listado (no al detalle, a diferencia de Cargos).</item>
///   <item>Mapeo de <see cref="PuestoErrorType.Conflict"/> con código <c>CodigoDuplicado</c> al campo Codigo.</item>
/// </list>
/// Usa <see cref="SgvWebApplicationFactory"/> + <see cref="FakePuestosApiClient"/> +
/// <see cref="FakeCargoApiClient"/> + <see cref="FakeUnidadOrganizativaApiClient"/>
/// para no requerir MySQL.
/// </summary>
[Collection("WebIntegration")]
public sealed class PuestoCreatePageTests
{
    private readonly WebIntegrationFixture _fixture;

    public PuestoCreatePageTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 1 — Acceso anónimo redirige a /auth/sign-in
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAnonymous_RedirectsToSignIn()
    {
        // Cliente sin autenticación: el lease anónimo dispara el challenge de
        // [Authorize] en la página Create sin requerir overrides adicionales.
        await using var lease = await _fixture.CreateAnonymousLeaseAsync();

        var response = await lease.Client.GetAsync("/organizacion/puestos/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("/auth/sign-in", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        await using var lease = await _fixture.CreatePuestoLeaseAsync(new FakePuestosApiClient());

        var response = await lease.Client.GetAsync("/organizacion/puestos/crear");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 2 — Render de los seis campos editables
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticated_FormContainsAllSixFields()
    {
        var apiClient = new FakePuestosApiClient();
        var cargoClient = FakeCargoApiClient.WithCargoList(
            new CargoDto(Guid.NewGuid(), "C-VEND", "Vendedor", null, Guid.NewGuid(), "Ventas"));
        var unidadClient = new FakeUnidadOrganizativaApiClient
        {
            AllActivasResult =
            [
                new UnidadOrganizativaDto(
                    WebTestBuilders.SampleUnidadOrganizativaId,
                    "UO-001",
                    "Comercial",
                    Guid.NewGuid(),
                    "Área",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
            ]
        };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, unidadClient, cargoClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Los seis campos editables deben estar presentes en el form.
        Assert.Contains($"name=\"{PuestoFormKeys.CodigoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.NombreKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.DescripcionKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.UnidadOrganizativaIdKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.CargoIdKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.PuestoSuperiorIdKey}\"", content, StringComparison.OrdinalIgnoreCase);

        // El catálogo de cargos debe popular el select.
        Assert.Contains("Vendedor", content, StringComparison.OrdinalIgnoreCase);
        // El catálogo de unidades debe popular el select sin usar el workaround paginado del PageModel.
        Assert.Contains("Comercial", content, StringComparison.OrdinalIgnoreCase);
        Assert.Single(unidadClient.GetAllActivasCalls);
        Assert.Empty(unidadClient.QueryCalls);

        // El catálogo de puestos debe haber sido consultado exactamente una vez.
        Assert.Single(apiClient.GetAllCalls);
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 3 — PuestoSuperiorId poblado con N+1 opciones
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenPuestosCatalogHasResults_SelectContainsNPlusOneOptions()
    {
        // Sembramos 3 puestos: la opción vacía "Sin puesto superior" + 3 = 4 opciones.
        var seededPuestos = new[]
        {
            WebTestBuilders.BuildPuestoDto("P-001", "Director"),
            WebTestBuilders.BuildPuestoDto("P-002", "Gerente"),
            WebTestBuilders.BuildPuestoDto("P-003", "Analista")
        };
        var apiClient = new FakePuestosApiClient { GetAllResult = seededPuestos };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El dropdown CodigoYNombre del PuestoSuperiorId debe contener los 3 puestos sembrados.
        // Buscamos el bloque <select name="Input.PuestoSuperiorId"> y contamos las <option>.
        var selectMatch = Regex.Match(
            content,
            $@"<select[^>]*name=""{Regex.Escape(PuestoFormKeys.PuestoSuperiorIdKey)}""[^>]*>([\s\S]*?)</select>",
            RegexOptions.IgnoreCase);
        Assert.True(selectMatch.Success, $"El select {PuestoFormKeys.PuestoSuperiorIdKey} debe estar renderizado.");

        var selectBody = selectMatch.Groups[1].Value;
        var optionCount = Regex.Matches(selectBody, @"<option", RegexOptions.IgnoreCase).Count;

        // N puestos sembrados + 1 opción vacía "Sin puesto superior" = N+1.
        Assert.Equal<int>(seededPuestos.Length + 1, optionCount);

        // Cada opción debe mostrar el formato Codigo — Nombre.
        Assert.Contains("Director", selectBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Gerente", selectBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analista", selectBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P-001", selectBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P-002", selectBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P-003", selectBody, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 3 (recuperable) — Catálogo de puestos caído
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenPuestosCatalogFails_ShowsRecoverableError()
    {
        var apiClient = new FakePuestosApiClient
        {
            GetAllException = new HttpRequestException("catálogo caído")
        };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // La página debe seguir renderizando (sin 500) para permitir reintento manual.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No se pudo cargar el catálogo", content, StringComparison.OrdinalIgnoreCase);

        // El form debe seguir visible con todos los campos para que el usuario pueda reintentar.
        Assert.Contains($"name=\"{PuestoFormKeys.CodigoKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.NombreKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.UnidadOrganizativaIdKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.CargoIdKey}\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"name=\"{PuestoFormKeys.PuestoSuperiorIdKey}\"", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 6 — POST exitoso → PRG a Index con banner
    //
    // Diferencia con Cargos: Puestos redirige al Listado (no al Detalle),
    // porque a diferencia de Cargo, no se conserva un identificador "antes
    // de la creación" — el usuario llega al listado y puede buscar el nuevo.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenSuccessful_RedirectsToListadoWithConfirmation()
    {
        var unidadId = WebTestBuilders.SampleUnidadOrganizativaId;
        var cargoId = WebTestBuilders.SampleCargoId;
        var newPuestoId = Guid.NewGuid();
        var apiClient = new FakePuestosApiClient
        {
            CreateResult = PuestoCommandResult.Success(
                new PuestoDto(
                    newPuestoId,
                    "P-NEW",
                    "Nuevo Puesto",
                    null,
                    unidadId,
                    "Comercial",
                    cargoId,
                    "Vendedor",
                    null))
        };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "P-NEW",
            ["Input.Nombre"] = "Nuevo Puesto",
            ["Input.Descripcion"] = string.Empty,
            ["Input.UnidadOrganizativaId"] = unidadId.ToString(),
            ["Input.CargoId"] = cargoId.ToString(),
            ["Input.PuestoSuperiorId"] = string.Empty
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("/organizacion/puestos", location, StringComparison.OrdinalIgnoreCase);

        // El request al API debe haber sido exactamente uno.
        var posted = Assert.Single(apiClient.CreateCalls);
        Assert.Equal("P-NEW", posted.Codigo);
        Assert.Equal("Nuevo Puesto", posted.Nombre);
        Assert.Equal(unidadId, posted.UnidadOrganizativaId);
        Assert.Equal(cargoId, posted.CargoId);
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 6 — POST con FieldErrors (400 ValidationProblemDetails)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnCodigo()
    {
        var unidadId = WebTestBuilders.SampleUnidadOrganizativaId;
        var cargoId = WebTestBuilders.SampleCargoId;
        var apiClient = new FakePuestosApiClient
        {
            CreateResult = PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["codigo"] = new[] { "ya existe" }
                })
        };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "P-RT",
            ["Input.Nombre"] = "Puesto Roundtrip",
            ["Input.UnidadOrganizativaId"] = unidadId.ToString(),
            ["Input.CargoId"] = cargoId.ToString()
        }));

        // El form debe re-renderizarse con el error, sin PRG.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Nuevo puesto", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de field-error "ya existe" debe quedar en el span de
        // asp-validation-for="Input.Codigo" (mapping vía
        // PuestoFormHelpers.ApplyFieldErrorsToModelState → prefijo "Input.").
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PuestoFormKeys.CodigoKey)}""[^>]*>[\s\S]*?ya existe[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'ya existe' to be rendered inside the {PuestoFormKeys.CodigoKey} field-validation span.");
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 6 — POST con CodigoDuplicado (409 Conflict)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm()
    {
        var unidadId = WebTestBuilders.SampleUnidadOrganizativaId;
        var cargoId = WebTestBuilders.SampleCargoId;
        var apiClient = new FakePuestosApiClient
        {
            CreateResult = PuestoCommandResult.Failure(
                new PuestoError(
                    PuestoErrorType.Conflict,
                    "CodigoDuplicado",
                    "Ya existe un puesto activo con el código P-DUP."))
        };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "P-DUP",
            ["Input.Nombre"] = "Puesto Duplicado",
            ["Input.UnidadOrganizativaId"] = unidadId.ToString(),
            ["Input.CargoId"] = cargoId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible con los valores enviados.
        Assert.Contains("Nuevo puesto", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P-DUP", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Puesto Duplicado", content, StringComparison.OrdinalIgnoreCase);

        // El campo Input.Codigo debe contener el mensaje de duplicado (NO el
        // catálogo, NO un alert general). El span asp-validation-for se
        // renderiza como <span data-valmsg-for="Input.Codigo">.</span>.
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PuestoFormKeys.CodigoKey)}""[^>]*>[\s\S]*?Ya existe un puesto activo con el código P-DUP\.[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the duplicate-Codigo conflict message to be rendered in the {PuestoFormKeys.CodigoKey} field-validation span.");

        // El catálogo debe haberse recargado para que el dropdown siga funcional.
        Assert.Equal<int>(2, apiClient.GetAllCalls.Count);
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 6 — POST con HttpRequestException (transporte caído)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Create_WhenHttpRequestException_ReloadsCatalogAndShowsGeneralError()
    {
        var unidadId = WebTestBuilders.SampleUnidadOrganizativaId;
        var cargoId = WebTestBuilders.SampleCargoId;
        var apiClient = new FakePuestosApiClient
        {
            CreateException = new HttpRequestException("api caída")
        };

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "P-TRANSPORT",
            ["Input.Nombre"] = "Puesto Transport Fail",
            ["Input.UnidadOrganizativaId"] = unidadId.ToString(),
            ["Input.CargoId"] = cargoId.ToString()
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible con los valores enviados (preserva input).
        Assert.Contains("Nuevo puesto", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("P-TRANSPORT", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Puesto Transport Fail", content, StringComparison.OrdinalIgnoreCase);

        // El catálogo de puestos debe haber sido recargado tras el fallo.
        Assert.Equal<int>(2, apiClient.GetAllCalls.Count);
    }

    // ──────────────────────────────────────────────
    // Spec 3A.1 · Req 7 — Sidenav muestra entry Nuevo con active state
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Create_WhenAuthenticated_SidenavShowsNuevoEntryWithActiveState()
    {
        var apiClient = new FakePuestosApiClient();

        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient, adminRole: true);

        var response = await lease.Client.GetAsync("/organizacion/puestos/crear");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El submenú "Puestos" debe contener un item "Nuevo" con href correcto.
        Assert.Contains("href=\"/organizacion/puestos/crear\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">Nuevo<", content, StringComparison.OrdinalIgnoreCase);

        // El grupo padre "Puestos" debe estar marcado como active (propagación por
        // StartsWithSegments: la variable Razor `puestosActive` se renderiza como
        // `active` cuando el path empieza con `/organizacion/puestos`).
        Assert.True(
            Regex.IsMatch(content, @"<a[^>]*aria-controls=""puestos""[^>]*class=""[^""]*\bactive\b[^""]*""", RegexOptions.IgnoreCase),
            "Expected the Puestos sidenav group toggle link to be marked as active when on /organizacion/puestos/crear.");
    }

    [Fact]
    public async Task Post_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        var apiClient = new FakePuestosApiClient();
        await using var lease = await _fixture.CreatePuestoLeaseAsync(apiClient);

        var getResponse = await lease.Client.GetAsync("/organizacion/puestos");
        var antiforgeryToken = await WebTestBuilders.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await lease.Client.PostAsync("/organizacion/puestos/crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Codigo"] = "P-DENY",
            ["Input.Nombre"] = "Puesto Denegado",
            ["Input.UnidadOrganizativaId"] = WebTestBuilders.SampleUnidadOrganizativaId.ToString(),
            ["Input.CargoId"] = WebTestBuilders.SampleCargoId.ToString()
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(apiClient.CreateCalls);
    }
}
