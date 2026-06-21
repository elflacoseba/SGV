using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Api;

public sealed class PersonaSkillControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Guid ExistingPersonaId = FakePersonaServicioConsulta.PersonaId1;
    private static readonly Guid ExistingSkillId = FakeHabilidadServicio.HabilidadId1;
    private static readonly Guid ExistingNivelId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid NonExistentPersonaId = Guid.Parse("e9999999-0000-0000-0000-000000000000");
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

    private sealed class FakePersonaSkillServicio : IPersonaSkillServicio
    {
        public List<PersonaSkillDto> Skills { get; set; } =
        [
            new(ExistingSkillId, ExistingNivelId),
        ];

        public Func<Guid, CancellationToken, Task<IReadOnlyList<PersonaSkillDto>>>? ListHandler { get; set; }
        public Func<Guid, Guid, AsignarPersonaSkillRequest, CancellationToken, Task<PersonaSkillCommandResult>>? UpsertHandler { get; set; }
        public Func<Guid, Guid, CancellationToken, Task<PersonaSkillCommandResult>>? DeleteHandler { get; set; }

        public Task<IReadOnlyList<PersonaSkillDto>> ListAsync(Guid personaId, CancellationToken cancellationToken = default)
        {
            if (ListHandler is not null) return ListHandler(personaId, cancellationToken);
            return Task.FromResult<IReadOnlyList<PersonaSkillDto>>(Skills);
        }

        public Task<PersonaSkillCommandResult> UpsertAsync(
            Guid personaId, Guid skillId, AsignarPersonaSkillRequest request, CancellationToken cancellationToken = default)
        {
            if (UpsertHandler is not null) return UpsertHandler(personaId, skillId, request, cancellationToken);
            return Task.FromResult(PersonaSkillCommandResult.Success(new PersonaSkillDto(skillId, request.NivelId)));
        }

        public Task<PersonaSkillCommandResult> DeleteAsync(
            Guid personaId, Guid skillId, CancellationToken cancellationToken = default)
        {
            if (DeleteHandler is not null) return DeleteHandler(personaId, skillId, cancellationToken);
            return Task.FromResult(PersonaSkillCommandResult.Success(new PersonaSkillDto(skillId, ExistingNivelId)));
        }
    }

    // ---- GET /api/v1/personas/{personaId}/skills ----

    [Fact]
    public async Task GetSkills_ReturnsOkWithDtoArray()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, FakePersonaSkillServicio>();
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/personas/{ExistingPersonaId}/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<PersonaSkillDto>>(response);
        Assert.NotNull(dtos);
        Assert.NotEmpty(dtos);
        Assert.Equal(ExistingSkillId, dtos[0].SkillId);
        Assert.Equal(ExistingNivelId, dtos[0].NivelId);
    }

    [Fact]
    public async Task GetSkills_WhenEmpty_ReturnsOkWithEmptyArray()
    {
        var fake = new FakePersonaSkillServicio { Skills = [] };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio>(fake);
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/personas/{ExistingPersonaId}/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dtos = await ReadAsAsync<List<PersonaSkillDto>>(response);
        Assert.NotNull(dtos);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task GetSkills_ResponseContainsSkillIdAndNivelId()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, FakePersonaSkillServicio>();
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/personas/{ExistingPersonaId}/skills");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("skillId", out _), "Response JSON MUST include 'skillId'");
        Assert.True(first.TryGetProperty("nivelId", out _), "Response JSON MUST include 'nivelId'");
    }

    // ---- PUT /api/v1/personas/{personaId}/skills/{skillId} ----

    [Fact]
    public async Task PutSkill_ValidRequest_Returns200OkWithDto()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, FakePersonaSkillServicio>();
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = ExistingNivelId });

        var response = await client.PutAsync(
            $"/api/v1/personas/{ExistingPersonaId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsAsync<PersonaSkillDto>(response);
        Assert.Equal(ExistingSkillId, dto.SkillId);
        Assert.Equal(ExistingNivelId, dto.NivelId);
    }

    [Fact]
    public async Task PutSkill_InvalidNivelId_Returns400WithProblemDetails()
    {
        var fake = new FakePersonaSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                PersonaSkillCommandResult.Failure(
                    new PersonaSkillError(PersonaSkillErrorType.Validation, "NivelInvalido",
                        "El nivel de habilidad especificado no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio>(fake);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = Guid.NewGuid() });

        var response = await client.PutAsync(
            $"/api/v1/personas/{ExistingPersonaId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task PutSkill_NonExistentPersona_ReturnsNotFound()
    {
        var fake = new FakePersonaSkillServicio
        {
            UpsertHandler = (_, _, _, _) => Task.FromResult(
                PersonaSkillCommandResult.Failure(
                    new PersonaSkillError(PersonaSkillErrorType.NotFound, "PersonaNoEncontrada",
                        "La persona no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio>(fake);
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = ExistingNivelId });

        var response = await client.PutAsync(
            $"/api/v1/personas/{NonExistentPersonaId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    // ---- DELETE /api/v1/personas/{personaId}/skills/{skillId} ----

    [Fact]
    public async Task DeleteSkill_ExistingAssignment_Returns204NoContent()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, FakePersonaSkillServicio>();
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{ExistingPersonaId}/skills/{ExistingSkillId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_NonExistentAssignment_ReturnsNotFound()
    {
        var fake = new FakePersonaSkillServicio
        {
            DeleteHandler = (_, _, _) => Task.FromResult(
                PersonaSkillCommandResult.Failure(
                    new PersonaSkillError(PersonaSkillErrorType.NotFound, "AsignacionNoEncontrada",
                        "La asignación de habilidad no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio>(fake);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{ExistingPersonaId}/skills/{NonExistentSkillId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemDetailsAsync(response);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task DeleteSkill_NonExistentPersona_ReturnsNotFound()
    {
        var fake = new FakePersonaSkillServicio
        {
            DeleteHandler = (_, _, _) => Task.FromResult(
                PersonaSkillCommandResult.Failure(
                    new PersonaSkillError(PersonaSkillErrorType.NotFound, "PersonaNoEncontrada",
                        "La persona no existe.")))
        };
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio>(fake);
        });
        var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/v1/personas/{NonExistentPersonaId}/skills/{ExistingSkillId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Route isolation: must not mix with /api/v1/skills ----

    [Fact]
    public async Task PutSkill_DoesNotConflictWithSkillsCatalogRoute()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, FakePersonaSkillServicio>();
        });
        var client = factory.CreateClient();
        var body = ToJsonBody(new { nivelId = ExistingNivelId });

        var response = await client.PutAsync(
            $"/api/v1/personas/{ExistingPersonaId}/skills/{ExistingSkillId}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- No relationships in parent resource ----

    [Fact]
    public async Task GetPersonaSkills_IsSeparateFromPersonaDto()
    {
        using var factory = new ApiWebApplicationFactory(services =>
        {
            services.RemoveService<IPersonaSkillServicio>();
            services.AddSingleton<IPersonaSkillServicio, FakePersonaSkillServicio>();
        });
        var client = factory.CreateClient();

        // Parent resource should NOT include skills
        var parentResponse = await client.GetAsync($"/api/v1/personas/{ExistingPersonaId}");
        var parentJson = await parentResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("skillId", parentJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nivelId", parentJson, StringComparison.OrdinalIgnoreCase);

        // Subresource should include skills
        var subResponse = await client.GetAsync($"/api/v1/personas/{ExistingPersonaId}/skills");
        var subJson = await subResponse.Content.ReadAsStringAsync();
        Assert.Contains("skillId", subJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nivelId", subJson, StringComparison.OrdinalIgnoreCase);
    }
}
