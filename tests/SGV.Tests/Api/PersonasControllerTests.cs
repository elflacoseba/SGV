using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class PersonasControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public PersonasControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // ---- Helpers ----

    private static StringContent ToJsonBody(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static async Task<T> ReadAsAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
    }

    private static async Task<ProblemDetails> ReadProblemDetailsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var basic = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new ProblemDetails
        {
            Status = basic.GetValueOrDefault("status", default).GetInt32(),
            Title = basic.GetValueOrDefault("title", default).GetString() ?? "",
            Detail = basic.GetValueOrDefault("detail", default).GetString() ?? "",
            Type = basic.GetValueOrDefault("type", default).GetString() ?? ""
        };
    }

    private static async Task<Dictionary<string, JsonElement>> ReadErrorsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return body.GetValueOrDefault("errors", default).Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
    }

    private static async Task AssertErrorFieldExists(HttpResponseMessage response, string fieldName)
    {
        var errors = await ReadErrorsAsync(response);
        Assert.True(errors.ContainsKey(fieldName), $"Expected field '{fieldName}' in errors");
    }

    private static CrearPersonaRequest DefaultCreateRequest() => new(
        Legajo: "LEG-NVO",
        Nombres: "Maria",
        Apellidos: "Garcia",
        Email: "maria@test.com",
        TipoDocumento: "DNI",
        NumeroDocumento: "12345678",
        Telefono: "555-0001");

    private static ActualizarPersonaRequest DefaultUpdateRequest(string legajo) => new(
        Legajo: legajo,
        Nombres: "Juan Actualizado",
        Apellidos: "Perez",
        Email: "juan@test.com",
        TipoDocumento: "DNI",
        NumeroDocumento: "12345678",
        Telefono: "555-0001");

    private static AsignarPersonaSkillRequest DefaultSkillRequest() => new(
        NivelId: Guid.NewGuid());

    // ---- GET (list) ----

    [Fact]
    public async Task GetAll_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/personas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/personas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<PersonaDto>>(response);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(FakePersonaServicioConsulta.PersonaId1, dtos[0].Id);
        Assert.Equal("LEG-001", dtos[0].Legajo);
    }

    [Fact]
    public async Task GetAll_WhenNoData_ReturnsOkWithEmptyArray()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(
                new FakePersonaServicioConsulta(isEmpty: true));
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<PersonaDto>>(response);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    // ---- GET (by id) ----

    [Fact]
    public async Task GetById_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<PersonaDto>(response);
        Assert.NotNull(dto);
        Assert.Equal(FakePersonaServicioConsulta.PersonaId1, dto.Id);
        Assert.Equal("Juan", dto.Nombres);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/personas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Controller metadata ----

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.PersonasController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.True(hasAuthorize, "Controller MUST require authorization");
    }

    // ---- JSON contract: no relationships ----

    [Fact]
    public async Task GetAll_JsonResponse_MustNotContainExcludedRelationships()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas");
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("postulantes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ocupaciones", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("habilidades", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("personaHabilidad", json, StringComparison.OrdinalIgnoreCase);
    }

    // ---- POST (create) ----

    [Fact]
    public async Task Post_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var body = ToJsonBody(DefaultCreateRequest());

        var response = await client.PostAsync("/api/v1/personas", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var body = ToJsonBody(DefaultCreateRequest());

        var response = await client.PostAsync("/api/v1/personas", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidRequest_Returns201CreatedWithDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(DefaultCreateRequest());

        var response = await client.PostAsync("/api/v1/personas", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await ReadAsAsync<PersonaDto>(response);
        Assert.Equal("LEG-NVO", dto.Legajo);
        Assert.Equal("Maria", dto.Nombres);
        Assert.NotEqual(Guid.Empty, dto.Id);
    }

    [Fact]
    public async Task Post_ValidationError_Returns400WithFieldErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["legajo"] = ["'Legajo' no debe estar vacío."],
            ["nombres"] = ["'Nombres' no debe estar vacío."]
        };
        var fakeComandos = new FakePersonaServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                PersonaCommandResult.Failure(
                    new PersonaError(PersonaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new
        {
            legajo = "",
            nombres = "",
            apellidos = "",
            email = "",
            tipoDocumento = "",
            numeroDocumento = "",
            telefono = ""
        });

        var response = await client.PostAsync("/api/v1/personas", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "legajo");
        await AssertErrorFieldExists(response, "nombres");
    }

    [Fact]
    public async Task Post_DuplicateLegajo_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakePersonaServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                PersonaCommandResult.Failure(
                    new PersonaError(PersonaErrorType.Conflict, "LegajoDuplicado", "Ya existe una persona activa con el mismo legajo.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(DefaultCreateRequest() with { Legajo = "LEG-001" });

        var response = await client.PostAsync("/api/v1/personas", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }

    // ---- PUT (update) ----

    [Fact]
    public async Task Put_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var body = ToJsonBody(DefaultUpdateRequest("LEG-001"));

        var response = await client.PutAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var body = ToJsonBody(DefaultUpdateRequest("LEG-001"));

        var response = await client.PutAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_ValidRequest_Returns200OkWithUpdatedDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(DefaultUpdateRequest("LEG-001"));

        var response = await client.PutAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<PersonaDto>(response);
        Assert.Equal("Juan Actualizado", dto.Nombres);
    }

    [Fact]
    public async Task Put_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakePersonaServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                PersonaCommandResult.Failure(
                    new PersonaError(PersonaErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(DefaultUpdateRequest("LEG-X"));

        var response = await client.PutAsync($"/api/v1/personas/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task Put_ValidationError_Returns400WithFieldErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["nombres"] = ["'Nombres' no debe estar vacío."]
        };
        var fakeComandos = new FakePersonaServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                PersonaCommandResult.Failure(
                    new PersonaError(PersonaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(DefaultUpdateRequest("LEG-001") with { Nombres = "" });

        var response = await client.PutAsync($"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "nombres");
    }

    // ---- DELETE (soft-delete) ----

    [Fact]
    public async Task Delete_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204NoContent()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakePersonaServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(
                PersonaCommandResult.Failure(
                    new PersonaError(PersonaErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync($"/api/v1/personas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    // ---- PATCH (reactivar) ----

    [Fact]
    public async Task PatchReactivar_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchReactivar_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchReactivar_ValidRequest_Returns200OkWithDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<PersonaDto>(response);
        Assert.Equal(FakePersonaServicioConsulta.PersonaId1, dto.Id);
    }

    [Fact]
    public async Task PatchReactivar_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakePersonaServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                PersonaCommandResult.Failure(
                    new PersonaError(PersonaErrorType.NotFound, "PersonaNoEncontrada", "La persona no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/personas/{Guid.NewGuid()}/reactivar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task PatchReactivar_Conflict_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakePersonaServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                PersonaCommandResult.Failure(
                    new PersonaError(PersonaErrorType.Conflict, "LegajoDuplicado",
                        "Ya existe una persona activa con el mismo legajo.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }

    // ---- PUT /skills/{skillId} (UpsertSkill) ----

    [Fact]
    public async Task UpsertSkill_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        var body = ToJsonBody(DefaultSkillRequest());

        var response = await client.PutAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/skills/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSkill_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();
        var body = ToJsonBody(DefaultSkillRequest());

        var response = await client.PutAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/skills/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSkill_WithAdmin_Returns200Ok()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, PersonaSkillTestsFake>();
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(DefaultSkillRequest());

        var response = await client.PutAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/skills/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- DELETE /skills/{skillId} (DeleteSkill) ----

    [Fact]
    public async Task DeleteSkill_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_WithAdmin_Returns204NoContent()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, PersonaSkillTestsFake>();
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Fake en memoria de <see cref="IPersonaSkillServicio"/> que devuelve éxito
    /// para todo Upsert/Delete de skill. Reusado aquí y en
    /// PersonaSkillControllerTests — vive en este archivo porque
    /// ApiWebApplicationFactory no expone un fake global para este servicio.
    /// </summary>
    private sealed class PersonaSkillTestsFake : IPersonaSkillServicio
    {
        public Task<IReadOnlyList<PersonaSkillDetailDto>> ListAsync(Guid personaId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PersonaSkillDetailDto>>([]);

        public Task<PersonaSkillCommandResult> UpsertAsync(Guid personaId, Guid skillId, AsignarPersonaSkillRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(PersonaSkillCommandResult.Success(new PersonaSkillDto(skillId, request.NivelId)));

        public Task<PersonaSkillCommandResult> DeleteAsync(Guid personaId, Guid skillId, CancellationToken cancellationToken = default)
            => Task.FromResult(PersonaSkillCommandResult.Success(new PersonaSkillDto(skillId, Guid.Empty)));
    }
}
