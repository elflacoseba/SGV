using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class PuestosControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public PuestosControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAll_ReturnsOkWithDtoArray()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioConsulta>();
            services.AddSingleton<IPuestoServicioConsulta>(
                new FakePuestoServicio(isEmpty: true));
        });
        var client = factory.CreateAdminClient();

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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/puestos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var controllerType = typeof(SGV.Api.Controllers.PuestosController);

        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any(a => a is AuthorizeAttribute);

        Assert.True(hasAuthorize, "Controller MUST require authorization at class level");
    }

    // ---- Anonymous (no credentials) authorization matrix ----

    [Fact]
    public async Task GetAll_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/puestos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Authenticated non-admin → 403 Forbidden on writes ----

    [Fact]
    public async Task Create_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;
        var body = ToJsonBody(new
        {
            codigo = "NVO",
            nombre = "Nuevo Puesto",
            unidadOrganizativaId = FakePuestoServicioComandos.DefaultUnidadId,
            cargoId = FakePuestoServicioComandos.DefaultCargoId
        });

        var response = await client.PostAsync("/api/v1/puestos", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;
        var body = ToJsonBody(new { nombre = "Sin Permiso" });

        var response = await client.PutAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.DeleteAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.PatchAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Any mutation without credentials → 401 ----

    [Theory]
    [InlineData("POST",   "/api/v1/puestos")]
    [InlineData("PUT",    "/api/v1/puestos/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/v1/puestos/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH",  "/api/v1/puestos/00000000-0000-0000-0000-000000000001/reactivar")]
    public async Task Mutation_WithoutCredentials_ReturnsUnauthorized(string method, string path)
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        HttpResponseMessage response = method switch
        {
            "POST"   => await client.PostAsync(path, ToJsonBody(new
            {
                codigo = "NVO",
                nombre = "Nuevo Puesto",
                unidadOrganizativaId = Guid.NewGuid(),
                cargoId = Guid.NewGuid()
            })),
            "PUT"    => await client.PutAsync(path, ToJsonBody(new { nombre = "Sin creds" })),
            "DELETE" => await client.DeleteAsync(path),
            "PATCH"  => await client.PatchAsync(path, null),
            _        => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported HTTP method")
        };

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();
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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();
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
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync($"/api/v1/puestos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    // ---- PATCH (reactivar) ----

    [Fact]
    public async Task PatchReactivar_ValidRequest_Returns200OkWithDto()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

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
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.PatchAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}/reactivar", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
    }

    // ---- REQ-PTO-002: GET /api/v1/puestos/consulta ----

    [Fact]
    public async Task GetConsulta_WithoutCredentials_ReturnsUnauthorized()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/puestos/consulta?status=eliminadas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConsulta_SinStatus_RetornaActivas()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/puestos/consulta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PagedResult<PuestoDto>>(response);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(FakePuestoServicio.PuestoId1, page.Items[0].Id);
    }

    [Fact]
    public async Task GetConsulta_PropagaSortAlServicio()
    {
        // El controller DEBE pasar el `sort` del query string al servicio
        // para que el repositorio pueda aplicarlo server-side antes de
        // paginar (REQ-PTO-001). Si el `sort` no se propaga, el fake
        // capturaría null y este test fallaría.
        var capture = new SortCapturingFake();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioConsulta>();
            services.AddSingleton<IPuestoServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/puestos/consulta?sort=nombre_desc&page=2&pageSize=5&status=activas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedSorts);
        Assert.Equal("nombre_desc", observed);
    }

    [Fact]
    public async Task GetConsulta_ConSearchPageSize_DevuelvePagedResult()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/puestos/consulta?search=GER&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PagedResult<PuestoDto>>(response);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task GetConsulta_ConSortCodigoAsc_FluyeAlServicio()
    {
        var capture = new SortCapturingFake();
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioConsulta>();
            services.AddSingleton<IPuestoServicioConsulta>(capture);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/puestos/consulta?sort=codigo_asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var observed = Assert.Single(capture.CapturedSorts);
        Assert.Equal("codigo_asc", observed);
    }

    [Fact]
    public async Task GetConsulta_ConStatusInvalido_CaeA_Activas()
    {
        // Paridad con Cargos: status inválido cae al segmento Activas.
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync("/api/v1/puestos/consulta?status=archivo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsAsync<PagedResult<PuestoDto>>(response);
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(FakePuestoServicio.PuestoId1, page.Items[0].Id);
    }

    // ---- REQ-PTO-010: DELETE 409 cuando hay ocupaciones vigentes ----

    [Fact]
    public async Task Delete_ConOcupacionesVigentes_Devuelve409ConProblemDetails()
    {
        var fakeComandos = new FakePuestoServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(
                        PuestoErrorType.Conflict,
                        "PuestoConOcupacionesActivas",
                        "El puesto tiene ocupaciones vigentes y no puede darse de baja.",
                        null,
                        ErrorCategoria.Conflict)))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(409, problem.Status);
        // El código estable se propaga al ProblemDetails (vía ApiResults).
        Assert.Equal("PuestoConOcupacionesActivas", problem.Title);
    }

    [Fact]
    public async Task Delete_SinOcupaciones_Devuelve204NoContent()
    {
        var factory = _fixture.RootFactory;
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/puestos/{FakePuestoServicio.PuestoId1}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_PuestoInexistente_Devuelve404ConProblemDetails()
    {
        var fakeComandos = new FakePuestoServicioComandos
        {
            DesactivarHandler = (_, _) => Task.FromResult(
                PuestoCommandResult.Failure(
                    new PuestoError(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<IPuestoServicioComandos>();
            services.AddSingleton<IPuestoServicioComandos>(fakeComandos);
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync($"/api/v1/puestos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    /// <summary>
    /// Fake en memoria que captura el <c>Sort</c> recibido por el servicio
    /// para validar el contrato entre controller y aplicación.
    /// Espejo del patrón de <c>CargosControllerTests</c>.
    /// </summary>
    private sealed class SortCapturingFake : IPuestoServicioConsulta
    {
        public List<string?> CapturedSorts { get; } = new();

        public Task<IReadOnlyList<PuestoDto>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PuestoDto>>(
                [new(FakePuestoServicio.PuestoId1, "GER-001", "Gerente General", null,
                    FakePuestoServicio.UnidadId1, "Gerencia General",
                    FakePuestoServicio.CargoId1, "Director", null)]);

        public Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<PuestoDto?>(null);

        public Task<PagedResult<PuestoDto>> QueryAsync(PuestoListQuery query, CancellationToken ct = default)
        {
            CapturedSorts.Add(query.Sort);
            return Task.FromResult(new PagedResult<PuestoDto>(
                [new(FakePuestoServicio.PuestoId1, "GER-001", "Gerente General", null,
                    FakePuestoServicio.UnidadId1, "Gerencia General",
                    FakePuestoServicio.CargoId1, "Director", null)],
                1, query.Page, query.PageSize));
        }
    }
}
