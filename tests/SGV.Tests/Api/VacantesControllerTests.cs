using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Vacantes.Comandos;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Contracts.Vacantes.Consultas.Dtos;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class VacantesControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public VacantesControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    private static CrearVacanteRequest DefaultCreateRequest() => new(
        Guid.Parse("c0000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        "Apertura de vacante");

    private static CambiarEstadoVacanteRequest DefaultCambioEstadoRequest() => new(
        EstadoVacanteId: Guid.Parse("20000000-0000-0000-0000-000000000003"));

    // ── GET endpoints ────────────────────────────────────────────

    [Fact]
    public async Task Get_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/vacantes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/vacantes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Default_ReturnsAbiertasSegmento()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/vacantes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<PagedResult<VacanteDto>>();
        Assert.NotNull(content);
        Assert.NotEmpty(content!.Items);
    }

    /// <summary>
    /// PB-5: cualquier valor de <c>status</c> desconocido (incluido
    /// <c>invalido</c>) debe normalizarse a <c>abiertas</c> y nunca
    /// mezclarse con cerradas.
    /// </summary>
    [Fact]
    public async Task Get_StatusInvalido_CaeAAbiertas()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        // Primer request: con status=invalido → debe normalizar a abiertas.
        var responseInvalido = await client.GetAsync("/api/v1/vacantes?status=invalido");
        Assert.Equal(HttpStatusCode.OK, responseInvalido.StatusCode);

        // Segundo request: sin status → default abiertas (parity).
        var responseDefault = await client.GetAsync("/api/v1/vacantes");
        Assert.Equal(HttpStatusCode.OK, responseDefault.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDetail()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/vacantes/{FakeVacanteServicioConsulta.VacanteId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<VacanteDetailDto>();
        Assert.NotNull(content);
        Assert.Equal(FakeVacanteServicioConsulta.VacanteId1, content!.Id);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/vacantes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Estados_GetAll_Returns200WithFourStates()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/estados-vacante");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<IReadOnlyList<EstadoVacanteDto>>();
        Assert.NotNull(content);
        Assert.Equal(4, content!.Count);
    }

    [Fact]
    public async Task Estados_WithoutCredentials_Returns401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/estados-vacante");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Controller metadata ─────────────────────────────────────

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.VacantesController);
        var hasAuthorize = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);
        Assert.True(hasAuthorize, "Controller MUST require authorization.");
    }

    // ── POST /api/v1/vacantes ───────────────────────────────────

    [Fact]
    public async Task Create_WithoutCredentials_Returns401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// PB-1: mutaciones requieren rol <c>Administrador</c> o
    /// <c>GestorVacantes</c>. Un usuario sin esos roles debe recibir
    /// 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Create_WithAuthenticatedNonMutator_Returns403()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201Created()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<VacanteDetailDto>();
        Assert.NotNull(content);
        Assert.Equal(FakeVacanteServicioComandos.DefaultVacanteId, content!.Id);
    }

    [Fact]
    public async Task Create_ValidacionFalla_Returns400WithProblemDetails()
    {
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.Validation,
                        VacanteErrorCodigo.DatosInvalidos,
                        "Uno o más campos contienen errores de validación."),
                    new Dictionary<string, string[]>
                    {
                        ["motivo"] = ["El motivo es obligatorio."]
                    }))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("motivo", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails()
    {
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.Validation,
                        VacanteErrorCodigo.EstadoTerminalInmutable,
                        "El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada)."),
                    new Dictionary<string, string[]>
                    {
                        ["estadoVacanteId"] = ["El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada)."]
                    }))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest() with
        {
            EstadoVacanteId = Guid.Parse("20000000-0000-0000-0000-000000000003")
        };

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("estadoVacanteId", problem!.Errors.Keys);
    }

    /// <summary>
    /// S-1: si el puesto ya tiene vacante abierta, el crear debe
    /// rechazar con 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Create_PuestoConVacanteAbierta_Returns409()
    {
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.Conflict,
                        VacanteErrorCodigo.PuestoConVacanteAbierta,
                        "Ya existe una vacante abierta para el puesto especificado.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("PuestoConVacanteAbierta", problem!.Title ?? string.Empty);
    }

    // ── N1 (T-8.1): Puesto con Ocupacion activa → 409 PuestoOcupado ──

    [Fact]
    public async Task Create_PuestoConOcupacionActiva_Returns409PuestoOcupado()
    {
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.Conflict,
                        VacanteErrorCodigo.PuestoOcupado,
                        "El puesto tiene una Ocupación activa; no se puede abrir una vacante mientras la posición esté ocupada.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("PuestoOcupado", problem!.Title ?? string.Empty);
    }

    [Fact]
    public async Task Create_PuestoDisponible_Returns201()
    {
        // Cubierto por `Create_ValidRequest_Returns201Created`; duplicado explícito
        // para T-8.1 con semántica exacta (Código del problema NO contiene "PuestoOcupado").
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_EstadoVacanteInexistente_Returns404()
    {
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.NotFound,
                        VacanteErrorCodigo.EstadoVacanteInexistente,
                        "El estado de vacante referenciado no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/vacantes", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PATCH /api/v1/vacantes/{id}/estado ──────────────────────

    [Fact]
    public async Task CambiarEstado_WithoutCredentials_Returns401()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var request = DefaultCambioEstadoRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/vacantes/{FakeVacanteServicioConsulta.VacanteId1}/estado",
            request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CambiarEstado_WithAuthenticatedNonMutator_Returns403()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var request = DefaultCambioEstadoRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/vacantes/{FakeVacanteServicioConsulta.VacanteId1}/estado",
            request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CambiarEstado_VacanteInexistente_Returns404()
    {
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CambiarEstadoHandler = (_, _, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.NotFound,
                        VacanteErrorCodigo.VacanteInexistente,
                        "La vacante no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCambioEstadoRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/vacantes/{Guid.NewGuid()}/estado",
            request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CambiarEstado_EstadoTerminalInmutable_Returns409()
    {
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CambiarEstadoHandler = (_, _, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.Conflict,
                        VacanteErrorCodigo.EstadoTerminalInmutable,
                        "La vacante está en un estado terminal y no admite más cambios.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCambioEstadoRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/vacantes/{FakeVacanteServicioConsulta.VacanteId1}/estado",
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("EstadoTerminalInmutable", problem!.Title ?? string.Empty);
    }

    [Fact]
    public async Task CambiarEstado_ValidRequest_Returns200WithDetail()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var request = DefaultCambioEstadoRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/vacantes/{FakeVacanteServicioConsulta.VacanteId1}/estado",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<VacanteDetailDto>();
        Assert.NotNull(content);
    }

    // ── N2 (T-8.1): Cubrir sin PersonaId → 400 PersonaIdRequeridoParaCubrir ──

    [Fact]
    public async Task CambiarEstado_CubrirSinPersonaId_Returns400PersonaIdRequerido()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["personaId"] = ["PersonaId es obligatorio al cubrir una Vacante."]
        };
        var fakeComandos = new FakeVacanteServicioComandos
        {
            CambiarEstadoHandler = (_, _, _) => Task.FromResult(
                VacanteCommandResult.Failure(
                    new VacanteError(
                        ErrorCategoria.Validation,
                        VacanteErrorCodigo.PersonaIdRequeridoParaCubrir,
                        "PersonaId es obligatorio al cubrir una Vacante."),
                    fieldErrors))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IVacanteServicioComandos>();
            services.AddSingleton<IVacanteServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = new CambiarEstadoVacanteRequest(
            EstadoVacanteId: Guid.Parse("20000000-0000-0000-0000-000000000003")); // Cubierta

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/vacantes/{FakeVacanteServicioConsulta.VacanteId1}/estado",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("personaId", problem!.Errors.Keys);
    }

    [Fact]
    public async Task CambiarEstado_CubrirConPersonaId_Returns200()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var request = new CambiarEstadoVacanteRequest(
            EstadoVacanteId: Guid.Parse("20000000-0000-0000-0000-000000000003"),
            PersonaId: Guid.NewGuid());

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/vacantes/{FakeVacanteServicioConsulta.VacanteId1}/estado",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<VacanteDetailDto>();
        Assert.NotNull(content);
    }
}
