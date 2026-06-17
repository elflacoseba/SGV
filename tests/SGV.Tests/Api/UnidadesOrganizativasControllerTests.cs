using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class UnidadesOrganizativasControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Guid UnidadId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid UnidadPadreId = Guid.Parse("b0000000-0000-0000-0000-000000000002");

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
        // ProblemDetails may have extensions; deserialize as base for status/title
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

    // ---- GET endpoints (existing) ----

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/unidades-organizativas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<UnidadOrganizativaDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(FakeUnidadOrganizativaServicio.UnidadId1, dtos[0].Id);
        Assert.Equal("GER", dtos[0].Codigo);
    }

    [Fact]
    public async Task GetAll_WhenNoData_ReturnsOkWithEmptyArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IUnidadOrganizativaServicioConsulta>();
            services.AddSingleton<IUnidadOrganizativaServicioConsulta>(
                new FakeUnidadOrganizativaServicio(isEmpty: true));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/unidades-organizativas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dtos = JsonSerializer.Deserialize<List<UnidadOrganizativaDto>>(json, JsonOptions);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/unidades-organizativas/{FakeUnidadOrganizativaServicio.UnidadId1}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<UnidadOrganizativaDto>(json, JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(FakeUnidadOrganizativaServicio.UnidadId1, dto.Id);
        Assert.Equal("Gerencia General", dto.Nombre);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/unidades-organizativas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Controller_DoesNotHaveAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.UnidadesOrganizativasController);

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
        var body = ToJsonBody(new { codigo = "NUEVO", nombre = "Nueva Unidad", tipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId });

        var response = await client.PostAsync("/api/v1/unidades-organizativas", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await ReadAsAsync<UnidadOrganizativaDto>(response);
        Assert.Equal("NUEVO", dto.Codigo);
        Assert.Equal("Nueva Unidad", dto.Nombre);
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
        var fakeComandos = new FakeUnidadOrganizativaServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IUnidadOrganizativaServicioComandos>();
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "", nombre = "", tipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId });

        var response = await client.PostAsync("/api/v1/unidades-organizativas", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "codigo");
        await AssertErrorFieldExists(response, "nombre");
    }

    [Fact]
    public async Task Post_DuplicateCode_Returns409WithProblemDetails()
    {
        var fakeComandos = new FakeUnidadOrganizativaServicioComandos
        {
            CrearHandler = (_, _) => Task.FromResult(
                UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado", "Ya existe una unidad activa con el mismo código.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IUnidadOrganizativaServicioComandos>();
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "GER", nombre = "Duplicado", tipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId });

        var response = await client.PostAsync("/api/v1/unidades-organizativas", body);

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
        var body = ToJsonBody(new { codigo = "GER-UPD", nombre = "Actualizada", tipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.DireccionId });

        var response = await client.PutAsync($"/api/v1/unidades-organizativas/{UnidadId}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<UnidadOrganizativaDto>(response);
        Assert.Equal("GER-UPD", dto.Codigo);
    }

    [Fact]
    public async Task Put_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeUnidadOrganizativaServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IUnidadOrganizativaServicioComandos>();
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "NON", nombre = "No existe", tipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId });

        var response = await client.PutAsync($"/api/v1/unidades-organizativas/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task Put_ValidationError_Returns400WithFieldErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["codigo"] = ["'Codigo' no debe estar vacío."]
        };
        var fakeComandos = new FakeUnidadOrganizativaServicioComandos
        {
            ActualizarHandler = (id, _, _) => Task.FromResult(
                UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                    fieldErrors))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IUnidadOrganizativaServicioComandos>();
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { codigo = "", nombre = "Test", tipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId });

        var response = await client.PutAsync($"/api/v1/unidades-organizativas/{UnidadId}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
        await AssertErrorFieldExists(response, "codigo");
    }

    // ---- PATCH (parent change) ----

    [Fact]
    public async Task PatchParent_ValidRequest_Returns200OkWithDto()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var body = ToJsonBody(new { unidadPadreId = UnidadPadreId });

        var response = await client.PatchAsync(
            $"/api/v1/unidades-organizativas/{UnidadId}/unidad-padre", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<UnidadOrganizativaDto>(response);
        Assert.Equal(UnidadPadreId, dto.UnidadPadreId);
    }

    [Fact]
    public async Task PatchParent_SelfParent_Returns400WithProblemDetails()
    {
        var fakeComandos = new FakeUnidadOrganizativaServicioComandos
        {
            CambiarUnidadPadreHandler = (id, _, _) => Task.FromResult(
                UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.Validation, "CicloJerarquico", "Una unidad no puede ser padre de sí misma.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IUnidadOrganizativaServicioComandos>();
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { unidadPadreId = UnidadId });

        var response = await client.PatchAsync(
            $"/api/v1/unidades-organizativas/{UnidadId}/unidad-padre", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
    }

    // ---- DELETE (soft-delete) ----

    [Fact]
    public async Task Delete_ExistingId_Returns204NoContent()
    {
        using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/unidades-organizativas/{UnidadId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404WithProblemDetails()
    {
        var fakeComandos = new FakeUnidadOrganizativaServicioComandos
        {
            EliminarHandler = (_, _) => Task.FromResult(
                UnidadOrganizativaCommandResult.Failure(
                    new UnidadOrganizativaError(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IUnidadOrganizativaServicioComandos>();
            services.AddSingleton<IUnidadOrganizativaServicioComandos>(fakeComandos);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/unidades-organizativas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }
}
