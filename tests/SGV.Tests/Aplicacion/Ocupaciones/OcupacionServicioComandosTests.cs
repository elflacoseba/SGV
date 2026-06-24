using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Aplicacion.Ocupaciones;

public sealed class OcupacionServicioComandosTests
{
    private static readonly Guid PersonaIdActiva = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid PersonaIdInactiva = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid PersonaIdInexistente = Guid.Parse("70000000-0000-0000-0000-000000000099");

    private static readonly Guid PuestoIdActivo = Guid.Parse("70000000-0000-0000-0000-000000000101");
    private static readonly Guid PuestoIdInactivo = Guid.Parse("70000000-0000-0000-0000-000000000102");
    private static readonly Guid PuestoIdInexistente = Guid.Parse("70000000-0000-0000-0000-000000000199");

    private static readonly Guid OcupacionIdActiva = Guid.Parse("70000000-0000-0000-0000-000000000201");
    private static readonly Guid OcupacionIdFinalizada = Guid.Parse("70000000-0000-0000-0000-000000000202");
    private static readonly Guid OcupacionIdEliminada = Guid.Parse("70000000-0000-0000-0000-000000000203");
    private static readonly Guid OcupacionIdInexistente = Guid.Parse("70000000-0000-0000-0000-000000000299");

    private static CrearOcupacionRequest CrearRequest(
        Guid? personaId = null,
        Guid? puestoId = null) => new(
        PersonaId: personaId ?? PersonaIdActiva,
        PuestoId: puestoId ?? PuestoIdActivo,
        FechaInicio: new DateOnly(2025, 1, 1),
        TipoAsignacion: TipoAsignacion.Permanente,
        Observaciones: null);

    // ── CrearAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(PersonaIdActiva, resultado.Value!.PersonaId);
        Assert.Equal(PuestoIdActivo, resultado.Value.PuestoId);
        Assert.Equal("Activo", resultado.Value.Estado);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PersonaInexistente_Retorna404()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(personaId: PersonaIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PersonaInactiva_Retorna409()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaInactiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(personaId: PersonaIdInactiva), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PuestoInexistente_Retorna404()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(puestoId: PuestoIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PuestoInactivo_Retorna409()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoInactivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(puestoId: PuestoIdInactivo), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PuestoUnicoConflictivo_Retorna409()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var existente = CrearOcupacionActiva(PuestoIdActivo, PersonaIdInactiva);
        ocupacionRepo.Datos.Add(existente);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PersonaYPuestoUnicoConflictivo_Retorna409()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var existente = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva);
        ocupacionRepo.Datos.Add(existente);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ActualizarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ActualizarAsync_Activo_RetornaDtoActualizadoYGuarda()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), TipoAsignacion.Temporal, "Actualizado"),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(new DateOnly(2025, 6, 1), resultado.Value!.FechaInicio);
        Assert.Equal(TipoAsignacion.Temporal, resultado.Value.TipoAsignacion);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_Inexistente_Retorna404()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            OcupacionIdInexistente,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), TipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_Finalizada_Retorna409()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdActivo, PersonaIdActiva, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), TipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_Eliminada_Retorna409()
    {
        var ocupacion = CrearOcupacionEliminada(PuestoIdActivo, PersonaIdActiva, OcupacionIdEliminada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), TipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── FinalizarAsync ──────────────────────────────────────────

    [Fact]
    public async Task FinalizarAsync_Activo_RetornaExitoYGuarda()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.FinalizarAsync(
            ocupacion.Id,
            new FinalizarOcupacionRequest(new DateOnly(2025, 12, 31)),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("Finalizado", resultado.Value!.Estado);
        Assert.Equal(new DateOnly(2025, 12, 31), resultado.Value.FechaFin);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task FinalizarAsync_Inexistente_Retorna404()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.FinalizarAsync(
            OcupacionIdInexistente,
            new FinalizarOcupacionRequest(new DateOnly(2025, 12, 31)),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task FinalizarAsync_YaFinalizada_Retorna409()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdActivo, PersonaIdActiva, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.FinalizarAsync(
            ocupacion.Id,
            new FinalizarOcupacionRequest(new DateOnly(2025, 12, 31)),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── EliminarAsync ───────────────────────────────────────────

    [Fact]
    public async Task EliminarAsync_Activo_RetornaExitoYGuarda()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.EliminarAsync(ocupacion.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task EliminarAsync_Inexistente_Retorna404()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.EliminarAsync(OcupacionIdInexistente, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task EliminarAsync_YaEliminada_Retorna409()
    {
        var ocupacion = CrearOcupacionEliminada(PuestoIdActivo, PersonaIdActiva, OcupacionIdEliminada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.EliminarAsync(ocupacion.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ReactivarAsync ──────────────────────────────────────────

    [Fact]
    public async Task ReactivarAsync_DesdeFinalizado_RetornaExitoYGuarda()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdActivo, PersonaIdActiva, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("Activo", resultado.Value!.Estado);
        Assert.Null(resultado.Value.FechaFin);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_DesdeEliminado_RetornaExitoYGuarda()
    {
        var ocupacion = CrearOcupacionEliminada(PuestoIdActivo, PersonaIdActiva, OcupacionIdEliminada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("Activo", resultado.Value!.Estado);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_Inexistente_Retorna404()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(OcupacionIdInexistente, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_PuestoConflictivo_Retorna409()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdActivo, PersonaIdActiva, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var conflictiva = CrearOcupacionActiva(PuestoIdActivo, PersonaIdInactiva, Guid.NewGuid());
        ocupacionRepo.Datos.Add(conflictiva);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_YaActiva_Retorna409()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(OcupacionErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static OcupacionServicioComandos CrearServicio(
        IOcupacionRepository ocupacionRepo,
        IPersonaRepository personaRepo,
        IPuestoRepository puestoRepo,
        IUnitOfWork uow)
    {
        return new OcupacionServicioComandos(ocupacionRepo, personaRepo, puestoRepo, uow);
    }

    private static Persona CrearPersonaActiva()
    {
        return new Persona("Juan", "Pérez", "LEG-OCP-001", "juan@ocupacion.com")
        {
            Id = PersonaIdActiva
        };
    }

    private static Persona CrearPersonaInactiva()
    {
        var p = new Persona("Ana", "García", "LEG-OCP-002", "ana@ocupacion.com")
        {
            Id = PersonaIdInactiva
        };
        p.Desactivar();
        return p;
    }

    private static Puesto CrearPuestoActivo()
    {
        return new Puesto(Guid.NewGuid(), Guid.NewGuid(), "PUESTO-001", "Puesto Activo")
        {
            Id = PuestoIdActivo
        };
    }

    private static Puesto CrearPuestoInactivo()
    {
        var p = new Puesto(Guid.NewGuid(), Guid.NewGuid(), "PUESTO-002", "Puesto Inactivo")
        {
            Id = PuestoIdInactivo
        };
        p.Desactivar();
        return p;
    }

    private static Ocupacion CrearOcupacionActiva(Guid puestoId, Guid personaId, Guid? id = null)
    {
        return new Ocupacion(personaId, puestoId, new DateOnly(2025, 1, 1), TipoAsignacion.Permanente)
        {
            Id = id ?? Guid.NewGuid()
        };
    }

    private static Ocupacion CrearOcupacionFinalizada(Guid puestoId, Guid personaId, Guid? id = null)
    {
        var o = CrearOcupacionActiva(puestoId, personaId, id);
        o.Finalizar(new DateOnly(2025, 6, 30));
        return o;
    }

    private static Ocupacion CrearOcupacionEliminada(Guid puestoId, Guid personaId, Guid? id = null)
    {
        var o = CrearOcupacionActiva(puestoId, personaId, id);
        o.EliminarLogicamente();
        return o;
    }
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakeOcupacionWriteRepository : IOcupacionRepository
{
    public List<Ocupacion> Datos { get; set; } = [];

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int GetByIdForUpdateCallCount { get; private set; }
    public int GetByIdIncludingHistoryCallCount { get; private set; }
    public int ExistsActiveByPuestoCallCount { get; private set; }
    public int ExistsActiveByPersonaYPuestoCallCount { get; private set; }

    public Task AddAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Datos.Add(ocupacion);
        return Task.CompletedTask;
    }

    public Task<Ocupacion?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(o => o.Id == id && o.EsVigente));
    }

    public Task<Ocupacion?> GetByIdIncludingHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdIncludingHistoryCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(o => o.Id == id));
    }

    public Task<bool> ExistsActiveByPuestoAsync(Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveByPuestoCallCount++;
        var exists = Datos.Any(o =>
            o.PuestoId == puestoId &&
            o.EsVigente &&
            o.Id != excludingId);
        return Task.FromResult(exists);
    }

    public Task<bool> ExistsActiveByPersonaYPuestoAsync(Guid personaId, Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveByPersonaYPuestoCallCount++;
        var exists = Datos.Any(o =>
            o.PersonaId == personaId &&
            o.PuestoId == puestoId &&
            o.EsVigente &&
            o.Id != excludingId);
        return Task.FromResult(exists);
    }

    public Task UpdateAsync(Ocupacion ocupacion, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var index = Datos.FindIndex(o => o.Id == ocupacion.Id);
        if (index >= 0)
        {
            Datos[index] = ocupacion;
        }
        return Task.CompletedTask;
    }

    public Task<Ocupacion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(o => o.Id == id && o.EsVigente));
    }

    public Task<IReadOnlyList<Ocupacion>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Ocupacion>>(Datos.Where(o => o.EsVigente).ToList());
    }

    public Task<IReadOnlyList<Ocupacion>> ListAllIncludingHistoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Ocupacion>>(Datos.ToList());
    }
}

internal sealed class FakePersonaWriteRepository : IPersonaRepository
{
    public List<Persona> Datos { get; set; } = [];

    public Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(p => p.Id == id));
    }

    public Task<Persona?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(p => p.Id == id && p.IsActive));
    }

    public Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Persona>>(Datos.Where(p => p.IsActive).ToList());

    public Task AddAsync(Persona persona, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task UpdateAsync(Persona persona, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task<bool> ExistsActiveEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task<bool> ExistsActiveDocumentoAsync(string tipoDocumento, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");
}

internal sealed class FakePuestoWriteRepository : IPuestoRepository
{
    public List<Puesto> Datos { get; set; } = [];

    public Task<Puesto?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(p => p.Id == id));
    }

    public Task<Puesto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(p => p.Id == id && p.IsActive));
    }

    public Task<IReadOnlyList<Puesto>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Puesto>>(Datos.Where(p => p.IsActive).ToList());

    public Task AddAsync(Puesto puesto, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task<Puesto?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task UpdateAsync(Puesto puesto, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.FromResult(1);
    }
}
