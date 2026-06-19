using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class SkillsControllerTests
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

    // ---- GET (existing) ----

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<HabilidadDto>>(response);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(FakeHabilidadServicio.HabilidadId1, dtos[0].Id);
        Assert.Equal("PROG", dtos[0].Codigo);
    }

    [Fact]
    public async Task GetAll_WhenNoData_ReturnsOkWithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioConsulta>();
            services.AddSingleton<IHabilidadServicioConsulta>(
                new FakeHabilidadServicio(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<HabilidadDto>>(response);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<HabilidadDto>(response);
        Assert.NotNull(dto);
        Assert.Equal(FakeHabilidadServicio.HabilidadId1, dto.Id);
        Assert.Equal("Programación", dto.Nombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/skills/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.SkillsController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }

    // ---- POST (create) ----

    [Fact]
    public async Task Post_ValidRequest_Returns201CreatedWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "NVO", nombre = "Nueva Habilidad", categoria = "Técnica" });

        var response = await client.PostAsync("/api/v1/skills", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await ReadAsAsync<HabilidadDto>(response);
        Assert.Equal("NVO", dto.Codigo);
        Assert.Equal("Nueva Habilidad", dto.Nombre);
        Assert.NotEqual(Guid.Empty, dto.Id);
    }

    [Fact]
    public async Task Post_ValidationError_Returns400WithFieldErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = ["'Codigo' no debe estar vacío."],
            ["nombre"] = ["'Nombre' no debe estar vacío."]
        };
        var fakeComandos = new FakeHabilidadServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioComandos>();
            services.AddSingleton<IHabilidadServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "", nombre = "" });

        var response = await client.PostAsync("/api/v1/skills", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "codigo");
        await AssertErrorFieldExists(response, "nombre");
    }

    [Fact]
    public async Task Post_DuplicateCode_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeHabilidadServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.Conflict, "CodigoDuplicado", "Ya existe una habilidad activa con el mismo código.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioComandos>();
            services.AddSingleton<IHabilidadServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "PROG", nombre = "Duplicado" });

        var response = await client.PostAsync("/api/v1/skills", body);

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
        var body = ToJsonBody(new { nombre = "Habilidad Actualizada", categoria = "Nueva Categoría" });

        var response = await client.PutAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<HabilidadDto>(response);
        Assert.Equal("Habilidad Actualizada", dto.Nombre);
    }

    [Fact]
    public async Task Put_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeHabilidadServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioComandos>();
            services.AddSingleton<IHabilidadServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nombre = "No existe" });

        var response = await client.PutAsync($"/api/v1/skills/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task Put_ValidationError_Returns400WithFieldErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["nombre"] = ["'Nombre' no debe estar vacío."]
        };
        var fakeComandos = new FakeHabilidadServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioComandos>();
            services.AddSingleton<IHabilidadServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nombre = "" });

        var response = await client.PutAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "nombre");
    }

    // ---- DELETE (soft-delete) ----

    [Fact]
    public async Task Delete_ExistingId_Returns204NoContent()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeHabilidadServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(
                HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioComandos>();
            services.AddSingleton<IHabilidadServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/skills/{Guid.NewGuid()}");

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
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<HabilidadDto>(response);
        Assert.Equal(FakeHabilidadServicio.HabilidadId1, dto.Id);
    }

    [Fact]
    public async Task PatchReactivar_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeHabilidadServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.NotFound, "HabilidadNoEncontrada", "La habilidad no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioComandos>();
            services.AddSingleton<IHabilidadServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/skills/{Guid.NewGuid()}/reactivar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task PatchReactivar_Conflict_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeHabilidadServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                HabilidadCommandResult.Failure(
                    new HabilidadError(HabilidadErrorType.Conflict, "CodigoDuplicado",
                        "Ya existe una habilidad activa con el mismo código.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IHabilidadServicioComandos>();
            services.AddSingleton<IHabilidadServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }
}
