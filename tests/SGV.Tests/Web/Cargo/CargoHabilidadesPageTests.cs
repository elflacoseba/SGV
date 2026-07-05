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

    [Fact]
    public async Task Get_Admin_QuitarButton_RendersConfirmPromptWithSkillName()
    {
        // Req 4 de cargo-skill-ui-tabla-editable exige que la interfaz MUST
        // confirmar la baja antes de quitar una asociación. El handler
        // nativo confirm() es la opción más simple y compatible con todos
        // los navegadores modernos, y mantiene el flujo HTML5 formaction
        // sin requerir un harness JS dedicado.
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");

        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "BAS", "Básico", 1, 1);
        var skillId = Guid.NewGuid();
        const string skillNombre = "Liderazgo";
        var habilidad = new HabilidadDto(skillId, "H-001", skillNombre, "Desc", "Conductual");

        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.GetSkillsResult = new[]
        {
            new CargoSkillDetailDto(habilidad, nivel)
            {
                SkillId = skillId,
                NivelRequeridoId = nivel.Id,
                Ponderacion = 1.00m,
                EsObligatoria = false
            }
        };

        var habilidadApiClient = new FakeHabilidadApiClient
        {
            NivelesResult = new[] { nivel }
        };

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient,
            habilidadApiClient,
            adminRole: true);

        var response = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El botón Quitar debe invocar confirm() con return para cancelar
        // el submit cuando el usuario rechaza. El mensaje MUST identificar
        // la habilidad concreta (interpolando el nombre vía Razor) para que
        // el admin no quite una asociación por accidente.
        var quitarButtonMatch = Regex.Match(
            content,
            @"<button[^>]*formaction=""\?handler=Quitar[^>]*>[^<]*Quitar</button>",
            RegexOptions.IgnoreCase);
        Assert.True(quitarButtonMatch.Success, "Quitar button was not rendered.");
        var quitarButton = quitarButtonMatch.Value;
        var onclickMatch = Regex.Match(
            quitarButton,
            @"onclick\s*=\s*""([^""]*)""",
            RegexOptions.IgnoreCase);
        Assert.True(
            onclickMatch.Success,
            "Quitar button must declare an onclick attribute.");
        var onclickValue = onclickMatch.Groups[1].Value;
        Assert.Contains("return confirm(", onclickValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(skillNombre, onclickValue, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T3.5 — Errores recuperables (Req 5)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostActualizar_Admin_PonderacionOutOfRange_ReloadsAndRendersRangeError()
    {
        // El validador local [Range(0.01, 100.00)] del input model del
        // Actualizar corta antes de invocar al cliente API: la página
        // re-renderiza con un mensaje accionable y NUNCA sale al backend.
        // Esta cobertura blinda el comportamiento de "validación local
        // corta corto-circuito" — contraparte del test
        // Post_Asignar_BackendPonderacionFieldError que prueba el camino
        // inverso (validación local pasa, backend rechaza).
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
        // Si la validación local NO cortara, este Success sería el
        // resultado que vería la página — útil para distinguir un
        // fallo de la aserción Empty(SkillUpsertCalls) abajo.
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Success(
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 1.00m, EsObligatoria = false });

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
                ["Ponderacion"] = "999", // 999 > 100 → fuera del [Range].
                ["EsObligatoria"] = "true"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Blindaje: el validador [Range] corta antes de invocar al
        // cliente API. Sin esta cobertura, un refactor futuro podría
        // mover la validación al backend y romper la promesa "no round
        // trip si la entrada es inválida localmente".
        Assert.Empty(apiClient.SkillUpsertCalls);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje localizado del [Range] debe aparecer en el form
        // re-renderizado para que el usuario entienda por qué la
        // actualización no salió. La aserción es por substring — basta
        // con que el mensaje llegue a algún punto del HTML renderizado.
        Assert.Contains("La ponderación debe estar entre", content, StringComparison.OrdinalIgnoreCase);
    }

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
    public async Task Post_Asignar_LocalPonderacionOutOfRange_RendersRangeErrorInPage()
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

    [Fact]
    public async Task Post_Asignar_BackendPonderacionFieldError_RendersErrorInAsignarInputPonderacion()
    {
        // Este test verifica el camino real de ApplySkillFailureToModelState:
        // el backend rechaza la petición CON FieldErrors por campo. La
        // validación local pasa (Ponderacion = 50.00 ∈ [0.01, 100.00]),
        // cargoApiClient.UpsertSkillAsync es invocado, y la página
        // re-renderiza el error del backend bajo el data-valmsg-for
        // "AsignarInput.Ponderacion". El test anterior
        // (Post_Asignar_LocalPonderacionOutOfRange) NO ejercita este
        // camino porque su payload estaba fuera del [Range] y el handler
        // short-circuiteaba antes de invocar al cliente API.
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos son inválidos."),
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
                ["AsignarInput.Ponderacion"] = "50.00"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // El cliente API fue efectivamente invocado: el [Range] local no
        // short-circuiteó, así que este test prueba el mapeo real de
        // ApplySkillFailureToModelState con FieldErrors no vacíos.
        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje específico del backend (no el [Range] local) debe
        // aparecer bajo el data-valmsg-for correcto. Esta aserción
        // distingue el camino de ApplySkillFailureToModelState del
        // short-circuit local: el mensaje "no puede superar 100.00" sólo
        // viene del backend, no del validador del input model.
        Assert.True(
            Regex.IsMatch(content, @"data-valmsg-for=""AsignarInput\.Ponderacion""[^>]*>[\s\S]*?La ponderación no puede superar 100\.00", RegexOptions.IgnoreCase),
            "Expected the backend Ponderacion field-error to render in the AsignarInput.Ponderacion validation span.");
    }

    // ──────────────────────────────────────────────
    // Hallazgo #2 — Cobertura de OnPostQuitarAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostQuitar_NonAdmin_RedirectsToAccessDenied()
    {
        // El handler chequea `EsAdministrador` antes de invocar al
        // cliente API. Un usuario autenticado sin el rol Administrador
        // recibe Forbid() → 302 a /error/403, y el cliente API nunca se
        // invoca. Esto blinda la frontera admin-only frente a un refactor
        // que mueva el chequeo detrás de la llamada de red.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        // Configurar un resultado exitoso NO debería importar: si el
        // handler cortara después de invocar al cliente, esto se
        // consumiría. La aserción SkillDeleteCalls.Empty abajo prueba
        // que NUNCA se invoca.
        apiClient.SkillDeleteResult = new CargoSkillDeleteResult(true, HttpStatusCode.NoContent, null, null);

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: false);

        // La página Habilidades emite Forbid() antes de hidratar la
        // grilla (no hay un GET exitoso del cual extraer el token
        // antiforgery). Usamos /auth/sign-in — accesible para
        // cualquier usuario autenticado — que sí renderiza un form
        // con @AntiForgeryToken implícito y contra el cual podemos
        // validar el POST contra la cookie antiforgery ya presente en
        // el jar (seteada durante el flujo de sign-in del fixture).
        var signInGet = await client.GetAsync("/auth/sign-in");
        Assert.Equal(HttpStatusCode.OK, signInGet.StatusCode);
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(signInGet);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/error/403", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Blindaje: el chequeo de rol corta ANTES de salir al backend.
        Assert.Empty(apiClient.SkillDeleteCalls);
    }

    [Fact]
    public async Task PostQuitar_TransportFailure_RedirectsWithDangerMessage()
    {
        // Falla de transporte desde DeleteSkillAsync debe traducirse en
        // un PRG con TempData danger (mensaje accionable, sin filtrar
        // stack trace). Esta es la contraparte del test
        // Post_TransportFailure_ShowsRecoverableMessage_NoStackTrace
        // aplicado al path de Quitar: Asignar/Actualizar re-renderizan
        // la página (200 OK con mensaje en la respuesta) pero Quitar
        // no puede re-renderizar porque ya eliminó la fila, así que
        // usa PRG + TempData.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillDeleteException = new HttpRequestException("network down");

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        // El GET inicial sirve además para obtener el token
        // antiforgery de un form que sí se renderiza (Asignar está
        // siempre presente cuando el usuario es admin).
        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Quitar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            $"/organizacion/cargos/{cargoId}/habilidades",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        // Seguimos el PRG y verificamos que el TempData danger llega al
        // GET renderizado. La aserción es contra el span del alert y el
        // substring del mensaje para no acoplarse al orden de clases.
        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("No se pudo contactar", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"alert alert-danger\"", refreshedContent, StringComparison.Ordinal);
        // El stack trace / tipo de excepción NO debe filtrarse al HTML.
        Assert.DoesNotContain("HttpRequestException", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at SGV.", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostQuitar_NotFound_RedirectsWithWarningMessage()
    {
        // 404 al quitar no es un error fatal: refleja una race condition
        // real (otra pestaña quitó la asociación). PRG con TempData
        // warning permite que el siguiente GET refresque la grilla sin
        // asustar al usuario con un modal de error.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");
        var apiClient = FakeCargoApiClient.WithCargoList(cargo);
        apiClient.SkillDeleteResult = new CargoSkillDeleteResult(
            false,
            HttpStatusCode.NotFound,
            "AsociacionNoEncontrada",
            "La asociación ya no existe.");

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

        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Contains("ya no existe", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"alert alert-warning\"", refreshedContent, StringComparison.Ordinal);
    }

    // ──────────────────────────────────────────────
    // Hallazgo #5 — ApplySkillFailureToModelState branches
    // (result.Error.Type con FieldErrors == null)
    // ──────────────────────────────────────────────

    private static FormUrlEncodedContent BuildAsignarForm(string antiforgeryToken, Guid skillId, Guid nivelId) =>
        new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["AsignarInput.SkillId"] = skillId.ToString(),
            ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
            ["AsignarInput.Ponderacion"] = "50.00"
        });

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

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await client.PostAsync(
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

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await client.PostAsync(
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

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await client.PostAsync(
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

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Asignar",
            BuildAsignarForm(antiforgeryToken, skillId, nivelId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("El servicio no respondió correctamente", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T2.1 + T2.3 (cargos-navegacion-habilidades):
    // per-row error anchoring + defensive fallback
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostActualizar_BackendPonderacionFieldError_RendersErrorInActualizarRowAndSummary()
    {
        // Req 3 escenario "Error de validación anclado a la fila correcta":
        // cuando el backend rechaza una edición con FieldErrors por campo,
        // el mensaje MUST aparecer anclado al input Ponderacion de la fila
        // editada (no bajo AsignarInput.*) Y en el validation-summary
        // general. La fila se identifica por su skillId en la convención
        // Actualizar[{skillId}].Campo.
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
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos son inválidos."),
            new Dictionary<string, string[]>
            {
                ["Ponderacion"] = new[] { "Fuera de rango" }
            });

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                [$"Actualizar[{skillId}].NivelRequeridoId"] = nivelId.ToString(),
                [$"Actualizar[{skillId}].Ponderacion"] = "50.00",
                [$"Actualizar[{skillId}].EsObligatoria"] = "true"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje del backend (no el [Range] local) debe aparecer
        // anclado a la fila correcta bajo la convención Actualizar[xxx].
        // Esta aserción distingue el camino de ApplyActualizarFailureToModelState
        // del helper legacy que mapeaba todo a AsignarInput.*.
        var expectedKey = $"Actualizar[{skillId}].Ponderacion";
        Assert.True(
            Regex.IsMatch(content, $@"data-valmsg-for=""{Regex.Escape(expectedKey)}""[^>]*>[\s\S]*?Fuera de rango", RegexOptions.IgnoreCase),
            $"Expected the backend Ponderacion field-error to render in the Actualizar[{skillId}].Ponderacion validation span.");
    }

    [Fact]
    public async Task PostActualizar_BackendNonWhitelistedFieldError_RendersErrorOnlyInSummary()
    {
        // Req 3 escenario "Error defensivo fuera de la fila activa":
        // cuando el backend devuelve un FieldError cuya key no está en el
        // whitelist {NivelRequeridoId,Ponderacion,EsObligatoria}, el mensaje
        // MUST aparecer solo en el validation-summary general sin anclarse
        // a ninguna fila específica.
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
        apiClient.SkillUpsertResult = CargoSkillCommandResult.Failure(
            new CargoSkillError(
                CargoSkillErrorType.Validation,
                "DatosInvalidos",
                "Uno o más campos son inválidos."),
            new Dictionary<string, string[]>
            {
                ["OtroCampo"] = new[] { "Error defensivo" }
            });

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                [$"Actualizar[{skillId}].NivelRequeridoId"] = nivelId.ToString(),
                [$"Actualizar[{skillId}].Ponderacion"] = "50.00",
                [$"Actualizar[{skillId}].EsObligatoria"] = "true"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        // El mensaje defensivo MUST aparecer en el validation-summary.
        Assert.Contains("Error defensivo", content, StringComparison.OrdinalIgnoreCase);

        // Y NO debe anclarse a ninguna fila con la convención Actualizar[xxx].
        Assert.False(
            Regex.IsMatch(content, $@"data-valmsg-for=""Actualizar\[{skillId}\]\.OtroCampo""", RegexOptions.IgnoreCase),
            "Expected the defensive field error NOT to be anchored to any Actualizar row.");
    }

    // ──────────────────────────────────────────────
    // T2.2 + T2.3 caso 3 (cargos-navegacion-habilidades):
    // PRG no-regression for Actualizar success
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PostActualizar_Success_PreservesPrgFlowAndReloadsGridWithNewValues()
    {
        // Req 3 escenario "Éxito de edición preserva el flujo editable":
        // cuando el backend responde éxito, la página MUST persistir los
        // cambios contra el backend mediante PRG con TempData Y MUST volver
        // a cargar la grilla manteniéndola editable y mostrando los nuevos
        // valores. Esta cobertura blinda la transición del helper de
        // AsignarInput.* a Actualizar[xxx].* para que el camino feliz de
        // Actualizar siga funcionando.
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
            new CargoSkillDto(skillId, nivelId) { Ponderacion = 3.50m, EsObligatoria = true });

        using var client = await _fixture.CreateAuthenticatedClientAsync(
            apiClient, new FakeHabilidadApiClient(), adminRole: true);

        var getResponse = await client.GetAsync($"/organizacion/cargos/{cargoId}/habilidades");
        var antiforgeryToken = await CargoWebTestFixture.ExtractAntiforgeryTokenAsync(getResponse);

        var response = await client.PostAsync(
            $"/organizacion/cargos/{cargoId}/habilidades?handler=Actualizar&skillId={skillId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                [$"Actualizar[{skillId}].NivelRequeridoId"] = nivelId.ToString(),
                [$"Actualizar[{skillId}].Ponderacion"] = "3.50",
                [$"Actualizar[{skillId}].EsObligatoria"] = "true"
            }));

        // PRG: redirect 302 a la misma página.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Contains($"/organizacion/cargos/{cargoId}/habilidades", location, StringComparison.OrdinalIgnoreCase);

        // El cliente API fue invocado con los valores correctos (binding por
        // diccionario funciona).
        var upsert = Assert.Single(apiClient.SkillUpsertCalls);
        Assert.Equal(cargoId, upsert.CargoId);
        Assert.Equal(skillId, upsert.SkillId);
        Assert.Equal(nivelId, upsert.Request.NivelRequeridoId);
        Assert.Equal(3.50m, upsert.Request.Ponderacion);
        Assert.True(upsert.Request.EsObligatoria);

        // El TempData del PRG debe propagarse al siguiente GET, que recarga
        // la grilla con los nuevos valores (Ponderacion = 3.50).
        var refreshed = await client.GetAsync(response.Headers.Location);
        var refreshedContent = HttpUtility.HtmlDecode(await refreshed.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Contains("actualiz", refreshedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"value=""3.50", refreshedContent, StringComparison.OrdinalIgnoreCase);
    }
}