using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Api;

[Collection("ApiIntegration")]
public sealed class HabilidadesPersonasControllerTests(ApiIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetPersonas_Anonymous_Returns401()
    {
        await using var factory = CreateFactory(new FakeService());
        var response = await factory.CreateClient().GetAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/personas");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPersonas_Authenticated_Returns200AndExpectedEnvelope()
    {
        await using var factory = CreateFactory(new FakeService());
        var response = await factory.CreateAdminClient().GetAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/personas");
        var result = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetPersonas_WithPagination_ReturnsCorrectPage()
    {
        await using var factory = CreateFactory(new FakeService());
        var response = await factory.CreateAdminClient().GetAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/personas?page=2&pageSize=2");
        var result = await ReadAsync(response);
        Assert.Single(result.Items);
        Assert.Equal("L-003", result.Items[0].Persona.Legajo);
    }

    [Fact]
    public async Task GetPersonas_WithSearch_ReturnsMatching()
    {
        await using var factory = CreateFactory(new FakeService());
        var response = await factory.CreateAdminClient().GetAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/personas?search=L-002");
        var result = await ReadAsync(response);
        Assert.Single(result.Items);
        Assert.Equal("L-002", result.Items[0].Persona.Legajo);
    }

    [Fact]
    public async Task GetPersonas_WithSort_ReturnsOrdered()
    {
        await using var factory = CreateFactory(new FakeService());
        var response = await factory.CreateAdminClient().GetAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/personas?sort=apellidos_desc");
        var result = await ReadAsync(response);
        Assert.Equal(["Zulu", "Mora", "Alba"], result.Items.Select(x => x.Persona.Apellidos).ToArray());
    }

    [Fact]
    public async Task GetPersonas_WithStatusEliminadas_ReturnsOnlyDeleted()
    {
        await using var factory = CreateFactory(new FakeService());
        var response = await factory.CreateAdminClient().GetAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/personas?status=eliminadas");
        var result = await ReadAsync(response);
        Assert.Single(result.Items);
        Assert.False(result.Items[0].Persona.IsActive);
        Assert.Equal(PersonaSegmentoListado.Eliminadas, result.Segmento);
    }

    [Fact]
    public async Task GetPersonas_WithNonExistentSkillId_Returns404()
    {
        await using var factory = CreateFactory(new FakeService());
        var response = await factory.CreateAdminClient().GetAsync($"/api/v1/skills/{Guid.NewGuid()}/personas");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPersonas_NormalizesInvalidBoundariesAndSort()
    {
        var fake = new FakeService();
        await using var factory = CreateFactory(fake);
        var response = await factory.CreateAdminClient().GetAsync($"/api/v1/skills/{FakeHabilidadServicio.HabilidadId1}/personas?page=0&pageSize=500&sort=unknown");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, fake.LastQuery!.Page);
        Assert.Equal(100, fake.LastQuery.PageSize);
        Assert.Equal("apellidos_asc", fake.LastQuery.Sort);
    }

    private ApiWebApplicationFactory CreateFactory(FakeService fake) => fixture.RootFactory.WithOverrides(services =>
    {
        services.RemoveService<ISkillPersonaServicioConsulta>();
        services.AddSingleton<ISkillPersonaServicioConsulta>(fake);
    });

    private static async Task<PersonaHabilidadesPageResult> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<PersonaHabilidadesPageResult>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

    private sealed class FakeService : ISkillPersonaServicioConsulta
    {
        public HabilidadPersonasListQuery? LastQuery { get; private set; }

        public Task<PersonaHabilidadesPageResult?> ListarPersonasAsync(Guid skillId, HabilidadPersonasListQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            if (skillId != FakeHabilidadServicio.HabilidadId1)
            {
                return Task.FromResult<PersonaHabilidadesPageResult?>(null);
            }

            var active = new[] { Make("L-001", "Alba", true), Make("L-002", "Mora", true), Make("L-003", "Zulu", true) };
            var source = query.Segmento == PersonaSegmentoListado.Eliminadas ? new[] { Make("D-001", "Baja", false) } : active;
            var filtered = string.IsNullOrWhiteSpace(query.Search) ? source : source.Where(x => x.Persona.Legajo!.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            var ordered = query.Sort == "apellidos_desc" ? filtered.OrderByDescending(x => x.Persona.Apellidos) : filtered.OrderBy(x => x.Persona.Apellidos);
            var list = ordered.ToArray();
            var items = list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();
            return Task.FromResult<PersonaHabilidadesPageResult?>(new(items, query.Page, query.PageSize, list.Length, query.Sort, query.Segmento));
        }

        private static SkillPersonaDetailDto Make(string legajo, string apellidos, bool active)
        {
            var personaId = Guid.NewGuid();
            var nivelId = Guid.NewGuid();
            return new(new PersonaDto(personaId, legajo, "Nombre", apellidos, null, null, null, null, null, null, active), new(nivelId, "BASICO", "Básico", 1, 1))
            { PersonaId = personaId, HabilidadId = FakeHabilidadServicio.HabilidadId1, NivelHabilidadId = nivelId };
        }
    }
}
