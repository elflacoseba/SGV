using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Tests del slice PR3b: la Razor Page
/// <c>Pages/Organizacion/Cargos/Habilidades.cshtml</c> +
/// su <c>HabilidadesModel</c>. Cubren acceso restringido, hidratación de
/// la grilla, asignación / actualización / baja, manejo de errores
/// recuperables y rechazo por validación de campo. Replica el patrón de
/// <c>CargoEditPageTests</c> usando <see cref="CargoWebTestFixture"/> +
/// <see cref="FakeCargoApiClient"/> + <see cref="FakeHabilidadApiClient"/>.
/// </summary>
public sealed class CargoHabilidadesPageTests : IClassFixture<CargoWebTestFixture>
{
    private readonly CargoWebTestFixture _fixture;

    public CargoHabilidadesPageTests(CargoWebTestFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T3.5 — Acceso restringido (Req 1)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Anonymous_RedirectsToSignIn()
    {
        using var client = _fixture.BaseFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync($"/organizacion/cargos/{Guid.NewGuid()}/habilidades");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_AuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        // El factory fixture existente produce un principal SIN role-claims
        // (el token "token-123" es opaco), por lo que
        // User.IsInRole(RolesSgv.Administrador) devuelve false y la página
        // emite Forbid(). El cookie auth scheme configurado en Program.cs
        // tiene AccessDeniedPath="/error/403", así que Forbid() se traduce
        // a un 302 redirect hacia esa ruta — equivalente observable para
        // el navegador y consistente con el patrón del repo (Forbid en
        // lugar de 403 plano cuando hay sesión autenticada).
        var apiClient = FakeCargoApiClient.WithCargoList();
        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient);

        var response = await client.GetAsync($"/organizacion/cargos/{Guid.NewGuid()}/habilidades");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T3.5 — Carga inicial (Req 2)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_Admin_EmptySkills_RendersEmptyState()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = Array.Empty<CargoSkillDetailDto>();

        using var client = await _fixture.CreateAuthenticatedClientAsync(apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var response = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Estado vacío explícito y visible para que el usuario sepa que el
        // cargo existe pero no tiene habilidades.
        Assert.Contains("no tiene habilidades", content, StringComparison.OrdinalIgnoreCase);
        // El form de "Asignar nueva habilidad" sigue presente aunque la
        // tabla esté vacía (Req 2 escenario "Cargo sin habilidades").
        Assert.Contains("Asignar", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Admin_WithSkills_RendersRowWithNivelRequeridoId()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");

        var nivelBasico = new NivelHabilidadDto(Guid.NewGuid(), "BAS", "Básico", 1, 1);
        var nivelAvanzado = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");

        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = new[]
        {
            new CargoSkillDetailDto(habilidad, nivelBasico)
            {
                SkillId = skillId,
                NivelRequeridoId = nivelAvanzado.Id,
                Ponderacion = 2.50m,
                EsObligatoria = true
            }
        };

        // La grilla re-hidrata el dropdown de niveles a partir del catálogo
        // de habilidades (no del catálogo del vínculo). Sin catálogo, el
        // select de la fila queda vacío y el NivelRequeridoId no aparece
        // en el HTML.
        var habilidadApiClient = new FakeHabilidadApiClient
        {
            NivelesResult = new[] { nivelBasico, nivelAvanzado }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient,
            habilidadApiClient,
            adminRole: true);

        var response = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // La columna "NivelRequerido" expone el NivelRequeridoId del vínculo
        // (memoria #569), nunca un Habilidad.NivelId que no existe.
        Assert.Contains("NivelRequerido", content, StringComparison.OrdinalIgnoreCase);
        // El id del nivel requerido del vínculo viaja como value del select
        // de actualización (anti-drift: NO se usa Habilidad.NivelId).
        // La aserción usa Contains en minúsculas porque Razor no modifica
        // los GUID pero los option tags pueden contener el id con casing
        // variable según la serialización del Guid.
        var guidString = nivelAvanzado.Id.ToString().ToLowerInvariant();
        Assert.Contains(guidString, content, StringComparison.OrdinalIgnoreCase);
        // La ponderación persistida se rehidrata en el input.
        Assert.Contains($@"value=""2.50", content, StringComparison.OrdinalIgnoreCase);
        // La fila expone el nombre del nivel seleccionado para que el
        // usuario entienda qué opción está aplicada sin tener que abrir
        // el dropdown.
        Assert.Contains("Avanzado", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T3.5 — Asignar / Actualizar / Quitar (Req 3, 4)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostAsignar_Admin_CallsUpsertSkillAsync_AndPrgRedirectsWithSuccess()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Success(
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 1.00m, EsObligatoria = false });

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["AsignarInput.SkillId"] = skillId.ToString(),
                ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
                ["AsignarInput.Ponderacion"] = "1.00",
                ["AsignarInput.EsObligatoria"] = "true"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/cargos/{cargoId}/habilidades", location, StringComparison.OrdinalIgnoreCase);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);
        Assert.Equal(nivelId, upsert.Request.NivelRequeridoId);
        Assert.Equal(1.00m, upsert.Request.Ponderacion);
        Assert.True(upsert.Request.EsObligatoria);

        // El PRG debe propagar el TempData que el siguiente GET renderiza.
        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("se asign", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostActualizar_Admin_PropagatesPonderacionYEsObligatoria()
    {
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", null, "Conductual");
        var nivel = new NivelHabilidadDto(nivelId, "AVZ", "Avanzado", 3, 3);

        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = new[]
        {
            new CargoSkillDetailDto(habilidad, nivel)
            {
                SkillId = skillId,
                NivelRequeridoId = nivelId,
                Ponderacion = 1.00m,
                EsObligatoria = false
            }
        };
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Success(
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 2.50m, EsObligatoria = true });

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["NivelRequeridoId"] = nivelId.ToString(),
                ["Ponderacion"] = "2.50",
                ["EsObligatoria"] = "true"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);
        Assert.Equal(nivelId, upsert.Request.NivelRequeridoId);
        Assert.Equal(2.50m, upsert.Request.Ponderacion);
        Assert.True(upsert.Request.EsObligatoria);
    }

    [Fact]
    public async Task PostQuitar_Admin_CallsDeleteSkillAsync_AndPrgRedirectsWithSuccess()
    {
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillDeleteResult = new CargoSkillDeleteResult(true, HttpStatusCode.NoContent, null, null);

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var delete = Assert.Single(apiClient.SkillDeleteCalls);
        Assert.Equal(cargoId, delete.CargoId);
        Assert.Equal(skillId, delete.SkillId);

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());
        Assert.Contains("quit", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T3.5 — Errores recuperables (Req 5)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Post_TransportFailure_ShowsRecoverableMessage_NoStackTrace()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertException = new HttpRequestException("network down");

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["AsignarInput.SkillId"] = skillId.ToString(),
                ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
                ["AsignarInput.Ponderacion"] = "1.00"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje debe ser accionable y NO contener trazas internas.
        Assert.Contains("No se pudo contactar al servicio", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at SGV.", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_BackendReturns400WithPonderacionFieldError_RendersFieldErrorInPage()
    {
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos del vínculo contienen errores de validación."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = new[] { "La ponderación no puede superar 100.00." }
            });

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["AsignarInput.SkillId"] = skillId.ToString(),
                ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
                ["AsignarInput.Ponderacion"] = "150.00"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje específico del backend debe aparecer en el form
        // re-renderizado. Lo esperamos dentro del <span data-valmsg-for=...>
        // asociado al campo Ponderacion (anti-drift: la UI usa el id de
        // NivelRequerido del vínculo, no del catálogo Habilidad).
        Assert.True(
            Regex.IsMatch(content, @"data-valmsg-for=""AsignarInput\.Ponderacion""[^>]*>[\s\S]*?La ponderaci", RegexOptions.IgnoreCase),
            "Expected the Ponderacion field-error to render in the AsignarInput.Ponderacion validation span.");
    }
}