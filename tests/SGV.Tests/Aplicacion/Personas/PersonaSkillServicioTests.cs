using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using SGV.Dominio.Personas;
using SGV.Tests.Aplicacion.Comun;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

public sealed class PersonaSkillServicioTests
{
    private static readonly Guid PersonaIdValida = Guid.Parse("61000000-0000-0000-0000-000000000001");
    private static readonly Guid PersonaIdInexistente = Guid.Parse("61000000-0000-0000-0000-00000000FFFF");
    private static readonly Guid SkillIdValido = Guid.Parse("62000000-0000-0000-0000-000000000001");
    private static readonly Guid SkillIdInexistente = Guid.Parse("62000000-0000-0000-0000-00000000FFFF");
    private static readonly Guid NivelIdValido = Guid.Parse("63000000-0000-0000-0000-000000000001");
    private static readonly Guid NivelIdInexistente = Guid.Parse("63000000-0000-0000-0000-00000000FFFF");

    private static readonly Persona PersonaActiva = new("Juan", "Pérez", "LEG-001", "juan@test.com")
    {
        Id = PersonaIdValida
    };

    private static readonly Habilidad HabilidadActiva = new("SKILL-01", "Habilidad Test")
    {
        Id = SkillIdValido
    };

    private static readonly NivelHabilidad NivelValido = new("N1", "Nivel 1", 1, 1)
    {
        Id = NivelIdValido
    };

    private static AsignarPersonaSkillRequest CrearRequest(Guid? nivelId = null)
        => new(nivelId ?? NivelIdValido);

