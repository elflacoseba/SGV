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

public sealed class PuestosControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/puestos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<PuestoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(FakePuestoServicio.PuestoId1, dtos[0].Id);
        Assert.Equal("GER-001", dtos[0].Codigo);
        Assert.Equal("Gerencia General", dtos[0].UnidadOrganizativaNombre);
        Assert.Equal("Director", dtos[0].CargoNombre);
    }

    [Fact]
    public async Task GetAll_WhenNoData_ReturnsOkWithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioConsulta>();
            services.AddSingleton<IPuestoServicioConsulta>(
                new FakePuestoServicio(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/puestos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<PuestoDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<PuestoDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakePuestoServicio.PuestoId1, dto.Id);
        Assert.Equal("Gerente General", dto.Nombre);
        Assert.Equal("Gerencia General", dto.UnidadOrganizativaNombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/puestos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.PuestosController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.False(hasAuthorize, "Controller should not require authorization");
    }

    // ---- Write endpoint helpers ----

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

    // ---- POST (create) ----

    [Fact]
    public async Task Post_ValidRequest_Returns201CreatedWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var body = ToJsonBody(new
        {
            codigo = "NVO",
            nombre = "Nuevo Puesto",
            unidadOrganizativaId = FakePuestoServicioComandos.DefaultUnidadId,
            cargoId = FakePuestoServicioComandos.DefaultCargoId
        });

        var response = await client.PostAsync("/api/v1/puestos", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await ReadAsAsync<PuestoDto>(response);
        Assert.Equal("NVO", dto.Codigo);
        Assert.Equal("Nuevo Puesto", dto.Nombre);
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
        var fakeComandos = new FakePuestoServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "", nombre = "", unidadOrganizativaId = Guid.NewGuid(), cargoId = Guid.NewGuid() });

        var response = await client.PostAsync("/api/v1/puestos", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "codigo");
        await AssertErrorFieldExists(response, "nombre");
    }

    [Fact]
    public async Task Post_DuplicateCode_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakePuestoServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.Conflict, "CodigoDuplicado", "Ya existe un puesto activo con el mismo código.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new
        {
            codigo = "EXISTENTE",
            nombre = "Duplicado",
            unidadOrganizativaId = FakePuestoServicioComandos.DefaultUnidadId,
            cargoId = FakePuestoServicioComandos.DefaultCargoId
        });

        var response = await client.PostAsync("/api/v1/puestos", body);

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
        var body = ToJsonBody(new { nombre = "Puesto Actualizado" });

        var response = await client.PutAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<PuestoDto>(response);
        Assert.Equal("Puesto Actualizado", dto.Nombre);
    }

    [Fact]
    public async Task Put_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakePuestoServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nombre = "No existe" });

        var response = await client.PutAsync($"/api/v1/puestos/{Guid.NewGuid()}", body);

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
        var fakeComandos = new FakePuestoServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nombre = "" });

        var response = await client.PutAsync($"/api/v1/puestos/{FakePuestoServicio.PuestoId1}", body);

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
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakePuestoServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/puestos/{Guid.NewGuid()}");

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
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<PuestoDto>(response);
        Assert.Equal(FakePuestoServicio.PuestoId1, dto.Id);
    }

    [Fact]
    public async Task PatchReactivar_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakePuestoServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/puestos/{Guid.NewGuid()}/reactivar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task PatchReactivar_Conflict_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakePuestoServicioComandos
        {
            ReactivarHandler = (_, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.Conflict, "CodigoDuplicado",
                        "Ya existe un puesto activo con el mismo código.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }
}
