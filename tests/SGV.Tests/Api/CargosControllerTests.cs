using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class CargosControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public CargosControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cargos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync("/api/v1/cargos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<CargoDto>>(response);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
    }

    [Fact]
    public async Task GetById_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<CargoDto>(response);
        Assert.NotNull(dto);
        Assert.Equal(FakeCargoServicio.CargoId1, dto.Id);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioConsulta>();
            services.AddSingleton<ICargoServicioConsulta>(
                new FakeCargoServicio(isEmpty: true));
        });
        var client = factory.CreateAdminClient();

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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/cargos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ParentPayloadDoesNotIncludeSkillAssignmentFields()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("skillId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("habilidades", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_ParentPayloadNoContaminaCamposDelSubrecursoSkill()
    {
        // PR2-T2.4: el contrato padre GET /api/v1/cargos/{id} NO debe
        // empezar a exponer los campos del subrecurso /skills por este
        // cambio contractual (cargo-skill-query-contract Req 3 escenario
        // "No contaminar el contrato padre de Cargo").
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        // Campos del subrecurso que NO deben filtrarse al padre.
        Assert.DoesNotContain("nivelRequeridoId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ponderacion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("esObligatoria", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"skill\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"nivel\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CargoSkillDetailDto", json, StringComparison.OrdinalIgnoreCase);

        // Pero los campos propios del padre deben seguir presentes.
        Assert.Contains("\"id\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"codigo\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"nombre\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"nivelId\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAll_ParentPayloadNoContaminaCamposDelSubrecursoSkill()
    {
        // PR2-T2.4 (endurecido): GET /api/v1/cargos (lista) tampoco debe
        // empezar a exponer los campos del subrecurso /skills.
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/cargos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("nivelRequeridoId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ponderacion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("esObligatoria", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"habilidades\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.CargosController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.True(hasAuthorize, "Controller MUST require authorization");
    }

    // ---- JSON contract (nivelId / nivelNombre) ----

    [Fact]
    public async Task GetAll_JsonResponseContieneNivelIdYNivelNombre()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
    public async Task Post_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;
        var body = ToJsonBody(new { codigo = "NVO", nombre = "Nuevo Cargo", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PostAsync("/api/v1/cargos", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;
        var body = ToJsonBody(new { codigo = "DIRECTOR", nombre = "Nuevo", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PutAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.DeleteAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.PatchAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST",   "/api/v1/cargos")]
    [InlineData("PUT",    "/api/v1/cargos/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/v1/cargos/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH",  "/api/v1/cargos/00000000-0000-0000-0000-000000000001/reactivar")]
    public async Task Mutation_WithoutCredentials_ReturnsUnauthorized(string method, string path)
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        HttpResponseMessage response = method switch
        {
            "POST"   => await client.PostAsync(path, ToJsonBody(new { codigo = "NVO", nombre = "Nuevo", nivelId = Guid.NewGuid() })),
            "PUT"    => await client.PutAsync(path,  ToJsonBody(new { codigo = "NVO", nombre = "Nuevo", nivelId = Guid.NewGuid() })),
            "DELETE" => await client.DeleteAsync(path),
            "PATCH"  => await client.PatchAsync(path, null),
            _        => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported HTTP method")
        };

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidRequest_Returns201CreatedWithDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { codigo = "DIRECTOR", nombre = "Duplicado", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PostAsync("/api/v1/cargos", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }

    // ---- PUT (update) ----

    [Fact]
    public async Task Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { codigo = "DIRECTOR", nombre = "Cargo Actualizado", nivelId = FakeCargoServicioComandos.DefaultNivelId });

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { codigo = "DIRECTOR", nombre = "No existe", nivelId = FakeCargoServicioComandos.DefaultNivelId });

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { codigo = "DIRECTOR", nombre = "", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PutAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "nombre");
    }

    [Fact]
    public async Task Put_EmptyCodigo_Returns400WithFieldErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = ["'Codigo' no debe estar vacío."]
        };
        var fakeComandos = new FakeCargoServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { codigo = "", nombre = "Cargo Actualizado", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PutAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorFieldExists(response, "codigo");
    }

    [Fact]
    public async Task Put_DuplicateActiveCodigo_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeCargoServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                CargoCommandResult.Failure(
                    new CargoError(CargoErrorType.Conflict, "CodigoDuplicado",
                        "Ya existe un cargo activo con el mismo código.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { codigo = "OTRO", nombre = "Cargo Duplicado", nivelId = FakeCargoServicioComandos.DefaultNivelId });

        var response = await client.PutAsync($"/api/v1/cargos/{FakeCargoServicio.CargoId1}", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
        Assert.Equal("CodigoDuplicado", problem.Title);
    }

    // ---- DELETE (soft-delete) ----

    [Fact]
    public async Task Delete_ExistingId_Returns204NoContent()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioComandos>();
            services.AddSingleton<ICargoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/cargos/{FakeCargoServicio.CargoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }

    // ---- GET /api/v1/cargos/consulta ----

    [Fact]
    public async Task GetConsulta_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cargos/consulta?status=eliminadas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConsulta_StatusEliminadas_RetornaSoloEliminadas()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioConsulta>();
            services.AddSingleton<ICargoServicioConsulta>(new FakeCargoServicio(withEliminadas: true));
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/cargos/consulta?status=eliminadas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PagedResult<CargoDto>>(response);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(FakeCargoServicio.CargoEliminadoId1, page.Items[0].Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetConsulta_StatusInvalido_CaeA_Activas()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioConsulta>();
            services.AddSingleton<ICargoServicioConsulta>(new FakeCargoServicio(withEliminadas: true));
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/cargos/consulta?status=archivo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PagedResult<CargoDto>>(response);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(FakeCargoServicio.CargoId1, page.Items[0].Id);
    }

    [Fact]
    public async Task GetConsulta_SinStatus_RetornaActivas()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/cargos/consulta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PagedResult<CargoDto>>(response);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(FakeCargoServicio.CargoId1, page.Items[0].Id);
    }

    [Fact]
    public async Task GetConsulta_PropagaSortAlServicio()
    {
        // El controller DEBE pasar el `sort` del query string al servicio
        // para que el repositorio pueda aplicarlo server-side antes de
        // paginar (REQ-CM-01). Si el `sort` no se propaga, el fake
        // capturaría null y este test fallaría.
        var capture = new SortCapturingFake();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioConsulta>();
            services.AddSingleton<ICargoServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/cargos/consulta?sort=nombre_desc&page=2&pageSize=5&status=activas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedSorts);
        Assert.Equal("nombre_desc", observed);
    }

    [Fact]
    public async Task GetConsulta_SortInvalido_NoLanzaYLlegaAlServicio()
    {
        // El controller NO debe filtrar el `sort`; cualquier valor
        // (incluso inválido) llega al servicio para que el repositorio
        // decida si lo aplica o cae al orden por defecto.
        var capture = new SortCapturingFake();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoServicioConsulta>();
            services.AddSingleton<ICargoServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/cargos/consulta?sort=foo_bar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("foo_bar", Assert.Single(capture.CapturedSorts));
    }

    /// <summary>
    /// Fake en memoria que captura el <c>Sort</c> recibido por el servicio
    /// para validar el contrato entre controller y aplicación.
    /// </summary>
    private sealed class SortCapturingFake : ICargoServicioConsulta
    {
        public List<string?> CapturedSorts { get; } = new();

        public Task<IReadOnlyList<CargoDto>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CargoDto>>(
                [new(FakeCargoServicio.CargoId1, "DIRECTOR", "Director", null, Guid.Parse("70000000-0000-0000-0000-000000000001"))]);

        public Task<CargoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<CargoDto?>(null);

        public Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken ct = default)
        {
            CapturedSorts.Add(query.Sort);
            return Task.FromResult(new PagedResult<CargoDto>(
                [new(FakeCargoServicio.CargoId1, "DIRECTOR", "Director", null, Guid.Parse("70000000-0000-0000-0000-000000000001"))],
                1, query.Page, query.PageSize));
        }
    }
}
