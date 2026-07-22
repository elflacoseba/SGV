using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Infraestructura.Persistencia.Catalogos;
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
        TipoDocumentoId: Guid.NewGuid(),
        NumeroDocumento: "12345678",
        Telefono: "555-0001");

    private static ActualizarPersonaRequest DefaultUpdateRequest(string legajo) => new(
        Legajo: legajo,
        Nombres: "Juan Actualizado",
        Apellidos: "Perez",
        Email: "juan@test.com",
        TipoDocumentoId: Guid.NewGuid(),
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
    public async Task GetAll_DtoExponeTipoDocumentoCodigoYNombreDenormalizados()
    {
        // PR2: el PersonaDto retornado por la API DEBE exponer TipoDocumentoId,
        // TipoDocumentoCodigo y TipoDocumentoNombre (no la string legacy
        // TipoDocumento). Si el JOIN no se hubiera implementado, estos campos
        // quedarían null.
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas");
        var dto = (await ReadAsAsync<List<PersonaDto>>(response))![0];

        Assert.NotNull(dto.TipoDocumentoId);
        Assert.Equal(TipoDocumentoConstantes.DniId, dto.TipoDocumentoId!.Value);
        Assert.Equal("DNI", dto.TipoDocumentoCodigo);
        Assert.Equal("Documento Nacional de Identidad", dto.TipoDocumentoNombre);
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

    // ---- GET /api/v1/personas/consulta ----

    [Fact]
    public async Task GetConsulta_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/personas/consulta?status=eliminadas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConsulta_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        // El endpoint debe permitir acceso a cualquier autenticado (sin
        // requerir rol Administrador), igual que GET /api/v1/personas plano.
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/personas/consulta?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PersonaListadoDto>(response);
        Assert.NotNull(page);
        Assert.NotEmpty(page!.Items);
    }

    [Fact]
    public async Task GetConsulta_StatusInvalido_CaeA_Activas()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas/consulta?status=archivo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PersonaListadoDto>(response);
        Assert.NotNull(page);
        Assert.Single(page!.Items);
        Assert.Equal(FakePersonaServicioConsulta.PersonaId1, page.Items[0].Id);
    }

    [Fact]
    public async Task GetConsulta_SinStatus_RetornaActivas()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas/consulta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PersonaListadoDto>(response);
        Assert.NotNull(page);
        Assert.Single(page!.Items);
        Assert.Equal(FakePersonaServicioConsulta.PersonaId1, page.Items[0].Id);
    }

    [Fact]
    public async Task GetConsulta_PropagaSortYPageAlServicio()
    {
        // El controller DEBE pasar el `sort`, `page`, `pageSize` y `status`
        // al servicio. Si los filtra o descarta, el fake los capturaría como
        // null/default y este test fallaría.
        var capture = new SortCapturingFakePersonaServicio();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            "/api/v1/personas/consulta?sort=apellidos_desc&page=2&pageSize=5&status=activas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedQueries);
        Assert.Equal("apellidos_desc", observed.Sort);
        Assert.Equal(2, observed.Page);
        Assert.Equal(5, observed.PageSize);
        Assert.Equal(PersonaSegmentoListado.Activas, observed.Segmento);
    }

    [Fact]
    public async Task GetConsulta_StatusEliminadas_NoRetornaActivas()
    {
        // Cuando el caller pide status=eliminadas, la respuesta debe usar el
        // fake con sólo personas eliminadas. Si el controller filtrara por
        // defecto o mezclara segmentos, este test detectaría la regresión.
        var capture = new SortCapturingFakePersonaServicio();
        var eliminadaId = Guid.NewGuid();
        capture.Eliminadas =
        [
            new PersonaDto(eliminadaId, "LEG-DEL", "Ana", "García", "ana@test.com", null, null, "DNI", "123", "555", true)
        ];
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas/consulta?status=eliminadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PersonaListadoDto>(response);
        Assert.NotNull(page);
        var item = Assert.Single(page!.Items);
        Assert.Equal(eliminadaId, item.Id);
        Assert.NotEqual(FakePersonaServicioConsulta.PersonaId1, item.Id);
    }

    // ---- GET /consulta soloSinUsuario (WU-3, REQ-PM-01) ----

    /// <summary>
    /// REQ-PM-01: <c>?soloSinUsuario=true</c> en el query string DEBE
    /// propagarse al servicio como <c>PersonaListQuery.SoloSinUsuario=true</c>.
    /// Si el controller lo ignorara o lo filtrara, el repo no podría
    /// aplicar el anti-join sobre AspNetUsers y el buscador modal quedaría
    /// sin efecto.
    /// </summary>
    [Fact]
    public async Task GetConsulta_ConSoloSinUsuarioTrue_PropagaAlServicio()
    {
        var capture = new SortCapturingFakePersonaServicio();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            "/api/v1/personas/consulta?soloSinUsuario=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedQueries);
        Assert.Equal(true, observed.SoloSinUsuario);
    }

    /// <summary>
    /// REQ-PM-01: <c>?soloSinUsuario=true&amp;status=eliminadas</c> DEBE
    /// propagar el flag como <c>true</c> al servicio y mantener
    /// <c>Segmento=Eliminadas</c>; el cortocircuito del repo (items=[],
    /// totalCount=0) lo aplica el repositorio, no el controller.
    /// </summary>
    [Fact]
    public async Task GetConsulta_ConSoloSinUsuarioTrueYEliminadas_PropagaAmbosFlags()
    {
        var capture = new SortCapturingFakePersonaServicio();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            "/api/v1/personas/consulta?soloSinUsuario=true&status=eliminadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedQueries);
        Assert.Equal(true, observed.SoloSinUsuario);
        Assert.Equal(PersonaSegmentoListado.Eliminadas, observed.Segmento);
    }

    /// <summary>
    /// REQ-PM-01: <c>soloSinUsuario</c> ausente en el query string DEBE
    /// preservar el comportamiento vigente (back-compat): el binding de
    /// ASP.NET en ausencia del parámetro deja el valor nullable en
    /// <c>null</c> y el repo no aplica filtro.
    /// </summary>
    [Fact]
    public async Task GetConsulta_SoloSinUsuarioAusente_PropagaNull()
    {
        var capture = new SortCapturingFakePersonaServicio();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/personas/consulta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedQueries);
        Assert.Null(observed.SoloSinUsuario);
    }

    /// <summary>
    /// REQ-PM-01: <c>soloSinUsuario=false</c> explícito DEBE propagarse al
    /// servicio como <c>bool = false</c> (no se normaliza). El repo trata
    /// <c>null</c> y <c>false</c> de forma idéntica (ambos desactivan el
    /// filtro) gracias a <c>if (soloSinUsuario == true)</c>, así que la
    /// semántica observable para el cliente es la misma que ausente.
    /// </summary>
    [Fact]
    public async Task GetConsulta_SoloSinUsuarioFalse_PropagaFalse()
    {
        var capture = new SortCapturingFakePersonaServicio();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            "/api/v1/personas/consulta?soloSinUsuario=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedQueries);
        Assert.Equal(false, observed.SoloSinUsuario);
    }

    /// <summary>
    /// REQ-PM-01: la composición completa — <c>soloSinUsuario</c> +
    /// <c>search</c> + <c>sort</c> + <c>page</c>/<c>pageSize</c> + segmento
    /// — DEBE propagarse íntegra al servicio sin reasignar ni descartar.
    /// </summary>
    [Fact]
    public async Task GetConsulta_SoloSinUsuarioCombinaConSearchSortYPage_PropagaTodo()
    {
        var capture = new SortCapturingFakePersonaServicio();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            "/api/v1/personas/consulta?soloSinUsuario=true&search=garcia&sort=apellidos_desc&page=2&pageSize=5&status=activas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedQueries);
        Assert.Equal(true, observed.SoloSinUsuario);
        Assert.Equal("garcia", observed.Search);
        Assert.Equal("apellidos_desc", observed.Sort);
        Assert.Equal(2, observed.Page);
        Assert.Equal(5, observed.PageSize);
        Assert.Equal(PersonaSegmentoListado.Activas, observed.Segmento);
    }

    /// <summary>
    /// Fake en memoria de <see cref="IPersonaServicioConsulta"/> que captura la
    /// última query recibida y devuelve datos controlados por segmento. Usado
    /// para verificar que el controller propaga search/sort/page/status sin
    /// filtrar ni reordenar.
    /// </summary>
    private sealed class SortCapturingFakePersonaServicio : IPersonaServicioConsulta
    {
        public List<PersonaListQuery> CapturedQueries { get; } = new();
        public IReadOnlyList<PersonaDto> Eliminadas { get; set; } = [];

        public Task<IReadOnlyList<PersonaDto>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PersonaDto>>([]);

        public Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<PersonaDto?>(null);

        public Task<PersonaListadoDto> ListarAsync(
            PersonaListQuery query,
            CancellationToken cancellationToken = default)
        {
            CapturedQueries.Add(query);
            var source = query.Segmento == PersonaSegmentoListado.Eliminadas
                ? Eliminadas
                : new[] { new PersonaDto(FakePersonaServicioConsulta.PersonaId1, "LEG-001", "Juan", "Perez", "juan@test.com", null, null, "DNI", "12345678", "555-0001", true) };
            return Task.FromResult(new PersonaListadoDto(
                source.ToList(), source.Count, query.Page, query.PageSize));
        }
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
