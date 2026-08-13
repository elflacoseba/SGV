using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Ocupaciones.Comandos;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Ocupaciones.Enums;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Personas;
using SGV.Dominio.Vacantes;
using Xunit;

// Acceso al FakeEstadoVacanteRepository compartido (definido en VacanteServicioComandosTests).
using SGV.Tests.Aplicacion.Vacantes;

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
        TipoAsignacion: OcupacionTipoAsignacion.Permanente,
        Observaciones: null);

    // ── CrearAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        // T-3.3 (adaptación a N3): ahora el helper por default inyecta una
        // Vacante abierta para PuestoIdActivo, satisfaciendo el check N3.
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(PersonaIdActiva, resultado.Value!.PersonaId);
        Assert.Equal(PuestoIdActivo, resultado.Value.PuestoId);
        Assert.Equal(OcupacionEstado.Vigente, resultado.Value.Estado);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // ── N3 (T-3.2): CrearOcupacion directo rechaza sin Vacante abierta ──

    [Fact]
    public async Task CrearAsync_PuestoSinVacanteAbierta_DevuelveConflictoPuestoSinVacanteAbierta()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var vacanteRepo = new FakeVacanteLookupRepository(); // sin vacantes abiertas
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, vacanteRepo);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(OcupacionErrorCodigo.PuestoSinVacanteAbierta, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PuestoConVacanteAbierta_Exito()
    {
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var vacanteRepo = new FakeVacanteLookupRepository { PuestosConVacanteAbierta = [PuestoIdActivo] };
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, vacanteRepo);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
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
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
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
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal, "Actualizado"),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(new DateOnly(2025, 6, 1), resultado.Value!.FechaInicio);
        Assert.Equal(OcupacionTipoAsignacion.Temporal, resultado.Value.TipoAsignacion);
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
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
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
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
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
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // Issue 9: Missing tests for ActualizarAsync reference validation.

    [Fact]
    public async Task ActualizarAsync_PersonaInexistente_Retorna404()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdInexistente, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PersonaInactiva_Retorna409()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaInactiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdInactiva, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoInexistente_Retorna404()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdInexistente, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoInactivo_Retorna409()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoInactivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdInactivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoOcupado_Retorna409()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        // Another active occupation for the same puesto with a different persona.
        var otra = CrearOcupacionActiva(PuestoIdActivo, PersonaIdInactiva, Guid.NewGuid());
        ocupacionRepo.Datos.Add(otra);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal("PuestoOcupado", resultado.Error!.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PersonaYPuestoOcupados_Retorna409()
    {
        var ocupacion = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, OcupacionIdActiva);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        // Another active occupation for the same persona+puesto (different id).
        var otra = CrearOcupacionActiva(PuestoIdActivo, PersonaIdActiva, Guid.NewGuid());
        ocupacionRepo.Datos.Add(otra);

        var resultado = await servicio.ActualizarAsync(
            ocupacion.Id,
            new ActualizarOcupacionRequest(PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 6, 1), OcupacionTipoAsignacion.Temporal),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal("PersonaYPuestoOcupados", resultado.Error!.Code);
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
        Assert.Equal(OcupacionEstado.Finalizada, resultado.Value!.Estado);
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
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    /// <summary>
    /// Q1 (Finalizar Ocupación con Vacante Cubierta NO reabre la Vacante):
    /// tras Cubrir, la Vacante queda Cubierta y la Ocupación derivada
    /// lleva <c>VacanteId</c> seteado. Al Finalizar la Ocupación, la
    /// Vacante debe permanecer Cubierta (no se reabre ni se cambia).
    /// Verifica que el flujo de Finalizar no consulta ni muta Vacante.
    /// </summary>
    [Fact]
    public async Task Finalizar_VacanteCubiertaOrigen_NoReabreVacante()
    {
        var vacanteId = Guid.Parse("70000000-0000-0000-0000-000000000505");
        var estadoCubierta = new EstadoVacante("Cubierta", "Cubierta", 3, true, esCubierta: true)
        {
            Id = Guid.Parse("70000000-0000-0000-0000-000000000506")
        };
        var vacanteCubierta = new Vacante(
            PuestoIdActivo,
            estadoCubierta.Id,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "Motivo")
        {
            Id = vacanteId
        };
        vacanteCubierta.WithEstadoVacante(estadoCubierta);

        var ocupacionDerivada = new Ocupacion(
            PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente, vacanteId: vacanteId);
        // Activa por default.

        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacionDerivada] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        // Setear VacanteRepository para que N3 (ExistsAbiertaByPuestoAsync)
        // devuelva false y permita el flujo de Finalizar.
        var vacanteRepo = new FakeVacanteLookupRepository
        {
            VacantesPorId = { [vacanteId] = vacanteCubierta }
        };

        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, vacanteRepo);

        var resultado = await servicio.FinalizarAsync(
            ocupacionDerivada.Id,
            new FinalizarOcupacionRequest(new DateOnly(2025, 6, 30)),
            default);

        // Finalizar debe tener éxito sin tocar Vacante.
        Assert.True(resultado.IsSuccess);
        // La Vacante sigue Cubierta: el fake no recibió CambiarEstado ni UpdateAsync.
        Assert.Equal(estadoCubierta.Id, vacanteCubierta.EstadoVacanteId);
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
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
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
        Assert.Equal(OcupacionEstado.Vigente, resultado.Value!.Estado);
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
        Assert.Equal(OcupacionEstado.Vigente, resultado.Value!.Estado);
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
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
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
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Q2 (T-5.1): Reactivar Ocupacion rechaza Vacante Cancelada ──

    [Fact]
    public async Task ReactivarAsync_VacanteCancelada_DevuelveConflictoVacanteCancelada()
    {
        var vacanteId = Guid.Parse("70000000-0000-0000-0000-000000000501");
        var ocupacionConVacante = new Ocupacion(
            PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente, vacanteId: vacanteId);
        ocupacionConVacante.Finalizar(new DateOnly(2025, 6, 30));

        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacionConVacante] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();

        // Vacante con su EstadoVacante poblado con EsCancelada=true.
        var estadoCancelado = new EstadoVacante("Cancelada", "Cancelada", 4, true, esCancelada: true)
        {
            Id = Guid.Parse("70000000-0000-0000-0000-000000000502")
        };
        var vacanteCancelada = new Vacante(
            PuestoIdActivo, estadoCancelado.Id,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "Motivo").WithEstadoVacante(estadoCancelado);

        var vacanteRepo = new FakeVacanteLookupRepository
        {
            VacantesPorId = { [vacanteId] = vacanteCancelada }
        };

        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, vacanteRepo);

        var resultado = await servicio.ReactivarAsync(ocupacionConVacante.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(
            OcupacionErrorCodigo.VacanteCanceladaParaReactivar,
            resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_VacanteCubierta_Exito()
    {
        var vacanteId = Guid.Parse("70000000-0000-0000-0000-000000000503");
        var estadoCubiertaId = Guid.Parse("70000000-0000-0000-0000-000000000504");
        var estadoCubierta = new EstadoVacante("Cubierta", "Cubierta", 3, true, esCubierta: true)
        {
            Id = estadoCubiertaId
        };
        var vacanteCubierta = new Vacante(
            PuestoIdActivo,
            estadoCubiertaId,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            "Motivo")
        {
            Id = vacanteId
        };
        vacanteCubierta.WithEstadoVacante(estadoCubierta);

        var ocupacionConVacante = new Ocupacion(
            PersonaIdActiva, PuestoIdActivo, new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente, vacanteId: vacanteId);
        ocupacionConVacante.Finalizar(new DateOnly(2025, 6, 30));

        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacionConVacante] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var vacanteRepo = new FakeVacanteLookupRepository
        {
            VacantesPorId = { [vacanteId] = vacanteCubierta }
        };

        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, vacanteRepo);

        var resultado = await servicio.ReactivarAsync(ocupacionConVacante.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
    }

    [Fact]
    public async Task ReactivarAsync_SinVacanteId_Permite()
    {
        // Ocupacion histórica sin VacanteId → no consulta Vacante, permite reactivar.
        var ocupacionSinVacante = CrearOcupacionFinalizada(PuestoIdActivo, PersonaIdActiva, OcupacionIdFinalizada);

        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacionSinVacante] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var vacanteRepo = new FakeVacanteLookupRepository(); // no consultada

        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, vacanteRepo);

        var resultado = await servicio.ReactivarAsync(ocupacionSinVacante.Id, default);

        Assert.True(resultado.IsSuccess);
    }

    // Issue 10: Missing tests for ReactivarAsync reference validation.

    [Fact]
    public async Task ReactivarAsync_PersonaInexistente_Retorna404()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdActivo, PersonaIdInexistente, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository();
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_PersonaInactiva_Retorna409()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdActivo, PersonaIdInactiva, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaInactiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_PuestoInexistente_Retorna404()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdInexistente, PersonaIdActiva, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_PuestoInactivo_Retorna409()
    {
        var ocupacion = CrearOcupacionFinalizada(PuestoIdInactivo, PersonaIdActiva, OcupacionIdFinalizada);
        var ocupacionRepo = new FakeOcupacionWriteRepository { Datos = [ocupacion] };
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoInactivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(ocupacion.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── REQ-OCC-FORM-010 (invertir-flujo-cubrir): Cubrir vía VacanteId ──

    private static readonly Guid VacanteIdAbierta = Guid.Parse("70000000-0000-0000-0000-000000000701");
    private static readonly Guid VacanteIdCubierta = Guid.Parse("70000000-0000-0000-0000-000000000702");

    private static readonly Guid EstadoAbiertaVacanteId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid EstadoCubiertaVacanteId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    private static Vacante CrearVacanteAbierta(Guid vacanteId, Guid puestoId)
    {
        var estado = new EstadoVacante("Abierta", "Abierta", 1, false) { Id = EstadoAbiertaVacanteId };
        return new Vacante(puestoId, estado.Id, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Apertura")
        {
            Id = vacanteId
        }.WithEstadoVacante(estado);
    }

    private static Vacante CrearVacanteCubierta(Guid vacanteId, Guid puestoId)
    {
        var estado = new EstadoVacante("Cubierta", "Cubierta", 3, true, esCubierta: true) { Id = EstadoCubiertaVacanteId };
        var vacante = new Vacante(puestoId, estado.Id, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Apertura")
        {
            Id = vacanteId
        };
        vacante.CambiarEstado(estado.Id, null, motivo: "Cubierta por cobertura previa", cerrar: true);
        return vacante.WithEstadoVacante(estado);
    }

    private static CrearOcupacionRequest CrearRequestConVacante(
        Guid vacanteId,
        Guid? personaId = null,
        Guid? puestoId = null)
        => new(
            PersonaId: personaId ?? PersonaIdActiva,
            PuestoId: puestoId ?? PuestoIdActivo,
            FechaInicio: new DateOnly(2026, 1, 1),
            TipoAsignacion: OcupacionTipoAsignacion.Permanente,
            Observaciones: null,
            VacanteId: vacanteId);

    [Fact]
    public async Task CrearAsync_ConVacanteId_VacanteAbierta_CreaOcupacionYTransicionaVacanteACubierta()
    {
        // T1.1 — happy path: AddAsync de Ocupación con VacanteId, PuestoId del Vacante,
        // RegistrarCambioEstadoAsync invocado, y SaveChangesCount = 1.
        var vacante = CrearVacanteAbierta(VacanteIdAbierta, PuestoIdActivo);
        var trackingRepo = new TrackingVacanteRepository();
        trackingRepo.StagedVacantes[VacanteIdAbierta] = vacante;

        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, trackingRepo);

        var resultado = await servicio.CrearAsync(
            CrearRequestConVacante(VacanteIdAbierta, puestoId: PuestoIdActivo),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(PuestoIdActivo, resultado.Value!.PuestoId);
        Assert.Equal(PersonaIdActiva, resultado.Value.PersonaId);
        Assert.Equal(OcupacionEstado.Vigente, resultado.Value.Estado);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Equal(1, trackingRepo.RegistrarCambioEstadoCallCount);
        Assert.Single(ocupacionRepo.Datos);
        Assert.Equal(VacanteIdAbierta, ocupacionRepo.Datos[0].VacanteId);
        Assert.Equal(PuestoIdActivo, ocupacionRepo.Datos[0].PuestoId);
        Assert.True(ocupacionRepo.Datos[0].EsVigente);
    }

    [Fact]
    public async Task CrearAsync_ConVacanteId_VacanteNoEncontrada_DevuelveNotFound()
    {
        // T1.2 — GetByIdForUpdateAsync retorna null → VacanteNoEncontrada sin SaveChanges.
        var trackingRepo = new TrackingVacanteRepository(); // sin vacantes
        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, trackingRepo);

        var resultado = await servicio.CrearAsync(
            CrearRequestConVacante(Guid.Parse("70000000-0000-0000-0000-000000000799")),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(OcupacionErrorCodigo.VacanteNoEncontrada, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(ocupacionRepo.Datos);
        Assert.Equal(0, trackingRepo.RegistrarCambioEstadoCallCount);
    }

    [Fact]
    public async Task CrearAsync_ConVacanteId_VacanteCubierta_Devuelve400_VacanteNoAbierta()
    {
        // T1.3 — Vacante Cubierta (EstadoVacante.EsTerminal=true) → VacanteNoAbierta.
        var vacante = CrearVacanteCubierta(VacanteIdCubierta, PuestoIdActivo);
        var trackingRepo = new TrackingVacanteRepository();
        trackingRepo.StagedVacantes[VacanteIdCubierta] = vacante;

        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, trackingRepo);

        var resultado = await servicio.CrearAsync(
            CrearRequestConVacante(VacanteIdCubierta, puestoId: PuestoIdActivo),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Equal(OcupacionErrorCodigo.VacanteNoAbierta, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(ocupacionRepo.Datos);
    }

    [Fact]
    public async Task CrearAsync_ConVacanteId_VacanteYaCubierta_Devuelve409_VacanteYaCubierta()
    {
        // T1.4 — Vacante Abierta pero ya tiene Ocupación vigente → VacanteYaCubierta.
        var vacante = CrearVacanteAbierta(VacanteIdAbierta, PuestoIdActivo);
        var trackingRepo = new TrackingVacanteRepository();
        trackingRepo.StagedVacantes[VacanteIdAbierta] = vacante;

        // ExistsActiveByVacanteAsync devuelve true (fake con cobertura manual).
        var ocupacionRepo = new FakeOcupacionWriteRepositoryConCobertura();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, trackingRepo);

        var resultado = await servicio.CrearAsync(
            CrearRequestConVacante(VacanteIdAbierta, puestoId: PuestoIdActivo),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(OcupacionErrorCodigo.VacanteYaCubierta, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(ocupacionRepo.Datos);
        Assert.Equal(0, trackingRepo.RegistrarCambioEstadoCallCount);
    }

    [Fact]
    public async Task CrearAsync_ConVacanteId_PuestoIdNoCoincide_Devuelve400_PuestoIdNoCoincideConVacante()
    {
        // T1.5 — Vacante.PuestoId = P1, request PuestoId = P2 → 400 con fieldError en puestoId.
        var otraPuestoId = Guid.Parse("70000000-0000-0000-0000-000000000102");
        var vacante = CrearVacanteAbierta(VacanteIdAbierta, PuestoIdActivo);
        var trackingRepo = new TrackingVacanteRepository();
        trackingRepo.StagedVacantes[VacanteIdAbierta] = vacante;

        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        // El PuestoId del request no existe en el repo, pero el path se
        // rechaza ANTES de validar el Puesto (la coherencia con la Vacante
        // viene primero); igual poblamos el puesto del request para que el
        // catálogo no falle por error distinto.
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo(), new Puesto(Guid.NewGuid(), Guid.NewGuid(), "PUESTO-OTRO", "Otro Puesto") { Id = otraPuestoId }] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, trackingRepo);

        var resultado = await servicio.CrearAsync(
            CrearRequestConVacante(VacanteIdAbierta, puestoId: otraPuestoId),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Equal(OcupacionErrorCodigo.PuestoIdNoCoincideConVacante, resultado.Error.Code);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("puestoId", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(ocupacionRepo.Datos);
        Assert.Equal(0, trackingRepo.RegistrarCambioEstadoCallCount);
    }

    [Fact]
    public async Task CrearAsync_ConVacanteId_FalloEnSaveChanges_NoCreaOcupacionYNoTransicionaVacante()
    {
        // T1.6 — Atomicidad: FakeUnitOfWork.ThrowOnSaveChanges lanza DbUpdateException
        // al commit; ninguna inserción queda confirmada.
        var vacante = CrearVacanteAbierta(VacanteIdAbierta, PuestoIdActivo);
        var trackingRepo = new TrackingVacanteRepository();
        trackingRepo.StagedVacantes[VacanteIdAbierta] = vacante;

        var ocupacionRepo = new FakeOcupacionWriteRepository();
        var personaRepo = new FakePersonaWriteRepository { Datos = [CrearPersonaActiva()] };
        var puestoRepo = new FakePuestoWriteRepository { Datos = [CrearPuestoActivo()] };
        var uow = new FakeUnitOfWork
        {
            ThrowOnSaveChanges = new DbUpdateException("Simulated constraint violation in T1.6 (invertir-flujo-cubrir).")
        };
        var servicio = CrearServicio(ocupacionRepo, personaRepo, puestoRepo, uow, trackingRepo);

        var resultado = await servicio.CrearAsync(
            CrearRequestConVacante(VacanteIdAbierta, puestoId: PuestoIdActivo),
            default);

        Assert.False(resultado.IsSuccess);
        // El catch vigente mapea DbUpdateException a DatosInvalidos con Conflict.
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        // CambiarEstado se invocó (el servicio lo orquestó) pero no se
        // persistió: el fake de tracking confirma que el commit quedó vacío.
        Assert.Equal(1, trackingRepo.RegistrarCambioEstadoCallCount);
        Assert.Empty(trackingRepo.CommitedVacantes);
        Assert.Equal(1, uow.SaveChangesCount); // intentó, falló
    }

    // ── Helpers ────────────────────────────────────────────────

    private static OcupacionServicioComandos CrearServicio(
        IOcupacionRepository ocupacionRepo,
        IPersonaRepository personaRepo,
        IPuestoRepository puestoRepo,
        IUnitOfWork uow,
        IVacanteRepository? vacanteRepo = null,
        IEstadoVacanteRepository? estadoVacanteRepo = null)
    {
        return new OcupacionServicioComandos(
            ocupacionRepo, personaRepo, puestoRepo, uow,
            new FakeConstraintViolationDetector(),
            new FakeLogger<OcupacionServicioComandos>(),
            vacanteRepo ?? new FakeVacanteLookupRepository { PuestosConVacanteAbierta = [PuestoIdActivo] },
            estadoVacanteRepo ?? new FakeEstadoVacanteRepository { SoloCubierta = false });
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

internal class FakeOcupacionWriteRepository : IOcupacionRepository
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

    public Task<(IReadOnlyList<Ocupacion> Items, int TotalCount)> QueryAsync(
        OcupacionListQuery query,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Write fake does not support query operations.");

    // T1.9 / REQ-OCC-FORM-010 (invertir-flujo-cubrir): el fake declara las
    // firmas nuevas; el comportamiento concreto se agrega en T1.10 / GREEN.
    public virtual Task<bool> ExistsActiveByVacanteAsync(Guid vacanteId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public virtual Task<(Guid Id, string PersonaNombre)?> ObtenerVigentePorVacanteAsync(
        Guid vacanteId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(Guid Id, string PersonaNombre)?>(null);
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

    public Task<bool> ExistsActiveDocumentoAsync(Guid tipoDocumentoId, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");

    public Task<(IReadOnlyList<Persona> Items, int TotalCount)> QueryAsync(
        string? search, int page, int pageSize, string? sort = null,
        PersonaSegmentoListado segmento = PersonaSegmentoListado.Activas,
        CancellationToken cancellationToken = default,
        bool? soloSinUsuario = null)
        => Task.FromResult<(IReadOnlyList<Persona>, int)>(([], 0));
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

    public Task<(IReadOnlyList<Puesto> Items, int TotalCount)> QueryAsync(
        string? search, int page, int pageSize, string? sort = null,
        PuestoSegmentoListado segmento = PuestoSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(IReadOnlyList<Puesto>, int)>(([], 0));

    public Task<IReadOnlyList<Puesto>> ListarDisponiblesAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake for reference checking.");
}

internal sealed class FakeLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

internal sealed class FakeConstraintViolationDetector : IConstraintViolationDetector
{
    public bool IsConstraintViolation(DbUpdateException ex) => true;
}

internal sealed class FakeVacanteLookupRepository : IVacanteRepository
{
    public HashSet<Guid> PuestosConVacanteAbierta { get; set; } = [];
    public Dictionary<Guid, Vacante?> VacantesPorId { get; set; } = [];

    public Task<bool> ExistsAbiertaByPuestoAsync(Guid puestoId, CancellationToken ct = default)
        => Task.FromResult(PuestosConVacanteAbierta.Contains(puestoId));

    public Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        var v = VacantesPorId.TryGetValue(id, out var found) ? found : null;
        return Task.FromResult(v);
    }

    // Métodos no ejercidos por N3/Q2 — NotImplemented.
    public Task AddAsync(Vacante domain, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RegistrarCambioEstadoAsync(Vacante vacante, HistorialEstadoVacante historial, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(Vacante vacante, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(IReadOnlyList<Vacante> Items, int TotalCount)> ListarAsync(VacanteListQuery query, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Vacante?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Vacante>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
}

/// <summary>
/// Helper para tests Q2: hidrata la nav <c>Vacante.EstadoVacante</c> usando
/// reflection porque el setter es <c>private</c>. La nav es necesaria para
/// que el servicio distinga <c>EsCubierta</c> / <c>EsCancelada</c> en
/// los checks N2 (Cubrir) y Q2 (Reactivar). WU-8 (PR #259 review H-8):
/// los flags se setean ahora vía constructor de <see cref="EstadoVacante"/>;
/// este helper sólo cubre la nav property del agregado Vacante, no
/// duplicable sin tocar la entidad de dominio.
/// </summary>
internal static class VacanteTestExtensions
{
    public static Vacante WithEstadoVacante(this Vacante vacante, EstadoVacante estado)
    {
        var prop = typeof(Vacante).GetProperty(nameof(Vacante.EstadoVacante))
            ?? throw new InvalidOperationException(
                $"Vacante.EstadoVacante no encontrada: refactor de la entidad.");
        prop.SetValue(vacante, estado);
        return vacante;
    }
}

/// <summary>
/// Fake de <see cref="IVacanteRepository"/> orientado a los tests de
/// <c>OcupacionServicioComandos.CrearAsync</c> con <c>VacanteId</c>
/// (change <c>invertir-flujo-cubrir</c>). Carga la Vacante por id desde
/// <see cref="StagedVacantes"/> y registra los intentos de cambio de
/// estado para que los tests puedan verificar la atomicidad (los commits
/// se mueven a <see cref="CommitedVacantes"/> cuando el UoW completa).
/// </summary>
internal sealed class TrackingVacanteRepository : IVacanteRepository
{
    public Dictionary<Guid, Vacante?> StagedVacantes { get; } = [];
    public List<Vacante> CommitedVacantes { get; } = [];
    public int RegistrarCambioEstadoCallCount { get; private set; }

    private bool _pending;
    private Vacante _stagingVacante = default!;
    private HistorialEstadoVacante _stagingHistorial = default!;

    public Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(StagedVacantes.TryGetValue(id, out var v) ? v : null);

    public Task RegistrarCambioEstadoAsync(Vacante vacante, HistorialEstadoVacante historial, CancellationToken ct = default)
    {
        RegistrarCambioEstadoCallCount++;
        _stagingVacante = vacante;
        _stagingHistorial = historial;
        _pending = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Compromete la mutación pendiente (modela el SaveChangesAsync exitoso
    /// de EF). Si el UoW tiró, el test no llama a Commit y la lista queda
    /// vacía — demostrando el rollback.
    /// </summary>
    public void Commit()
    {
        if (!_pending) return;
        CommitedVacantes.Add(_stagingVacante);
        _pending = false;
    }

    // Métodos no usados por el path REQ-OCC-FORM-010.
    public Task AddAsync(Vacante vacante, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(Vacante vacante, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(IReadOnlyList<Vacante> Items, int TotalCount)> ListarAsync(VacanteListQuery query, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsAbiertaByPuestoAsync(Guid puestoId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Vacante?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Vacante>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
}

/// <summary>
/// Variante de <see cref="FakeOcupacionWriteRepository"/> que reporta
/// <c>ExistsActiveByVacanteAsync = true</c> (para T1.4).
/// </summary>
internal sealed class FakeOcupacionWriteRepositoryConCobertura : FakeOcupacionWriteRepository
{
    public override Task<bool> ExistsActiveByVacanteAsync(Guid vacanteId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

// T1.6 (invertir-flujo-cubrir) usa el FakeUnitOfWork con ThrowOnSaveChanges
// declarado en este mismo assembly (SGV.Tests.Aplicacion.Vacantes) para
// simular la constraint violation. No se necesita un fake separado.