    // ── UpsertAsync ────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var personaRepo = new FakePersonaReadRepository(PersonaActiva);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakePersonaSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(PersonaIdValida, SkillIdValido, CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(SkillIdValido, resultado.Value!.SkillId);
        Assert.Equal(NivelIdValido, resultado.Value.NivelId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_NivelIdInvalido_RetornaValidacionYSinGuardar()
    {
        var personaRepo = new FakePersonaReadRepository(PersonaActiva);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(); // Empty → no levels
        var skillRepo = new FakePersonaSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(PersonaIdValida, SkillIdValido, CrearRequest(NivelIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaSkillErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("NivelHabilidadNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_PersonaInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var personaRepo = new FakePersonaReadRepository(); // Empty → no persona
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakePersonaSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(PersonaIdInexistente, SkillIdValido, CrearRequest(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaSkillErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("PersonaNoEncontrada", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_HabilidadInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var personaRepo = new FakePersonaReadRepository(PersonaActiva);
        var habilidadRepo = new FakeHabilidadReadRepository(); // Empty → no habilidad
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakePersonaSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(PersonaIdValida, SkillIdInexistente, CrearRequest(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaSkillErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("HabilidadNoEncontrada", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── DeleteAsync ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_AsociacionExistente_RetornaExitoYGuarda()
    {
        var personaRepo = new FakePersonaReadRepository(PersonaActiva);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var existing = new PersonaHabilidad(PersonaIdValida, SkillIdValido, NivelIdValido)
        {
            Id = Guid.NewGuid()
        };
        var skillRepo = new FakePersonaSkillRepository(existing);
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.DeleteAsync(PersonaIdValida, SkillIdValido, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, skillRepo.DeleteCallCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task DeleteAsync_AsociacionInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var personaRepo = new FakePersonaReadRepository(PersonaActiva);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakePersonaSkillRepository(); // Empty → no association
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.DeleteAsync(PersonaIdValida, SkillIdValido, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaSkillErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("AsociacionNoEncontrada", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ListAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_PersonaConHabilidades_RetornaDetalleCompleto()
    {
        var personaRepo = new FakePersonaReadRepository(PersonaActiva);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skill1 = new PersonaHabilidad(PersonaIdValida, SkillIdValido, NivelIdValido)
        {
            Id = Guid.NewGuid()
        };
        var skill2 = new PersonaHabilidad(PersonaIdValida, Guid.Parse("62000000-0000-0000-0000-000000000002"), NivelIdValido)
        {
            Id = Guid.NewGuid()
        };
        var skillRepo = new FakePersonaSkillRepository(skill1, skill2);
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.ListAsync(PersonaIdValida, default);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, d => d.SkillId == SkillIdValido);
        Assert.Contains(resultado, d => d.SkillId == Guid.Parse("62000000-0000-0000-0000-000000000002"));
        Assert.All(resultado, d =>
        {
            Assert.NotNull(d.Skill);
            Assert.NotNull(d.Nivel);
        });
    }

    [Fact]
    public async Task ListAsync_PersonaSinHabilidades_RetornaVacio()
    {
        var personaRepo = new FakePersonaReadRepository(PersonaActiva);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakePersonaSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.ListAsync(PersonaIdValida, default);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static PersonaSkillServicio CrearServicio(
        IPersonaRepository personaRepo,
        IHabilidadRepository habilidadRepo,
        INivelHabilidadRepository nivelRepo,
        IPersonaSkillRepository skillRepo,
        IUnitOfWork uow)
    {
        return new PersonaSkillServicio(personaRepo, habilidadRepo, nivelRepo, skillRepo, uow);
    }
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakePersonaReadRepository : IPersonaRepository
{
    private readonly Persona? _persona;

    public FakePersonaReadRepository() { }

    public FakePersonaReadRepository(Persona persona)
    {
        _persona = persona;
    }

    public int GetByIdForUpdateCallCount { get; private set; }

    public Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        var match = _persona is not null && _persona.Id == id && _persona.IsActive ? _persona : null;
        return Task.FromResult(match);
    }

    public Task<Persona?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_persona?.Id == id ? _persona : null);

    public Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Persona>>(_persona is null ? [] : [_persona]);

    public Task AddAsync(Persona p, CancellationToken ct = default) => Task.CompletedTask;
    public Task UpdateAsync(Persona p, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task ReactivateAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_persona?.Id == id ? _persona : null);
    public Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? id = null, CancellationToken ct = default)
        => Task.FromResult(false);
    public Task<bool> ExistsActiveEmailAsync(string email, Guid? id = null, CancellationToken ct = default)
        => Task.FromResult(false);
    public Task<bool> ExistsActiveDocumentoAsync(string tipo, string num, Guid? id = null, CancellationToken ct = default)
        => Task.FromResult(false);
}


internal sealed class FakePersonaSkillRepository : IPersonaSkillRepository
{
    public List<PersonaHabilidad> Datos { get; set; }

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public int GetByIdCallCount { get; private set; }
    public int ListAllCallCount { get; private set; }
    public int ListByPersonaIdCallCount { get; private set; }
    public int GetByPersonaAndSkillCallCount { get; private set; }

    public FakePersonaSkillRepository(params PersonaHabilidad[] datos)
    {
        Datos = [.. datos];
    }

    public Task<IReadOnlyList<PersonaHabilidad>> ListByPersonaIdAsync(Guid personaId, CancellationToken ct = default)
    {
        ListByPersonaIdCallCount++;
        return Task.FromResult<IReadOnlyList<PersonaHabilidad>>(
            Datos.Where(d => d.PersonaId == personaId).ToList());
    }

    public Task<IReadOnlyList<PersonaSkillDetailDto>> ListDetailedByPersonaIdAsync(
        Guid personaId, CancellationToken ct = default)
    {
        var items = Datos.Where(d => d.PersonaId == personaId).ToList();
        return Task.FromResult<IReadOnlyList<PersonaSkillDetailDto>>(
            items.Select(a => new PersonaSkillDetailDto(
                a.HabilidadId,
                a.NivelHabilidadId,
                new HabilidadDto(a.HabilidadId, "COD", "Nombre", null, null),
                new NivelHabilidadDto(a.NivelHabilidadId, "N1", "Nivel", 1, 1)))
            .ToList());
    }

    public Task<PersonaHabilidad?> GetByPersonaAndSkillAsync(Guid personaId, Guid skillId, CancellationToken ct = default)
    {
        GetByPersonaAndSkillCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.PersonaId == personaId && d.HabilidadId == skillId));
    }

    public Task AddAsync(PersonaHabilidad ph, CancellationToken ct = default)
    {
        AddCallCount++;
        Datos.Add(ph);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PersonaHabilidad ph, CancellationToken ct = default)
    {
        UpdateCallCount++;
        var idx = Datos.FindIndex(d => d.Id == ph.Id);
        if (idx >= 0) Datos[idx] = ph;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PersonaHabilidad ph, CancellationToken ct = default)
    {
        DeleteCallCount++;
        Datos.Remove(ph);
        return Task.CompletedTask;
    }

    public Task<PersonaHabilidad?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        GetByIdCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id));
    }

    public Task<IReadOnlyList<PersonaHabilidad>> ListAllAsync(CancellationToken ct = default)
    {
        ListAllCallCount++;
        return Task.FromResult<IReadOnlyList<PersonaHabilidad>>(Datos.ToList());
    }
}

// FakeUnitOfWork, FakeHabilidadReadRepository, FakeNivelHabilidadRepo moved to SGV.Tests.Aplicacion.Comun.TestFakes
