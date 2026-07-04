using FluentValidation;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using SGV.Dominio.Organizacion;
using SGV.Tests.Aplicacion.Comun;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class CargoSkillServicioTests
{
    private static readonly Guid CargoIdValido = Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid CargoIdInexistente = Guid.Parse("81000000-0000-0000-0000-00000000FFFF");
    private static readonly Guid SkillIdValido = Guid.Parse("82000000-0000-0000-0000-000000000001");
    private static readonly Guid SkillIdInexistente = Guid.Parse("82000000-0000-0000-0000-00000000FFFF");
    private static readonly Guid NivelIdValido = Guid.Parse("83000000-0000-0000-0000-000000000001");
    private static readonly Guid NivelIdInexistente = Guid.Parse("83000000-0000-0000-0000-00000000FFFF");

    private static readonly Cargo CargoActivo = new("CARGO-001", "Cargo Test", Guid.NewGuid())
    {
        Id = CargoIdValido
    };

    private static readonly Habilidad HabilidadActiva = new("SKILL-01", "Habilidad Test")
    {
        Id = SkillIdValido
    };

    private static readonly NivelHabilidad NivelValido = new("N1", "Nivel 1", 1, 1)
    {
        Id = NivelIdValido
    };

    private static AsignarCargoSkillRequest CrearRequest(
        Guid? nivelRequeridoId = null,
        decimal? ponderacion = null,
        bool? esObligatoria = null)
        => new(nivelRequeridoId ?? NivelIdValido, ponderacion, esObligatoria);

    // ── UpsertAsync ────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(CargoIdValido, SkillIdValido, CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(SkillIdValido, resultado.Value!.SkillId);
        Assert.Equal(NivelIdValido, resultado.Value.NivelRequeridoId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_NivelIdInvalido_RetornaValidacionYSinGuardar()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(); // Empty → no levels
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(CargoIdValido, SkillIdValido, CrearRequest(NivelIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoSkillErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("NivelHabilidadNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_CargoInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(); // Empty → no cargo
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(CargoIdInexistente, SkillIdValido, CrearRequest(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoSkillErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("CargoNoEncontrado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_HabilidadInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(); // Empty → no habilidad
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(CargoIdValido, SkillIdInexistente, CrearRequest(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoSkillErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("HabilidadNoEncontrada", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── UpsertAsync — Defaults del vínculo (T1.1) ───────────────

    [Fact]
    public async Task UpsertAsync_SinPonderacionNiEsObligatoria_AplicaDefaultsYDevuelveDtoCompleto()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(
            CargoIdValido,
            SkillIdValido,
            CrearRequest(),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(SkillIdValido, resultado.Value!.SkillId);
        Assert.Equal(NivelIdValido, resultado.Value.NivelRequeridoId);
        Assert.Equal(1.00m, resultado.Value.Ponderacion);
        Assert.False(resultado.Value.EsObligatoria);
    }

    [Fact]
    public async Task UpsertAsync_RequestConPonderacionYEsObligatoria_PersisteYDevuelveValoresDelRequest()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(
            CargoIdValido,
            SkillIdValido,
            CrearRequest(ponderacion: 2.50m, esObligatoria: true),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(2.50m, resultado.Value!.Ponderacion);
        Assert.True(resultado.Value.EsObligatoria);
        Assert.Equal(NivelIdValido, resultado.Value.NivelRequeridoId);
        var persistido = Assert.Single(skillRepo.Datos);
        Assert.Equal(2.50m, persistido.Ponderacion);
        Assert.True(persistido.EsObligatoria);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(1.257)]
    public async Task UpsertAsync_PonderacionInvalida_RetornaFieldErrorsSinGuardar(decimal ponderacionInvalida)
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(
            CargoIdValido,
            SkillIdValido,
            CrearRequest(ponderacion: ponderacionInvalida),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoSkillErrorType.Validation, resultado.Error!.Type);
        Assert.NotNull(resultado.FieldErrors);
        Assert.NotEmpty(resultado.FieldErrors!);
        Assert.True(resultado.FieldErrors!.ContainsKey("ponderacion"));
        Assert.Empty(skillRepo.Datos);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_NivelRequeridoIdVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(
            CargoIdValido,
            SkillIdValido,
            new AsignarCargoSkillRequest(NivelRequeridoId: Guid.Empty),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoSkillErrorType.Validation, resultado.Error!.Type);
        Assert.NotNull(resultado.FieldErrors);
        Assert.True(resultado.FieldErrors!.ContainsKey("nivelRequeridoId"));
        Assert.Equal(0, cargoRepo.GetByIdForUpdateCallCount);
        Assert.Empty(skillRepo.Datos);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_AsociacionExistente_ReemplazaConValoresPersistidos()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var existing = new CargoHabilidad(CargoIdValido, SkillIdValido, NivelIdValido, 1.0m, false)
        {
            Id = Guid.NewGuid()
        };
        var skillRepo = new FakeCargoSkillRepository(existing);
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.UpsertAsync(
            CargoIdValido,
            SkillIdValido,
            CrearRequest(ponderacion: 3.75m, esObligatoria: true),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(3.75m, resultado.Value!.Ponderacion);
        Assert.True(resultado.Value.EsObligatoria);
        Assert.Equal(NivelIdValido, resultado.Value.NivelRequeridoId);
        Assert.Single(skillRepo.Datos);
        var persistido = skillRepo.Datos[0];
        Assert.Equal(3.75m, persistido.Ponderacion);
        Assert.True(persistido.EsObligatoria);
        Assert.NotEqual(existing.Id, persistido.Id);
        Assert.Equal(1, skillRepo.DeleteCallCount);
        Assert.Equal(1, skillRepo.AddCallCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task UpsertAsync_AsociacionExistente_MismoRequestEsIdempotente()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var existing = new CargoHabilidad(CargoIdValido, SkillIdValido, NivelIdValido, 2.5m, true)
        {
            Id = Guid.NewGuid()
        };
        var skillRepo = new FakeCargoSkillRepository(existing);
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var primerRequest = CrearRequest(ponderacion: 2.50m, esObligatoria: true);
        var primerResultado = await servicio.UpsertAsync(CargoIdValido, SkillIdValido, primerRequest, default);

        Assert.True(primerResultado.IsSuccess);
        Assert.Equal(2.50m, primerResultado.Value!.Ponderacion);
        Assert.True(primerResultado.Value.EsObligatoria);

        // Re-invocar con exactamente la misma carga debe dejar una única asociación activa.
        var segundaVez = await servicio.UpsertAsync(CargoIdValido, SkillIdValido, primerRequest, default);

        Assert.True(segundaVez.IsSuccess);
        Assert.Equal(2.50m, segundaVez.Value!.Ponderacion);
        Assert.True(segundaVez.Value.EsObligatoria);
        Assert.Single(skillRepo.Datos);
        Assert.Equal(2, uow.SaveChangesCount);
    }

    // ── DeleteAsync ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_AsociacionExistente_RetornaExitoYGuarda()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var existing = new CargoHabilidad(CargoIdValido, SkillIdValido, NivelIdValido, 1.0m, false)
        {
            Id = Guid.NewGuid()
        };
        var skillRepo = new FakeCargoSkillRepository(existing);
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.DeleteAsync(CargoIdValido, SkillIdValido, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, skillRepo.DeleteCallCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task DeleteAsync_AsociacionInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository(); // Empty → no association
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.DeleteAsync(CargoIdValido, SkillIdValido, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoSkillErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("AsociacionNoEncontrada", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ListAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_CargoConHabilidades_RetornaDetalleCompleto()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skill1 = new CargoHabilidad(CargoIdValido, SkillIdValido, NivelIdValido, 2.50m, true)
        {
            Id = Guid.NewGuid()
        };
        var skill2 = new CargoHabilidad(CargoIdValido, Guid.Parse("82000000-0000-0000-0000-000000000002"), NivelIdValido, 1.0m, false)
        {
            Id = Guid.NewGuid()
        };
        var skillRepo = new FakeCargoSkillRepository(skill1, skill2);
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.ListAsync(CargoIdValido, default);

        Assert.Equal(2, resultado.Count);
        var obligatorio = resultado.Single(d => d.Skill.Id == SkillIdValido);
        Assert.Equal(SkillIdValido, obligatorio.SkillId);
        Assert.Equal(NivelIdValido, obligatorio.NivelRequeridoId);
        Assert.Equal(2.50m, obligatorio.Ponderacion);
        Assert.True(obligatorio.EsObligatoria);
        Assert.NotNull(obligatorio.Skill);
        Assert.NotNull(obligatorio.Nivel);

        var opcional = resultado.Single(d => d.Skill.Id == Guid.Parse("82000000-0000-0000-0000-000000000002"));
        Assert.Equal(1.00m, opcional.Ponderacion);
        Assert.False(opcional.EsObligatoria);
    }

    [Fact]
    public async Task ListAsync_CargoSinHabilidades_RetornaVacio()
    {
        var cargoRepo = new FakeCargoReadRepositoryForSkills(CargoActivo);
        var habilidadRepo = new FakeHabilidadReadRepository(HabilidadActiva);
        var nivelRepo = new FakeNivelHabilidadRepo(NivelValido);
        var skillRepo = new FakeCargoSkillRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(cargoRepo, habilidadRepo, nivelRepo, skillRepo, uow);

        var resultado = await servicio.ListAsync(CargoIdValido, default);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static CargoSkillServicio CrearServicio(
        ICargoRepository cargoRepo,
        IHabilidadRepository habilidadRepo,
        INivelHabilidadRepository nivelRepo,
        ICargoSkillRepository skillRepo,
        IUnitOfWork uow)
    {
        var validator = new AsignarCargoSkillRequestValidator();
        return new CargoSkillServicio(
            cargoRepo,
            habilidadRepo,
            nivelRepo,
            skillRepo,
            uow,
            validator);
    }
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakeCargoReadRepositoryForSkills : ICargoRepository
{
    private readonly Cargo? _cargo;

    public FakeCargoReadRepositoryForSkills() { }

    public FakeCargoReadRepositoryForSkills(Cargo cargo)
    {
        _cargo = cargo;
    }

    public int GetByIdForUpdateCallCount { get; private set; }

    public Task<Cargo?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        var match = _cargo is not null && _cargo.Id == id && _cargo.IsActive ? _cargo : null;
        return Task.FromResult(match);
    }

    // Unused — minimal implementation
    public Task<Cargo?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_cargo?.Id == id ? _cargo : null);

    public Task<IReadOnlyList<Cargo>> ListAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Cargo>>(_cargo is null ? [] : [_cargo]);

    public Task AddAsync(Cargo cargo, CancellationToken ct = default) => Task.CompletedTask;
    public Task UpdateAsync(Cargo cargo, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task ReactivateAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    public Task<Cargo?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_cargo?.Id == id ? _cargo : null);
    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken ct = default)
        => Task.FromResult(false);
    public Task<bool> HasActivePuestosAsync(Guid cargoId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<(IReadOnlyList<Cargo> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        CargoSegmentoListado segmento = CargoSegmentoListado.Activas,
        CancellationToken ct = default)
        => Task.FromResult<(IReadOnlyList<Cargo>, int)>(([], 0));
}

internal sealed class FakeCargoSkillRepository : ICargoSkillRepository
{
    public List<CargoHabilidad> Datos { get; set; }

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public int GetByIdCallCount { get; private set; }
    public int ListAllCallCount { get; private set; }
    public int ListByCargoIdCallCount { get; private set; }
    public int GetByCargoAndSkillCallCount { get; private set; }

    public FakeCargoSkillRepository(params CargoHabilidad[] datos)
    {
        Datos = [.. datos];
    }

    public Task<IReadOnlyList<CargoHabilidad>> ListByCargoIdAsync(Guid cargoId, CancellationToken ct = default)
    {
        ListByCargoIdCallCount++;
        return Task.FromResult<IReadOnlyList<CargoHabilidad>>(
            Datos.Where(d => d.CargoId == cargoId).ToList());
    }

    public Task<IReadOnlyList<CargoSkillDetailDto>> ListDetailedByCargoIdAsync(
        Guid cargoId, CancellationToken ct = default)
    {
        var items = Datos.Where(d => d.CargoId == cargoId).ToList();
        return Task.FromResult<IReadOnlyList<CargoSkillDetailDto>>(
            items.Select(a => new CargoSkillDetailDto(
                new HabilidadDto(a.HabilidadId, "COD", "Nombre", null, null),
                new NivelHabilidadDto(a.NivelRequeridoId, "N1", "Nivel", 1, 1))
            {
                SkillId = a.HabilidadId,
                NivelRequeridoId = a.NivelRequeridoId,
                Ponderacion = a.Ponderacion,
                EsObligatoria = a.EsObligatoria,
            })
            .ToList());
    }

    public Task<CargoHabilidad?> GetByCargoAndSkillAsync(Guid cargoId, Guid skillId, CancellationToken ct = default)
    {
        GetByCargoAndSkillCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.CargoId == cargoId && d.HabilidadId == skillId));
    }

    public Task AddAsync(CargoHabilidad ch, CancellationToken ct = default)
    {
        AddCallCount++;
        Datos.Add(ch);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CargoHabilidad ch, CancellationToken ct = default)
    {
        UpdateCallCount++;
        var idx = Datos.FindIndex(d => d.Id == ch.Id);
        if (idx >= 0) Datos[idx] = ch;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CargoHabilidad ch, CancellationToken ct = default)
    {
        DeleteCallCount++;
        Datos.Remove(ch);
        return Task.CompletedTask;
    }

    public Task<CargoHabilidad?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        GetByIdCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id));
    }

    public Task<IReadOnlyList<CargoHabilidad>> ListAllAsync(CancellationToken ct = default)
    {
        ListAllCallCount++;
        return Task.FromResult<IReadOnlyList<CargoHabilidad>>(Datos.ToList());
    }
}

// FakeUnitOfWork, FakeHabilidadReadRepository, FakeNivelHabilidadRepo moved to SGV.Tests.Aplicacion.Comun.TestFakes