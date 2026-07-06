using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Cargo;
using SGV.Web.Integration.Organizacion;
using Xunit;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Web smoke tests para la página Edit del módulo Puestos (PR 3B).
/// Espejo de <c>CargoEditPageTests</c> ajustado al contrato de Puestos:
/// <list type="bullet">
///   <item>GET prepopula sólo <c>Nombre</c>, <c>Descripcion?</c> y <c>PuestoSuperiorId?</c>; los demás campos son inmutables.</item>
///   <item>POST redirige al Details (no al Index, a diferencia de Create).</item>
///   <item>El HTML de Edit MUST NOT contener <c>name="Input.Codigo"</c>, <c>name="Input.UnidadOrganizativaId"</c> ni <c>name="Input.CargoId"</c> (test RED obligatorio <see cref="Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo"/>).</item>
/// </list>
/// Usa <see cref="SgvWebApplicationFactory"/> + <see cref="FakePuestosApiClient"/> +
/// <see cref="FakeCargoApiClient"/> + <see cref="FakeUnidadOrganizativaApiClient"/>
/// para no requerir MySQL.
/// </summary>
public sealed class PuestoEditPageTests : IClassFixture<PuestoWebTestFixture>
{
    private readonly PuestoWebTestFixture _fixture;

    public PuestoEditPageTests(PuestoWebTestFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 1 — Acceso anónimo redirige a /auth/sign-in
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAnonymous_RedirectsToSignIn()
    {
        // Cliente sin autenticación: usa la base factory sin overrides para
        // que [Authorize] de la página dispare el challenge.
        var client = _fixture.BaseFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync($"/organizacion/puestos/editar/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains("/auth/sign-in", location, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 5 — GET autenticado prepopula Nombre/Descripcion/PuestoSuperiorId
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenAuthenticated_PrepopulatesNombreDescripcionPuestoSuperior()
    {
        var puestoId = Guid.NewGuid();
        var superiorId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var puesto = new PuestoDto(
            puestoId,
            "P-EDIT",
            "Nombre original",
            "Descripción original",
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            superiorId);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[]
            {
                puesto,
                new PuestoDto(
                    superiorId,
                    "P-SUP",
                    "Director Superior",
                    null,
                    Guid.NewGuid(),
                    "Gerencia",
                    Guid.NewGuid(),
                    "Gerente",
                    null)
            }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/puestos/editar/{puestoId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El form debe estar visible y prellenado con los valores editables.
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nombre original", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Descripción original", content, StringComparison.OrdinalIgnoreCase);

        // El dropdown de PuestoSuperiorId debe estar repoblado con CodigoYNombre.
        Assert.Contains("P-SUP", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Director Superior", content, StringComparison.OrdinalIgnoreCase);

        // El dropdown de UnidadOrganizativaId/CargoId NO está en el form de Edit
        // (inmutables), pero el catálogo se carga igual para que el select de
        // PuestoSuperiorId funcione; los nombres aparecen como parte del
        // CodigoYNombre ("P-EDIT — Nombre original").
        Assert.Contains("P-EDIT", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 1 (Scenario "Puesto inexistente en edit") — 404 recuperable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_WhenPuestoNotFound_ShowsRecoverableState()
    {
        // Sin GetByIdResult: GetByIdAsync devuelve null → estado recuperable.
        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = null,
            GetAllResult = Array.Empty<PuestoDto>()
        };
        var missingId = Guid.NewGuid();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/puestos/editar/{missingId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Estado recuperable: mensaje + link "Volver al listado".
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);

        // El form NO debe estar visible (sin datos para editar).
        Assert.DoesNotContain("Nombre original", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 4 — Ausencia de Codigo/UnidadOrganizativaId/CargoId en HTML de Edit
    //
    // TEST RED OBLIGATORIO (design §7, spec `puesto-web-crear-editar` Req 4):
    // El HTML renderizado por Edit MUST NOT incluir los inputs inmutables.
    // Triangulación positiva: Nombre/Descripcion/PuestoSuperiorId SÍ se renderizan.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo()
    {
        var puestoId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var puesto = new PuestoDto(
            puestoId,
            "P-EDIT",
            "Nombre",
            null,
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            null);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/puestos/editar/{puestoId}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // ── Triangulación negativa ──
        // Los inputs inmutables NO deben aparecer en el HTML de Edit.
        Assert.DoesNotMatch(new Regex(@"name=""Input\.Codigo""", RegexOptions.IgnoreCase), content);
        Assert.DoesNotMatch(new Regex(@"name=""Input\.UnidadOrganizativaId""", RegexOptions.IgnoreCase), content);
        Assert.DoesNotMatch(new Regex(@"name=""Input\.CargoId""", RegexOptions.IgnoreCase), content);

        // ── Triangulación positiva ──
        // Los tres campos editables SÍ deben renderizarse.
        Assert.Matches(new Regex(@"name=""Input\.Nombre""", RegexOptions.IgnoreCase), content);
        Assert.Matches(new Regex(@"name=""Input\.Descripcion""", RegexOptions.IgnoreCase), content);
        Assert.Matches(new Regex(@"name=""Input\.PuestoSuperiorId""", RegexOptions.IgnoreCase), content);
    }

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 6 — POST exitoso → PRG a Details con TempData de éxito
    //
    // Diferencia con Create: Edit redirige al Details (no al Index, porque la
    // URL del puesto recién actualizado es la página de detalle). Hard-code
    // `$"/organizacion/puestos/detalles/{id}"` (PR 3C refactoriza a Url.Page).
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation()
    {
        var puestoId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var puesto = new PuestoDto(
            puestoId,
            "P-EDIT",
            "Nombre original",
            "Descripción original",
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            null);
        var updatedPuesto = new PuestoDto(
            puestoId,
            "P-EDIT",
            "Nombre actualizado",
            "Descripción actualizada",
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            null);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto },
            UpdateResult = PuestoCommandResult.Success(updatedPuesto)
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/puestos/editar/{puestoId}");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/puestos/editar/{puestoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Nombre"] = "Nombre actualizado",
            ["Input.Descripcion"] = "Descripción actualizada",
            ["Input.PuestoSuperiorId"] = string.Empty
        }));

        // PRG a Details.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/puestos/detalles/{puestoId}", location, StringComparison.OrdinalIgnoreCase);

        // El payload enviado al API sólo incluye los 3 campos editables;
        // ActualizarPuestoRequest NO tiene Codigo, UnidadOrganizativaId, CargoId.
        var update = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(puestoId, update.Id);
        Assert.Equal("Nombre actualizado", update.Request.Nombre);
        Assert.Equal("Descripción actualizada", update.Request.Descripcion);
        Assert.Null(update.Request.PuestoSuperiorId);
    }

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 6 — POST con FieldErrors del backend → error a nivel de campo
    //
    // El field-error "nombre" debe renderizarse en el span de Input.Nombre.
    // Los campos Codigo/UO/Cargo no existen en Edit, así que NO podemos
    // afirmar errores sobre ellos — la aserción es estrictamente sobre Nombre.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationOnNombre()
    {
        var puestoId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var puesto = new PuestoDto(
            puestoId,
            "P-EDIT",
            "Nombre original",
            null,
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            null);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto },
            UpdateResult = PuestoCommandResult.Failure(
                new PuestoError(PuestoErrorType.Validation, "Validation", "validation failed"),
                new Dictionary<string, string[]>
                {
                    ["nombre"] = new[] { "El nombre es obligatorio." }
                })
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/puestos/editar/{puestoId}");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/puestos/editar/{puestoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Nombre"] = string.Empty,
            ["Input.Descripcion"] = string.Empty,
            ["Input.PuestoSuperiorId"] = string.Empty
        }));

        // El form debe re-renderizarse con el error, sin PRG.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible.
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje de field-error "El nombre es obligatorio" debe quedar en
        // el span de asp-validation-for="Input.Nombre" (mapping vía
        // PuestoFormHelpers.ApplyFieldErrorsToModelState → prefijo "Input.").
        Assert.True(
            Regex.IsMatch(content, $@"<span[^>]*data-valmsg-for=""{Regex.Escape(PuestoFormKeys.NombreKey)}""[^>]*>[\s\S]*?El nombre es obligatorio[\s\S]*?</span>", RegexOptions.IgnoreCase),
            $"Expected the backend field-error message 'El nombre es obligatorio' to be rendered inside the {PuestoFormKeys.NombreKey} field-validation span.");
    }

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 6 — POST con 409 CodigoDuplicado → error general recuperable
    //
    // ActualizarPuestoRequest NO incluye Codigo (inmutable), por lo que un
    // 409 del backend sólo puede llegar por validación interna (e.g.,
    // conflicto de PuestoSuperiorId). Lo cubrimos mapeando el Error.Message
    // general bajo string.Empty vía PuestoPostResultMapper.TryMap.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenCodigoDuplicadoConflict_ShowsSpecificMessageAndKeepsForm()
    {
        var puestoId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var puesto = new PuestoDto(
            puestoId,
            "P-EDIT",
            "Nombre original",
            null,
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            null);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto },
            UpdateResult = PuestoCommandResult.Failure(
                new PuestoError(
                    PuestoErrorType.Conflict,
                    "CodigoDuplicado",
                    "Ya existe un puesto activo con el código P-DUP."))
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/puestos/editar/{puestoId}");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/puestos/editar/{puestoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Nombre"] = "Puesto Duplicado",
            ["Input.Descripcion"] = string.Empty,
            ["Input.PuestoSuperiorId"] = string.Empty
        }));

        // El form debe re-renderizarse con el error, sin PRG.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form debe seguir visible con los valores enviados.
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Puesto Duplicado", content, StringComparison.OrdinalIgnoreCase);

        // El mensaje específico del conflict debe aparecer (PuestoCommandResult
        // no tiene Codigo duplicado específico en Edit porque Codigo no es
        // editable; el mapper lo aplica como error general recuperable).
        Assert.Contains("Ya existe un puesto activo con el código P-DUP", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Spec 3B.1 · Req 6 — POST con HttpRequestException → error recuperable sin 500
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_Edit_WhenTransportFails_ShowsRecoverableError()
    {
        var puestoId = Guid.NewGuid();
        var unidadId = PuestoWebTestFixture.SampleUnidadOrganizativaId;
        var cargoId = PuestoWebTestFixture.SampleCargoId;
        var puesto = new PuestoDto(
            puestoId,
            "P-EDIT",
            "Nombre original",
            null,
            unidadId,
            "Comercial",
            cargoId,
            "Vendedor",
            null);

        var apiClient = new FakePuestosApiClient
        {
            GetByIdResult = puesto,
            GetAllResult = new[] { puesto },
            UpdateException = new HttpRequestException("api caída")
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var getResponse = await client.GetAsync($"/organizacion/puestos/editar/{puestoId}");
        var antiforgeryToken = await PuestoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync($"/organizacion/puestos/editar/{puestoId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.Nombre"] = "Puesto Transport Fail",
            ["Input.Descripcion"] = string.Empty,
            ["Input.PuestoSuperiorId"] = string.Empty
        }));

        // El handler debe atrapar la excepción de transporte y responder 200
        // con el form re-renderizado, no propagar como 500.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El form sigue visible para que el usuario pueda reintentar.
        Assert.Contains("Editar", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Puesto Transport Fail", content, StringComparison.OrdinalIgnoreCase);

        // El banner rojo de error recuperable debe estar visible.
        Assert.Contains("No se pudo contactar al servicio de puestos", content, StringComparison.OrdinalIgnoreCase);

        // El payload se envió antes de que la fake lanzara la excepción.
        var update = Assert.Single(apiClient.UpdateCalls);
        Assert.Equal(puestoId, update.Id);
        Assert.Equal("Puesto Transport Fail", update.Request.Nombre);
    }
}