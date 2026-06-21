using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class CargoSkillControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Guid ExistingCargoId = FakeCargoServicio.CargoId1;
    private static readonly Guid ExistingSkillId = FakeHabilidadServicio.HabilidadId1;
    private static readonly Guid ExistingNivelId = Guid.Parse("70000000-0000-0000-0000-000000000001");
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
        ExistingNivelId, "N1", "Nivel 1", 1, 1);

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
            return Task.FromResult(CargoSkillCommandResult.Success(new CargoSkillDto(skillId, request.NivelId)));
        }

        public Task<CargoSkillCommandResult> DeleteAsync(
            Guid cargoId, Guid skillId, CancellationToken cancellationToken = default)
        {
            if (DeleteHandler is not null) return DeleteHandler(cargoId, skillId, cancellationToken);
            return Task.FromResult(CargoSkillCommandResult.Success(new CargoSkillDto(skillId, ExistingNivelId)));
        }
    }

    // ---- GET /api/v1/cargos/{cargoId}/skills ----

    [Fact]
    public async Task GetSkills_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/cargos/{ExistingCargoId}/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<CargoSkillDetailDto>>(response);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(ExistingSkillId, dtos[0].Skill.Id);
        Assert.Equal(ExistingNivelId, dtos[0].Nivel.Id);
        Assert.NotNull(dtos[0].Skill);
        Assert.Equal("PROG", dtos[0].Skill.Codigo);
        Assert.NotNull(dtos[0].Nivel);
        Assert.Equal("N1", dtos[0].Nivel.Codigo);
    }

    [Fact]
    public async Task GetSkills_WhenEmpty_ReturnsOkWithEmptyArray()
    {
        var fake = new FakeCargoSkillServicio { Skills = [] };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/cargos/{NonExistentCargoId}/skills");

        // Per spec, list returns empty for non-existent parent (no association exists)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSkills_ResponseContainsSkillIdAndNivelId()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = ExistingNivelId });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<CargoSkillDto>(response);
        Assert.Equal(ExistingSkillId, dto.SkillId);
        Assert.Equal(ExistingNivelId, dto.NivelId);
    }

    [Fact]
    public async Task PutSkill_InvalidNivelId_Returns400WithProblemDetails()
    {
        var fake = new FakeCargoSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                CargoSkillCommandResult.Failure(
                    new CargoSkillError(CargoSkillErrorType.Validation, "NivelInvalido",
                        "El nivel de habilidad especificado no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = Guid.NewGuid() });

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = ExistingNivelId });

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = ExistingNivelId });

        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{NonExistentSkillId}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- DELETE /api/v1/cargos/{cargoId}/skills/{skillId} ----

    [Fact]
    public async Task DeleteSkill_ExistingAssignment_Returns204NoContent()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateClient();

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
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio>(fake);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/cargos/{NonExistentCargoId}/skills/{ExistingSkillId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Route isolation: must not mix with /api/v1/skills ----

    [Fact]
    public async Task PutSkill_DoesNotConflictWithSkillsCatalogRoute()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<ICargoSkillServicio>();
            services.AddSingleton<ICargoSkillServicio, FakeCargoSkillServicio>();
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = ExistingNivelId });

        // This should hit the cargo skill subresource, NOT the skills catalog
        var response = await client.PutAsync(
            $"/api/v1/cargos/{ExistingCargoId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
