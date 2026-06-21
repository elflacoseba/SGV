using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class CargosControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cargos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<CargoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(FakeCargoServicio.CargoId1, dtos[0].Id);
        Assert.Equal("DIRECTOR", dtos[0].Codigo);
    }

    [Fact]
    public async Task GetAll_WhenNoData_ReturnsOkWithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioConsulta>();
            services.AddSingleton<ICargoServicioConsulta>(
                new FakeCargoServicio(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cargos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<CargoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<CargoDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakeCargoServicio.CargoId1, dto.Id);
        Assert.Equal("Director", dto.Nombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/cargos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ParentPayloadDoesNotIncludeSkillAssignmentFields()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("skillId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("habilidades", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.CargosController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }

    // ---- JSON contract (nivelId / nivelNombre) ----

    [Fact]
    public async Task GetAll_JsonResponseContieneNivelIdYNivelNombre()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cargos");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var first = doc.RootElement.EnumerateArray().First();
        Assert.True(first.TryGetProperty("nivelId", out _),
            "Response JSON MUST include 'nivelId'");
        Assert.True(first.TryGetProperty("nivelNombre", out _),
            "Response JSON MUST include 'nivelNombre'");
        Assert.False(first.TryGetProperty("nivel", out _),
            "Response JSON MUST NOT include legacy 'nivel'");
    }

    // ---- POST (create) ----

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

    [Fact]
    public async Task Post_ValidRequest_Returns201CreatedWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "NVO", nombre = "Nuevo Cargo", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PostAsync("/api/v1/cargos", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await ReadAsAsync<CargoDto>(response);
        Assert.Equal("NVO", dto.Codigo);
        Assert.Equal("Nuevo Cargo", dto.Nombre);
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
        var fakeComandos = new FakeCargoServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "", nombre = "", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PostAsync("/api/v1/cargos", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "codigo");
        await AssertErrorFieldExists(response, "nombre");
    }

    [Fact]
    public async Task Post_DuplicateCode_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeCargoServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Conflict, "CodigoDuplicado", "Ya existe un cargo activo con el mismo código.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "DIRECTOR", nombre = "Duplicado", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PostAsync("/api/v1/cargos", body);

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
        var body = ToJsonBody(new { nombre = "Cargo Actualizado", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<CargoDto>(response);
        Assert.Equal("Cargo Actualizado", dto.Nombre);
    }

    [Fact]
    public async Task Put_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeCargoServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nombre = "No existe", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PutAsync($"/api/v1/cargos/{Guid.NewGuid()}", body);

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
        var fakeComandos = new FakeCargoServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nombre = "", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PutAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}", body);

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
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeCargoServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/cargos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task Delete_Conflict_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeCargoServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Conflict, "CargoConPuestosActivos",
                        "No se puede desactivar un cargo que tiene puestos activos asociados.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
        Assert.Equal("CargoConPuestosActivos", problem.Title);
    }

    // ---- PATCH (reactivar) ----

    [Fact]
    public async Task PatchReactivar_ValidRequest_Returns200OkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<CargoDto>(response);
        Assert.Equal(FakeCargoServicio.CargoId1, dto.Id);
    }

    [Fact]
    public async Task PatchReactivar_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeCargoServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.NotFound, "CargoNoEncontrado", "El cargo no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/cargos/{Guid.NewGuid()}/reactivar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task PatchReactivar_Conflict_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeCargoServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Conflict, "CodigoDuplicado",
                        "Ya existe un cargo activo con el mismo código.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }
}
