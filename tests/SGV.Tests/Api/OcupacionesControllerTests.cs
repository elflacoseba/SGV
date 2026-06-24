using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;
using SGV.Dominio.Ocupaciones;
using Xunit;

namespace SGV.Tests.Api;

public sealed class OcupacionesControllerTests
    : IClassFixture<WebApplicationFactory<SGV.Api.Program>>
{
    private readonly WebApplicationFactory<SGV.Api.Program> _factory;

    public OcupacionesControllerTests(WebApplicationFactory<SGV.Api.Program> factory)
    {
        _factory = factory;
    }

    // ── GET /api/v1/ocupaciones ─────────────────────────────────

    [Fact]
    public async Task GetAll_Default_ReturnsOkWithActiveOccupations()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ocupaciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto[]>();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.All(content, o => Assert.Equal("Activo", o.Estado));
    }

    [Fact]
    public async Task GetAll_IncludeHistory_ReturnsAllIncludingFinalized()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ocupaciones?includeHistory=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto[]>();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    // ── GET /api/v1/ocupaciones/{id} ───────────────────────────

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
        Assert.Equal(FakeOcupacionServicioConsulta.OcupacionId1, content!.Id);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/ocupaciones/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST /api/v1/ocupaciones ────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201Created()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new CrearOcupacionRequest(
            FakeOcupacionServicioConsulta.PersonaId1,
            FakeOcupacionServicioConsulta.PuestoId1,
            new DateOnly(2024, 6, 1),
            TipoAsignacion.Permanente);

        var response = await client.PostAsJsonAsync("/api/v1/ocupaciones", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
        Assert.Equal(FakeOcupacionServicioConsulta.OcupacionId1, content!.Id);
    }

    [Fact]
    public async Task Create_Conflict_Returns409WithProblemDetails()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                CrearHandler = (_, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.Conflict, "PuestoOcupado",
                            "Ya existe una ocupación activa para el puesto especificado.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var request = new CrearOcupacionRequest(
            FakeOcupacionServicioConsulta.PersonaId1,
            FakeOcupacionServicioConsulta.PuestoId1,
            new DateOnly(2024, 6, 1),
            TipoAsignacion.Permanente);

        var response = await client.PostAsJsonAsync("/api/v1/ocupaciones", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("PuestoOcupado", problem!.Title ?? string.Empty);
    }

    // ── PUT /api/v1/ocupaciones/{id} ────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_Returns200Ok()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new ActualizarOcupacionRequest(
            FakeOcupacionServicioConsulta.PersonaId1,
            FakeOcupacionServicioConsulta.PuestoId1,
            new DateOnly(2024, 6, 15),
            TipoAsignacion.Interina);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                ActualizarHandler = (_, _, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                            "La ocupación no existe.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var request = new ActualizarOcupacionRequest(
            FakeOcupacionServicioConsulta.PersonaId1,
            FakeOcupacionServicioConsulta.PuestoId1,
            new DateOnly(2024, 6, 15),
            TipoAsignacion.Interina);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_Finalized_Returns409()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                ActualizarHandler = (_, _, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.Conflict, "OcupacionNoEditable",
                            "La ocupación no está activa y no se puede modificar.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var request = new ActualizarOcupacionRequest(
            FakeOcupacionServicioConsulta.PersonaId1,
            FakeOcupacionServicioConsulta.PuestoId1,
            new DateOnly(2024, 6, 15),
            TipoAsignacion.Interina);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── PATCH /api/v1/ocupaciones/{id}/finalizar ────────────────

    [Fact]
    public async Task Finalize_ValidRequest_Returns200Ok()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new FinalizarOcupacionRequest(new DateOnly(2024, 12, 31));

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/finalizar", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
        Assert.Equal("Finalizado", content!.Estado);
    }

    [Fact]
    public async Task Finalize_NonExistent_Returns404()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                FinalizarHandler = (_, _, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                            "La ocupación no existe.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var request = new FinalizarOcupacionRequest(new DateOnly(2024, 12, 31));

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/ocupaciones/{Guid.NewGuid()}/finalizar", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Finalize_AlreadyFinalized_Returns409()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                FinalizarHandler = (_, _, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.Conflict, "OcupacionNoEditable",
                            "La ocupación no está activa y no se puede finalizar.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var request = new FinalizarOcupacionRequest(new DateOnly(2024, 12, 31));

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/finalizar", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── PATCH /api/v1/ocupaciones/{id}/reactivar ───────────────

    [Fact]
    public async Task Reactivate_ValidRequest_Returns200Ok()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/reactivar",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
        Assert.Equal("Activo", content!.Estado);
    }

    [Fact]
    public async Task Reactivate_NonExistent_Returns404()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                ReactivarHandler = (_, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                            "La ocupación no existe.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/ocupaciones/{Guid.NewGuid()}/reactivar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_Conflict_Returns409()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                ReactivarHandler = (_, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.Conflict, "PuestoOcupado",
                            "Ya existe una ocupación activa para el puesto especificado.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── DELETE /api/v1/ocupaciones/{id} ─────────────────────────

    [Fact]
    public async Task Delete_ExistingId_Returns204NoContent()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            var fakeComandos = new FakeOcupacionServicioComandos
            {
                EliminarHandler = (_, _) => Task.FromResult(
                    OcupacionCommandResult.Failure(
                        new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                            "La ocupación no existe.")))
            };
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/ocupaciones/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
