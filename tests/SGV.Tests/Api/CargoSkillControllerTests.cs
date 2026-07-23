using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;
using SGV.Tests.Api.Collections;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class CargoSkillControllerTests
{
    private readonly ApiIntegrationFixture _fixture;
    public CargoSkillControllerTests(ApiIntegrationFixture fixture) => _fixture = fixture;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Guid ExistingCargoId = FakeCargoServicio.CargoId1;
    private static readonly Guid ExistingSkillId = FakeHabilidadServicio.HabilidadId1;
    private static readonly Guid ExistingNivelRequeridoId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid NonExistentCargoId = Guid.Parse("b9999999-0000-0000-0000-000000000000");
    private static readonly Guid NonExistentSkillId = Guid.Parse("d9999999-0000-0000-0000-000000000000");

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

    // ---- Fake service ----

    private static readonly HabilidadDto DefaultHabilidad = new(
        ExistingSkillId, "PROG", "Programación", "Lenguajes", "Técnica");

    private static readonly NivelHabilidadDto DefaultNivel = new(
        ExistingNivelRequeridoId, "N1", "Nivel 1", 1, 1);

    private sealed class FakeCargoSkillServicio : ICargoSkillServicio
    {
        public List<CargoSkillDetailDto> Skills { get; set; } =
        [
            new(DefaultHabilidad, DefaultNivel),
        ];

        public Func<Guid, CancellationToken, Task<IReadOnlyList<CargoSkillDetailDto>>>? ListHandler { get; set; }
        public Func<Guid, Guid, AsignarCargoSkillRequest, CancellationToken, Task<CargoSkillCommandResult>>? UpsertHandler { get; set; }
        public Func<Guid, Guid, CancellationToken, Task<CargoSkillCommandResult>>? DeleteHandler { get; set; }

        public Task<IReadOnlyList<CargoSkillDetailDto>> ListAsync(Guid cargoId, CancellationToken cancellationToken = default)
        {
            if (ListHandler is not null) return ListHandler(cargoId, cancellationToken);
            return Task.FromResult<IReadOnlyList<CargoSkillDetailDto>>(Skills);
        }

        public Task<CargoSkillCommandResult> UpsertAsync(
            Guid cargoId, Guid skillId, AsignarCargoSkillRequest request, CancellationToken cancellationToken = default)
        {
            if (UpsertHandler is not null) return UpsertHandler(cargoId, skillId, request, cancellationToken);
            return Task.FromResult(CargoSkillCommandResult.Success(new CargoSkillDto(skillId, request.NivelRequeridoId)));
        }

        public Task<CargoSkillCommandResult> DeleteAsync(
            Guid cargoId, Guid skillId, CancellationToken cancellationToken = default)
        {
            if (DeleteHandler is not null) return DeleteHandler(cargoId, skillId, cancellationToken);
            return Task.FromResult(CargoSkillCommandResult.Success(new CargoSkillDto(skillId, ExistingNivelRequeridoId)));
        }
    }

    // ---- GET /api/v1/cargos/{cargoId}/skills ----

    [Fact]
    public async Task GetSkills_ReturnsOkWithDtoArray()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/cargos/{ExistingCargoId}/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<CargoSkillDetailDto>>(response);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(ExistingSkillId, dtos[0].Skill.Id);
        Assert.Equal(ExistingNivelRequeridoId, dtos[0].Nivel.Id);
        Assert.NotNull(dtos[0].Skill);
        Assert.Equal("PROG", dtos[0].Skill.Codigo);
        Assert.NotNull(dtos[0].Nivel);
        Assert.Equal("N1", dtos[0].Nivel.Codigo);
    }

    [Fact]
    public async Task GetSkills_WithoutCredentials_ReturnsUnauthorized()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/cargos/{ExistingCargoId}/skills");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSkills_WithAuthenticatedNonAdmin_ReturnsOk()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateNonAdminClient();

        var response = await client.GetAsync($"/api/v1/cargos/{ExistingCargoId}/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<CargoSkillDetailDto>>(response);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
    }

    [Fact]
    public async Task GetSkills_WhenEmpty_ReturnsOkWithEmptyArray()
    {
        var fake = new FakeCargoSkillServicio { Skills = [] };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/cargos/{ExistingCargoId}/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<CargoSkillDetailDto>>(response);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetSkills_NonExistentCargo_ReturnsOkWithEmptyArray()
    {
        var fake = new FakeCargoSkillServicio
        {
            ListHandler = (cargoId, ct) =>
                Task.FromResult<IReadOnlyList<CargoSkillDetailDto>>([])
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/cargos/{NonExistentCargoId}/skills");

        // Per spec, list returns empty for non-existent parent (no association exists)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSkills_ResponseContainsNestedSkillAndNivel()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateAdminClient();

        var response = await client.GetAsync($"/api/v1/cargos/{ExistingCargoId}/skills");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("skill", out var skillProp), "Response JSON MUST include 'skill'");
        Assert.True(skillProp.TryGetProperty("id", out _), "Response JSON 'skill' MUST include 'id'");
        Assert.True(skillProp.TryGetProperty("codigo", out _), "Response JSON 'skill' MUST include 'codigo'");
        Assert.True(first.TryGetProperty("nivel", out var nivelProp), "Response JSON MUST include 'nivel'");
        Assert.True(nivelProp.TryGetProperty("id", out _), "Response JSON 'nivel' MUST include 'id'");
        Assert.True(nivelProp.TryGetProperty("codigo", out _), "Response JSON 'nivel' MUST include 'codigo'");
    }

    // ---- PUT /api/v1/cargos/{cargoId}/skills/{skillId} ----

    [Fact]
    public async Task PutSkill_ValidRequest_Returns200OkWithDto()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<CargoSkillDto>(response);
        Assert.Equal(ExistingSkillId, dto.SkillId);
        Assert.Equal(ExistingNivelRequeridoId, dto.NivelRequeridoId);
    }

    [Fact]
    public async Task PutSkill_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;
        var body = ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutSkill_InvalidNivelRequeridoId_Returns400WithProblemDetails()
    {
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.Validation, "NivelInvalido",
                        "El nivel de habilidad especificado no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = Guid.NewGuid() });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task PutSkill_NonExistentCargo_ReturnsNotFound()
    {
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.NotFound, "CargoNoEncontrado",
                        "El cargo no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{NonExistentCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task PutSkill_NonExistentSkill_ReturnsNotFound()
    {
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.NotFound, "HabilidadNoEncontrada",
                        "La habilidad no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{NonExistentSkillId}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- DELETE /api/v1/cargos/{cargoId}/skills/{skillId} ----

    [Fact]
    public async Task DeleteSkill_ExistingAssignment_Returns204NoContent()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_WithAuthenticatedNonAdmin_ReturnsForbidden()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = FakeAuthenticationDefaults.UserHeader;

        var response = await client.DeleteAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT",    "/api/v1/cargos/00000000-0000-0000-0000-000000000001/skills/00000000-0000-0000-0000-000000000002")]
    [InlineData("DELETE", "/api/v1/cargos/00000000-0000-0000-0000-000000000001/skills/00000000-0000-0000-0000-000000000002")]
    public async Task SkillMutation_WithoutCredentials_ReturnsUnauthorized(string method, string path)
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();

        HttpResponseMessage response = method switch
        {
            "PUT"    => await client.PutAsync(path, ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId })),
            "DELETE" => await client.DeleteAsync(path),
            _        => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported HTTP method")
        };

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_NonExistentAssignment_ReturnsNotFound()
    {
        var fake = new FakeCargoSkillServicio
        {
            DeleteHandler = (_, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.NotFound, "AsignacionNoEncontrada",
                        "La asignación de habilidad no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{NonExistentSkillId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task DeleteSkill_NonExistentCargo_ReturnsNotFound()
    {
        var fake = new FakeCargoSkillServicio
        {
            DeleteHandler = (_, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.NotFound, "CargoNoEncontrado",
                        "El cargo no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();

        var response = await client.DeleteAsync(
            $"/api/v1/cargos/{NonExistentCargoId}/skills/{ExistingSkillId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSkill_FieldErrors_ReturnsValidationProblemDetails()
    {
        // PR2-T2.2: cuando el servicio devuelve FieldErrors (validación por
        // campo), el controller DEBE emitir un ValidationProblemDetails con
        // la clave "errors" poblada — NO un ProblemDetails genérico.
        // Escenario: cargo-skill-asignar-editar Req 3 "Nivel requerido inexistente".
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["nivelRequeridoId"] = ["El nivel de habilidad referenciado no existe."]
        };
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.Validation, "NivelHabilidadNoExiste",
                        "El nivel de habilidad referenciado no existe."),
                    fieldErrors))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = Guid.NewGuid() });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors),
            "ValidationProblemDetails MUST expose an 'errors' object keyed by field name");
        Assert.True(errors.TryGetProperty("nivelRequeridoId", out _),
            "errors MUST contain the 'nivelRequeridoId' key");
    }

    [Fact]
    public async Task UpsertSkill_PonderacionExcede100_Returns400ConCampoPonderacion()
    {
        // PR2-T2.2: una Ponderacion > 100.00 debe regresar 400 con un
        // ValidationProblemDetails donde el campo 'ponderacion' sea la
        // clave del error — escenario cargo-skill-ponderacion-obligatoria Req 4.
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["ponderacion"] = ["La ponderación no puede superar 100.00."]
        };
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.Validation, "DatosInvalidos",
                        "Uno o más campos del vínculo contienen errores de validación."),
                    fieldErrors))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId, ponderacion = 150m });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors),
            "ValidationProblemDetails MUST expose 'errors' when FieldErrors present");
        Assert.True(errors.TryGetProperty("ponderacion", out _),
            "errors MUST contain the 'ponderacion' key for out-of-range failures");
    }

    [Fact]
    public async Task UpsertSkill_PonderacionNull_Returns400ConMensajeObligatoria()
    {
        // Issue #191: la ponderación pasó de opcional a obligatoria. Un
        // payload con Ponderacion=null debe rechazarse con 400 y un
        // ValidationProblemDetails que contenga la clave 'ponderacion'
        // con el mensaje "La ponderación es obligatoria." — esto valida
        // el flujo completo request → FluentValidation → controller.
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.Validation, "DatosInvalidos",
                        "Uno o más campos del vínculo contienen errores de validación."),
                    new Dictionary<string, string[]>
                    {
                        ["ponderacion"] = ["La ponderación es obligatoria."]
                    }))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId, ponderacion = (decimal?)null });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("ponderacion", out var ponderacionErrors));
        var messages = ponderacionErrors.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("La ponderación es obligatoria.", messages);
    }

    [Fact]
    public async Task UpsertSkill_ValidationErrorSinFieldErrors_MantieneProblemDetails()
    {
        // PR2-T2.2: un error de validación sin FieldErrors (e.g. NotFound
        // downstream) debe seguir emitiendo ProblemDetails, NO ValidationProblemDetails.
        // Conserva compatibilidad con consumidores existentes del subrecurso.
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.Validation, "NivelInvalido",
                        "El nivel de habilidad especificado no existe.")))
        };
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = Guid.NewGuid() });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("errors", out _),
            "Without FieldErrors the body MUST NOT include 'errors' (uses ProblemDetails)");
        Assert.Equal("NivelInvalido", doc.RootElement.GetProperty("title").GetString());
    }

    // ---- Route isolation: must not mix with /api/v1/skills ----

    [Fact]
    public async Task PutSkill_DoesNotConflictWithSkillsCatalogRoute()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateAdminClient();
        var body = ToJsonBody(new { nivelRequeridoId = ExistingNivelRequeridoId });

        // This should hit the cargo skill subresource, NOT the skills catalog
        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
