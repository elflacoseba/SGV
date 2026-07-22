using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

public sealed class SkillPersonaServicioConsultaTests
{
    private static readonly Guid SkillId = Guid.NewGuid();
    private static readonly HabilidadPersonasListQuery Query = new(1, 20, null, "apellidos_asc", PersonaSegmentoListado.Activas);

    [Fact]
    public async Task ListarPersonasAsync_WithEmptySkillId_ThrowsArgumentException()
    {
        var service = new SkillPersonaServicioConsulta(new FakeRepository(), new FakeHabilidadServicio(true));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListarPersonasAsync(Guid.Empty, Query));
    }

    [Fact]
    public async Task ListarPersonasAsync_WithNonExistentSkill_ReturnsNull()
    {
        var service = new SkillPersonaServicioConsulta(new FakeRepository(), new FakeHabilidadServicio(false));
        var result = await service.ListarPersonasAsync(SkillId, Query);
        Assert.Null(result);
    }

    [Fact]
    public async Task ListarPersonasAsync_WithValidSkill_ReturnsRepositoryPageAndForwardsQuery()
    {
        var repository = new FakeRepository();
        var service = new SkillPersonaServicioConsulta(repository, new FakeHabilidadServicio(true));
        var result = await service.ListarPersonasAsync(SkillId, Query);

        Assert.NotNull(result);
        Assert.Equal(7, result.Total);
        Assert.Same(Query, repository.LastQuery);
        Assert.Equal(SkillId, repository.LastSkillId);
    }

    private sealed class FakeRepository : ISkillPersonaRepository
    {
        public Guid LastSkillId { get; private set; }
        public HabilidadPersonasListQuery? LastQuery { get; private set; }

        public Task<PersonaHabilidadesPageResult> ListDetailedBySkillIdAsync(Guid skillId, HabilidadPersonasListQuery query, CancellationToken cancellationToken = default)
        {
            LastSkillId = skillId;
            LastQuery = query;
            return Task.FromResult(new PersonaHabilidadesPageResult([], query.Page, query.PageSize, 7, query.Sort, query.Segmento));
        }
    }

    private sealed class FakeHabilidadServicio(bool exists) : IHabilidadServicioConsulta
    {
        public Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(exists ? new HabilidadDto(id, "SK", "Skill", null, null) : null);

        public Task<IReadOnlyList<HabilidadDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HabilidadDto>>([]);

        public Task<PagedResult<HabilidadDto>> QueryAsync(HabilidadListQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<HabilidadDto>([], 0, query.Page, query.PageSize));
    }
}
