using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Ocupaciones.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Ocupaciones;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class OcupacionesControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public OcupacionesControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static CrearOcupacionRequest DefaultCreateRequest() => new(
        FakeOcupacionServicioConsulta.PersonaId1,
        FakeOcupacionServicioConsulta.PuestoId1,
        new DateOnly(2024, 6, 1),
        TipoAsignacion.Permanente);

    private static ActualizarOcupacionRequest DefaultUpdateRequest() => new(
        FakeOcupacionServicioConsulta.PersonaId1,
        FakeOcupacionServicioConsulta.PuestoId1,
        new DateOnly(2024, 6, 15),
        TipoAsignacion.Interina);

    private static FinalizarOcupacionRequest DefaultFinalizeRequest() => new(new DateOnly(2024, 12, 31));

    // ---- GET endpoints ----

    [Fact]
    public async Task GetAll_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ocupaciones");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/ocupaciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Default_ReturnsOkWithActiveOccupations()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/ocupaciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<PagedResult<OcupacionDto>>();
        Assert.NotNull(content);
        Assert.NotEmpty(content!.Items);
        Assert.All(content.Items, o => Assert.Equal("Activo", o.Estado));
    }

    [Fact]
    public async Task GetAll_IncludeHistory_ReturnsAllIncludingFinalized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/ocupaciones?includeHistory=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<PagedResult<OcupacionDto>>();
        Assert.NotNull(content);
        Assert.NotEmpty(content!.Items);
    }

    [Fact]
    public async Task GetById_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
        Assert.Equal(FakeOcupacionServicioConsulta.OcupacionId1, content!.Id);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/ocupaciones/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Controller metadata ----

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.OcupacionesController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.True(hasAuthorize, "Controller MUST require authorization");
    }

    // ---- POST /api/v1/ocupaciones ----

    [Fact]
    public async Task Create_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/ocupaciones", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/ocupaciones", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201Created()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/ocupaciones", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
        Assert.Equal(FakeOcupacionServicioConsulta.OcupacionId1, content!.Id);
    }

    [Fact]
    public async Task Create_Conflict_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.Conflict, "PuestoOcupado",
                        "Ya existe una ocupación activa para el puesto especificado.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultCreateRequest();

        var response = await client.PostAsJsonAsync("/api/v1/ocupaciones", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("PuestoOcupado", problem!.Title ?? string.Empty);
    }

    // ---- PUT /api/v1/ocupaciones/{id} ----

    [Fact]
    public async Task Update_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var request = DefaultUpdateRequest();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var request = DefaultUpdateRequest();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidRequest_Returns200Ok()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var request = DefaultUpdateRequest();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<OcupacionDto>();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            ActualizarHandler = (_, _, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                        "La ocupación no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultUpdateRequest();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_Finalized_Returns409()
    {
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            ActualizarHandler = (_, _, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.Conflict, "OcupacionNoEditable",
                        "La ocupación no está activa y no se puede modificar.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultUpdateRequest();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- PATCH /api/v1/ocupaciones/{id}/finalizar ----

    [Fact]
    public async Task Finalize_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var request = DefaultFinalizeRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/finalizar", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Finalize_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var request = DefaultFinalizeRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/finalizar", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Finalize_ValidRequest_Returns200Ok()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var request = DefaultFinalizeRequest();

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
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            FinalizarHandler = (_, _, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                        "La ocupación no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultFinalizeRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/ocupaciones/{Guid.NewGuid()}/finalizar", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Finalize_AlreadyFinalized_Returns409()
    {
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            FinalizarHandler = (_, _, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.Conflict, "OcupacionNoEditable",
                        "La ocupación no está activa y no se puede finalizar.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var request = DefaultFinalizeRequest();

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/finalizar", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- PATCH /api/v1/ocupaciones/{id}/reactivar ----

    [Fact]
    public async Task Reactivate_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/reactivar",
            null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/reactivar",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_ValidRequest_Returns200Ok()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                        "La ocupación no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/ocupaciones/{Guid.NewGuid()}/reactivar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_Conflict_Returns409()
    {
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.Conflict, "PuestoOcupado",
                        "Ya existe una ocupación activa para el puesto especificado.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- DELETE /api/v1/ocupaciones/{id} ----

    [Fact]
    public async Task Delete_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204NoContent()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/ocupaciones/{FakeOcupacionServicioConsulta.OcupacionId1}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var fakeComandos = new FakeOcupacionServicioComandos
        {
            EliminarHandler = (_, _) => Task.FromResult(
                OcupacionCommandResult.Failure(
                    new(OcupacionErrorType.NotFound, "OcupacionNoEncontrada",
                        "La ocupación no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IOcupacionServicioComandos>();
            services.AddSingleton<IOcupacionServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync($"/api/v1/ocupaciones/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
