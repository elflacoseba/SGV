using System.Net;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Aplicacion.Seguridad.Usuarios;
using SGV.Tests.Web.Cargo;
using SGV.Web.Integration.Auth;
using SGV.Web.Integration.Habilidades;
using Xunit;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Tests de la Razor Page readonly <c>Pages/Organizacion/Habilidades/Cargos.cshtml</c>
/// introducida por el change <c>habilidades-navegacion-cargos</c> (WU-B).
/// Cubren los escenarios del design §5.3:
///   - Carga inicial con habilidad existente y cargos.
///   - Habilidad inexistente → estado recuperable.
///   - Status inválido (<c>archivo</c>) cae a <c>activas</c>.
///   - Paginación preserva contexto (page/pageSize).
///   - Gating admin: el botón "Gestionar habilidades del cargo" sólo se
///     renderiza cuando el usuario pertenece al rol Administrador.
/// </summary>
public sealed class HabilidadesCargosModelTests
{
    // ──────────────────────────────────────────────
    // T9 (habilidades-navegacion-cargos WU-B): PageModel
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Get_CargosPage_Anonymous_RedirectsToSignIn()
    {
        using var factory = new SgvWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var response = await client.GetAsync($"/organizacion/habilidades/{Guid.NewGuid()}/cargos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/auth/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CargosPage_ExistingSkillWithCargos_RendersTableWithItems()
    {
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");

        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.GetCargosHandler = (id, _) =>
        {
            Assert.Equal(skillId, id);
            return new PagedResult<SkillCargoDetailDto>(
                new[]
                {
                    new SkillCargoDetailDto(cargo, nivel)
                    {
                        CargoId = cargoId,
                        NivelRequeridoId = nivel.Id,
                        Ponderacion = 2.50m,
                        EsObligatoria = true,
                        CargoEliminado = false,
                    }
                },
                1, 1, 20);
        };

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cargos asociados a la habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Liderazgo", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C-001", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Director", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avanzado", content, StringComparison.OrdinalIgnoreCase);

        // El botón "Detalle del cargo" (siempre visible) debe estar presente.
        Assert.Contains(
            $"href=\"/organizacion/cargos/detalles/{cargoId}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"aria-label=\"Detalle de {cargo.Nombre}\"",
            content,
            StringComparison.OrdinalIgnoreCase);

        // El subrecurso fue invocado exactamente una vez con defaults normalizados.
        var call = Assert.Single(apiClient.GetCargosCalls);
        Assert.Equal(skillId, call.SkillId);
        Assert.Equal(HabilidadSegmentoListado.Activas, call.Query.Segmento);
        Assert.Equal(1, call.Query.Page);
        Assert.Equal(20, call.Query.PageSize);
    }

    [Fact]
    public async Task Get_CargosPage_NonExistingSkill_RendersRecoverableState()
    {
        // Convención vigente en Habilidades/Details y Cargos/Details: cuando
        // la entidad padre no existe, la página renderiza un estado
        // recuperable con un mensaje accionable, NO redirige y NO devuelve
        // 404 plano (eso lo hace la API; la página se autocontiene).
        var skillId = Guid.NewGuid();
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(); // sin seed
        apiClient.GetByIdHandler = _ => null; // explícito: GetByIdAsync retorna null

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no está disponible", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        // Sin fila de la grilla.
        Assert.DoesNotContain("Cargos asociados a la habilidad", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CargosPage_InvalidStatus_FallsBackToActivas()
    {
        // Req skill-cargo-query-contract Req 2 escenario "Status inválido
        // cae a activas": status=archivo debe resolver a Activas antes de
        // invocar al subrecurso. La página no debe propagar status inválido.
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        // Sin cargos seed → resultado vacío por defecto.

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos?status=archivo");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cargos asociados a la habilidad", content, StringComparison.OrdinalIgnoreCase);

        // El subrecurso fue invocado con segmento Activas (no Eliminadas, no literal).
        var call = Assert.Single(apiClient.GetCargosCalls);
        Assert.Equal(HabilidadSegmentoListado.Activas, call.Query.Segmento);
    }

    [Fact]
    public async Task Get_CargosPage_StatusEliminadas_PassesEliminadasSegment()
    {
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos?status=eliminadas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cargos eliminados de la habilidad", content, StringComparison.OrdinalIgnoreCase);

        var call = Assert.Single(apiClient.GetCargosCalls);
        Assert.Equal(HabilidadSegmentoListado.Eliminadas, call.Query.Segmento);
    }

    [Fact]
    public async Task Get_CargosPage_PaginationAndSearch_PreservedInSubresourceCall()
    {
        // El PageModel normaliza page/pageSize y propaga search/sort al
        // subrecurso. La página destino debe invocar GetCargosAsync con
        // exactamente los valores normalizados.
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync(
            $"/organizacion/habilidades/{skillId}/cargos?p=2&pageSize=5&search=lid&sort=codigo_asc&status=activas");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var call = Assert.Single(apiClient.GetCargosCalls);
        Assert.Equal(2, call.Query.Page);
        Assert.Equal(5, call.Query.PageSize);
        Assert.Equal("lid", call.Query.Search);
        Assert.Equal("codigo_asc", call.Query.Sort);
        Assert.Equal(HabilidadSegmentoListado.Activas, call.Query.Segmento);
    }

    [Fact]
    public async Task Get_CargosPage_NonAdmin_DoesNotRenderGestionarHabilidadesButton()
    {
        // Gating admin: el CTA "Gestionar habilidades del cargo" sólo debe
        // aparecer cuando el usuario autenticado pertenece al rol
        // RolesSgv.Administrador. La página es accesible para cualquier
        // autenticado, pero un usuario estándar NO debe ver el botón que
        // lo mandaría a un 403.
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");

        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.GetCargosHandler = (_, _) => new PagedResult<SkillCargoDetailDto>(
            new[]
            {
                new SkillCargoDetailDto(cargo, nivel)
                {
                    CargoId = cargoId,
                    NivelRequeridoId = nivel.Id,
                    Ponderacion = 1.00m,
                    EsObligatoria = false,
                    CargoEliminado = false,
                }
            },
            1, 1, 20);

        // El fixture existente autentica SIN rol Administrador por defecto.
        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El botón "Detalle del cargo" (siempre visible) sí aparece.
        Assert.Contains(
            $"href=\"/organizacion/cargos/detalles/{cargoId}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        // El botón admin "Gestionar habilidades del cargo" NO debe aparecer
        // porque el usuario autenticado no tiene rol Administrador.
        Assert.DoesNotContain(
            $"href=\"/organizacion/cargos/{cargoId}/habilidades\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            $"aria-label=\"Gestionar habilidades de {cargo.Nombre}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CargosPage_Admin_RendersGestionarHabilidadesButton()
    {
        // Contrapartida del test anterior: cuando el usuario SÍ es admin,
        // el botón "Gestionar habilidades del cargo" debe aparecer. La
        // página destino es admin-only (Forbid para no-admins), así que el
        // botón no-admin sería un riesgo de UX.
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var nivel = new NivelHabilidadDto(Guid.NewGuid(), "AVZ", "Avanzado", 3, 3);
        var cargoId = Guid.NewGuid();
        var cargo = new CargoDto(cargoId, "C-001", "Director", null, Guid.NewGuid(), "Senior");

        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.GetCargosHandler = (_, _) => new PagedResult<SkillCargoDetailDto>(
            new[]
            {
                new SkillCargoDetailDto(cargo, nivel)
                {
                    CargoId = cargoId,
                    NivelRequeridoId = nivel.Id,
                    Ponderacion = 1.00m,
                    EsObligatoria = false,
                    CargoEliminado = false,
                }
            },
            1, 1, 20);

        // Login con rol Administrador via JWT firmado (re-usa el patrón de
        // CargoWebTestFixture.CreateAuthenticatedClientAsync(..., adminRole: true)).
        // El cliente resultante vive contra la MISMA Program (SGV.Web), así
        // que el cliente es válido para invocar /organizacion/habilidades/.../cargos.
        using var cargoFixture = new CargoWebTestFixture();
        var cargoApiClient = FakeCargoApiClient.WithCargoList();
        using var adminClient = await cargoFixture.CreateAuthenticatedClientAsync(cargoApiClient, apiClient, adminRole: true);

        var response = await adminClient.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            $"href=\"/organizacion/cargos/{cargoId}/habilidades\"",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"aria-label=\"Gestionar habilidades de {cargo.Nombre}\"",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CargosPage_EmptyResult_RendersEmptyState()
    {
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        // GetCargosResult por defecto es empty.

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cargos asociados a la habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No hay cargos asociados", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CargosPage_TransportFailure_RendersRecoverableMessage()
    {
        // 5xx / fallo de transporte en GetByIdAsync debe traducirse a un
        // estado recuperable con mensaje accionable (sin stack trace).
        var skillId = Guid.NewGuid();
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.GetByIdException = new HttpRequestException("network down");

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Intentá nuevamente", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("network down", content, StringComparison.OrdinalIgnoreCase);
    }

    // PR #88 (review 🟠1 / 🟡3): el catch de GetCargosAsync debe
    // traducir fallas de transporte a estado recuperable (paridad con
    // GetByIdAsync). Antes del fix, la vista mostraba el banner de error
    // Y el empty state "no hay cargos" simultáneamente.
    [Fact]
    public async Task Get_CargosPage_GetCargosTransportFailure_RendersRecoverableState()
    {
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.GetCargosException = new HttpRequestException("subresource down");

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Estado recuperable: la grilla NO se renderiza.
        Assert.DoesNotContain("Cargos asociados a la habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Volver al listado", content, StringComparison.OrdinalIgnoreCase);
        // Mensaje accionable sin filtrar el stack trace.
        Assert.Contains("Intentá nuevamente", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpRequestException", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subresource down", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CargosPage_GetCargosJsonException_RendersRecoverableState()
    {
        // Cuerpo de respuesta malformado: el cliente lanza JsonException al
        // deserializar, que entra en IsTransportFailure y debe traducirse a
        // estado recuperable (paridad con HttpRequestException/TaskCanceled).
        var skillId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "H-001", "Liderazgo", "Desc", "Conductual");
        var apiClient = FakeHabilidadApiClient.WithHabilidadList(habilidad);
        apiClient.GetCargosException = new System.Text.Json.JsonException("unexpected token");

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Cargos asociados a la habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Intentá nuevamente", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JsonException", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CargosPage_GetByIdTaskCanceled_RendersRecoverableState()
    {
        // Timeout del subrecurso padre (GetByIdAsync) se traduce al mismo
        // estado recuperable que HttpRequestException.
        var skillId = Guid.NewGuid();
        var apiClient = FakeHabilidadApiClient.WithHabilidadList();
        apiClient.GetByIdException = new TaskCanceledException("timeout");

        using var factory = new SgvWebApplicationFactory();
        using var client = await CreateAuthenticatedClientAsync(factory, apiClient);

        var response = await client.GetAsync($"/organizacion/habilidades/{skillId}/cargos");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Cargos asociados a la habilidad", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Intentá nuevamente", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TaskCanceledException", content, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Login en la factory de SGV.Web con un principal SIN rol (paridad
    /// con el flujo <c>HabilidadWebTestFixture.CreateAuthenticatedClientAsync</c>).
    /// Reusado por todos los tests no-admin; los admin-tests delegan a
    /// <c>CargoWebTestFixture.CreateAuthenticatedClientAsync(..., adminRole: true)</c>.
    /// </summary>
    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        SgvWebApplicationFactory baseFactory,
        FakeHabilidadApiClient apiClient)
    {
        var authHandler = new HabilidadWebTestFixture.RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(
                    new LoginResponse("token-123", DateTimeOffset.UtcNow.AddHours(1))),
            });

        var factory = baseFactory.WithOverrides(
            configureServices: services => services.Configure<SGV.Web.Integration.Auth.SgvApiOptions>(
                options => options.BaseUrl = "https://api.test"),
            authApiHandler: authHandler,
            habilidadApiClient: apiClient);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var signInResponse = await client.GetAsync("/auth/sign-in");
        var antiforgeryToken = await HabilidadWebTestFixture.ExtractAntiforgeryTokenAsync(signInResponse);

        var loginResponse = await client.PostAsync("/auth/sign-in", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Input.UserNameOrEmail"] = "admin",
            ["Input.Password"] = "Password1!",
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }
}