using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class PersonasControllerTests
{
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

    // ---- GET (list) ----

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioConsulta>();
            services.AddSingleton<IPersonaServicioConsulta>(
                new FakePersonaServicioConsulta(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/personas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<PersonaDto>>(response);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    // ---- GET (by id) ----

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/personas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Controller metadata ----

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.PersonasController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }

    // ---- JSON contract: no relationships ----

    [Fact]
    public async Task GetAll_JsonResponse_MustNotContainExcludedRelationships()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/personas");
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("postulantes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ocupaciones", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("habilidades", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("personaHabilidad", json, StringComparison.OrdinalIgnoreCase);
    }

    // ---- POST (create) ----

    [Fact]
    public async Task Post_ValidRequest_Returns201CreatedWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var body = ToJsonBody(new { legajo = "LEG-NVO", nombres = "Maria", apellidos = "Garcia" });

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { legajo = "", nombres = "", apellidos = "" });

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { legajo = "LEG-001", nombres = "Duplicate", apellidos = "Test" });

        var response = await client.PostAsync("/api/v1/personas", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }

    // ---- PUT (update) ----

    [Fact]
    public async Task Put_ValidRequest_Returns200OkWithUpdatedDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var body = ToJsonBody(new { legajo = "LEG-001", nombres = "Juan Actualizado", apellidos = "Perez" });

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { legajo = "LEG-X", nombres = "No", apellidos = "Existe" });

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { legajo = "LEG-001", nombres = "", apellidos = "Perez" });

        var response = await client.PutAsync($"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "nombres");
    }

    // ---- DELETE (soft-delete) ----

    [Fact]
    public async Task Delete_ExistingId_Returns204NoContent()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/personas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    // ---- PATCH (reactivar) ----

    [Fact]
    public async Task PatchReactivar_ValidRequest_Returns200OkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaServicioComandos>();
            services.AddSingleton<IPersonaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/personas/{FakePersonaServicioConsulta.PersonaId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }
}
