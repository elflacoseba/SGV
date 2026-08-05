using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Vacantes.Comandos;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Consultas;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Vacantes;
using Xunit;

namespace SGV.Tests.Aplicacion.Vacantes;

/// <summary>
/// Cobertura RED→GREEN de <see cref="VacanteServicioComandos"/>.
/// Cubrir los pivotes de la spec del work unit 3.x:
/// S-1 (<c>Crear_PuestoConVacanteAbierta_DevuelveConflicto</c>),
/// atomicidad de cambio de estado
/// (<c>CambiarEstado_AtomicidadVacanteEHistorial</c>), estado terminal
/// inmutable
/// (<c>CambiarEstado_EstadoTerminal_DevuelveEstadoTerminalInmutable</c>),
/// validación de request (paridad con <c>OcupacionServicioComandosTests</c>).
/// </summary>
public sealed class VacanteServicioComandosTests
{
    private static readonly Guid PuestoId1 = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid PuestoId2 = Guid.Parse("70000000-0000-0000-0000-000000000002");

    private static readonly Guid EstadoAbiertaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid EstadoEnSeleccionId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid EstadoCubiertaId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid EstadoCanceladaId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid EstadoInexistenteId = Guid.Parse("70000000-0000-0000-0000-000000000099");

    private static readonly Guid VacanteId1 = Guid.Parse("70000000-0000-0000-0000-000000000301");
    private static readonly Guid VacanteId2 = Guid.Parse("70000000-0000-0000-0000-000000000302");
    private static readonly Guid VacanteInexistenteId = Guid.Parse("70000000-0000-0000-0000-000000000399");

    private static CrearVacanteRequest CrearRequestValido(
        Guid? puestoId = null,
        Guid? estadoVacanteId = null,
        string motivo = "Apertura de vacante",
        string? observaciones = null) => new(
        PuestoId: puestoId ?? PuestoId1,
        EstadoVacanteId: estadoVacanteId ?? EstadoAbiertaId,
        FechaApertura: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        Motivo: motivo,
        Observaciones: observaciones);

    private static CambiarEstadoVacanteRequest CrearCambioEstadoRequest(
        Guid? estadoVacanteId = null,
        string? motivo = null,
        string? observaciones = null,
        Guid? personaId = null) => new(
        EstadoVacanteId: estadoVacanteId ?? EstadoEnSeleccionId,
        Motivo: motivo,
        Observaciones: observaciones,
        PersonaId: personaId);

    // ── CrearAsync ─────────────────────────────────────────────

    [Fact]
    public async Task Crear_DatosValidos_RetornaExitoYGuarda()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequestValido(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(PuestoId1, resultado.Value!.PuestoId);
        Assert.Equal(EstadoAbiertaId, resultado.Value.EstadoVacanteId);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Single(repo.Datos);
    }

    [Fact]
    public async Task Crear_PuestoIdVacio_RetornaValidationFailure()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var request = CrearRequestValido(puestoId: Guid.Empty);
        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("puestoId", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Crear_EstadoInicialTerminalCubierta_RetornaValidationFailure()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var request = CrearRequestValido(estadoVacanteId: EstadoCubiertaId);
        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.EstadoTerminalInmutable, resultado.Error.Code);
        Assert.Contains("estadoVacanteId", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(repo.Datos);
    }

    [Fact]
    public async Task Crear_EstadoInicialTerminalCancelada_RetornaValidationFailure()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var request = CrearRequestValido(estadoVacanteId: EstadoCanceladaId);
        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.EstadoTerminalInmutable, resultado.Error.Code);
        Assert.Contains("estadoVacanteId", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(repo.Datos);
    }

    [Fact]
    public async Task Crear_EstadoVacanteIdVacio_RetornaValidationFailure()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var request = CrearRequestValido(estadoVacanteId: Guid.Empty);
        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Contains("estadoVacanteId", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Crear_MotivoVacio_RetornaValidationFailure()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var request = CrearRequestValido(motivo: "");
        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task Crear_EstadoVacanteInexistente_Retorna404()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository(); // vacío
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var request = CrearRequestValido(estadoVacanteId: EstadoInexistenteId);
        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.EstadoVacanteInexistente, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    /// <summary>
    /// S-1: pivot spec. Si el puesto ya tiene una vacante abierta
    /// (no terminal), el crear debe rechazar con 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Crear_PuestoConVacanteAbierta_DevuelveConflicto()
    {
        var existente = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo previo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository
        {
            Datos = [existente],
            AbiertasByPuesto = new HashSet<Guid> { PuestoId1 }
        };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequestValido(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.PuestoConVacanteAbierta, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Single(repo.Datos); // sin nueva inserción
    }

    /// <summary>
    /// T1.1 (issue #238): cuando el <c>SaveChangesAsync</c> del
    /// <see cref="IUnitOfWork"/> lanza <see cref="DbUpdateException"/> por
    /// una constraint violation (e.g. unique index
    /// <c>IX_Vacantes_ActivePuestoIdUnique</c> rechaza la inserción por
    /// la ventana TOCTOU entre el pre-check y la persistencia), el
    /// servicio DEBE mapearla a <c>409 Conflict</c> con código
    /// <see cref="VacanteErrorCodigo.PuestoConVacanteAbierta"/> — el mismo
    /// código que el pre-check ya usa en la línea 152. Defense-in-depth:
    /// la BD es la fuente de verdad final; el catch traduce la
    /// <see cref="DbUpdateException"/> al código de negocio correcto.
    /// </summary>
    [Fact]
    public async Task Crear_SaveChangesFallaPorConstraint_DevuelveConflictoPuestoConVacanteAbierta()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        // El fake detector devuelve true, simulando que el detector real
        // detectó MySqlException.Number 1062 (ER_DUP_ENTRY) en el
        // InnerException. El catch de CrearAsync:177 dispara y mapea.
        var uow = new FakeUnitOfWork
        {
            ThrowOnSaveChanges = new DbUpdateException(
                "Duplicate entry for key 'IX_Vacantes_ActivePuestoIdUnique'")
        };
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequestValido(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(
            VacanteErrorCodigo.PuestoConVacanteAbierta,
            resultado.Error.Code);
        Assert.Equal(1, uow.SaveChangesCount); // intentó persistir
        Assert.Equal(1, repo.AddCallCount);     // AddAsync se invocó
    }

    // ── N1 (T-2.2): CrearVacante rechaza si existe Ocupacion activa ─────

    [Fact]
    public async Task Crear_PuestoConOcupacionActiva_DevuelveConflictoPuestoOcupado()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository
        {
            PuestosConOcupacionActiva = [PuestoId1]
        };
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CrearAsync(CrearRequestValido(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.PuestoOcupado, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(repo.Datos);
    }

    [Fact]
    public async Task Crear_PuestoSinOcupacion_Exito()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository(); // sin ocupaciones activas
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CrearAsync(CrearRequestValido(), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Single(repo.Datos);
    }

    [Fact]
    public async Task Crear_PuestoConOcupacionEliminada_NoBloquea()
    {
        // Ocupacion pre-existente pero FechaFin != null (finalizada o eliminada)
        // → ExistsActiveByPuestoAsync devuelve false → N1 no bloquea.
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository(); // sin activas
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CrearAsync(CrearRequestValido(), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // ── N2 (T-4.3): Cubrir Vacante crea Ocupacion derivada ───────────

    private static readonly Guid PersonaGanadoraId = Guid.Parse("70000000-0000-0000-0000-000000000601");

    [Fact]
    public async Task CambiarEstado_A_Cubierta_ConPersonaId_CreaOcupacionYRegistraHistorial()
    {
        var abierta = new Vacante(PuestoId1, EstadoCubiertaIdAbierta, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository();
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCubiertaId, personaId: PersonaGanadoraId),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, ocupacionRepo.AddCallCount);
        Assert.Equal(abierta.Id, ocupacionRepo.LastAddedVacanteId);
        Assert.Equal(PersonaGanadoraId, ocupacionRepo.LastAddedPersonaId);
        Assert.Equal(PuestoId1, ocupacionRepo.LastAddedPuestoId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarEstado_A_Cubierta_SinPersonaId_DevuelvePersonaIdRequerido()
    {
        var abierta = new Vacante(PuestoId1, EstadoCubiertaIdAbierta, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository();
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCubiertaId), // sin PersonaId
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.PersonaIdRequeridoParaCubrir, resultado.Error.Code);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("personaId", resultado.FieldErrors!.Keys);
        Assert.Equal(0, ocupacionRepo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarEstado_A_Cancelada_NoCreaOcupacion()
    {
        var abierta = new Vacante(PuestoId1, EstadoCubiertaIdAbierta, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository();
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCanceladaId),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, ocupacionRepo.AddCallCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarEstado_A_NoTerminal_FlujoInalterado()
    {
        var abierta = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository();
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoEnSeleccionId),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, ocupacionRepo.AddCallCount);
    }

    [Fact]
    public async Task CambiarEstado_Atomicidad_DbUpdateException_NoPersiste()
    {
        var abierta = new Vacante(PuestoId1, EstadoCubiertaIdAbierta, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        // El fake usa Commit explícito: si SaveChangesAsync lanza, no
        // se aplica ningún cambio al store final. Esto modela el
        // rollback EF: una sola transacción cubre AddAsync, cambio de
        // estado, historial y la Ocupacion derivada. La prueba real de
        // atomicidad se hace contra MySQL (T-1.6 + OcupacionVacanteId-
        // PersistenciaTests). Aquí validamos que el commit del fake
        // queda vacío cuando el UoW tira, lo que demuestra que la
        // orquestación del servicio no produce cambios persistentes.
        var repo = new TrackingVacanteWriteRepository(abierta);
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork { ThrowOnSaveChanges = new DbUpdateException("FK violation") };
        var ocupacionRepo = new FakeOcupacionLookupRepository();
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCubiertaId, personaId: PersonaGanadoraId),
            default);

        Assert.False(resultado.IsSuccess);
        // El commit del fake está vacío: SaveChangesAsync tiró antes de
        // que se aplicara ningún cambio.
        Assert.Empty(repo.CommitedVacantes);
        Assert.Empty(repo.CommitedHistorial);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarEstado_CubrirExitoso_PersisteYAgregaOcupacion()
    {
        var abierta = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new TrackingVacanteWriteRepository(abierta);
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var ocupacionRepo = new FakeOcupacionLookupRepository();
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCubiertaId, personaId: PersonaGanadoraId),
            default);

        Assert.True(resultado.IsSuccess);
        repo.Commit();
        Assert.Equal(1, ocupacionRepo.AddCallCount);
        Assert.Equal(VacanteId1, ocupacionRepo.LastAddedVacanteId);
    }

    /// <summary>
    /// N4 (libera posición tras Cubrir → Finalizar Ocupación derivada):
    /// un Cubrir deja la posición "ocupada" (N1) por la Ocupación derivada.
    /// Tras Finalizar esa Ocupación, el detector N1 (ExistsActiveByPuesto)
    /// debe volver a false, lo que permite crear una nueva Vacante para
    /// el mismo Puesto. Cubre la transición Cubrir → Finalizar.
    /// </summary>
    [Fact]
    public async Task CubrirYLuegoFinalizar_PermiteNuevaVacante_ParaMismoPuesto()
    {
        var abierta = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        // Puesto con Ocupación activa al inicio (estado post-Cubrir).
        var ocupacionRepo = new FakeOcupacionLookupRepository { PuestosConOcupacionActiva = [PuestoId1] };
        var servicio = CrearServicio(repo, estadoRepo, uow, ocupacionRepo);

        // 1) Pre-check: N1 bloquea CrearVacante porque hay Ocupación activa.
        var crearBloqueado = await servicio.CrearAsync(CrearRequestValido(), default);
        Assert.False(crearBloqueado.IsSuccess);
        Assert.Equal(VacanteErrorCodigo.PuestoOcupado, crearBloqueado.Error!.Code);

        // 2) Simula Finalizar la Ocupación derivada: el detector cae a false.
        ocupacionRepo.PuestosConOcupacionActiva = [];

        // 3) Ahora CrearVacante debe tener éxito (la posición se liberó).
        var crearLiberado = await servicio.CrearAsync(CrearRequestValido(), default);
        Assert.True(crearLiberado.IsSuccess);
    }


    // Alias local para evitar conflicto con los IDs ya definidos más arriba.
    private static readonly Guid EstadoCubiertaIdAbierta = EstadoAbiertaId;

    // ── CambiarEstadoAsync ─────────────────────────────────────

    [Fact]
    public async Task CambiarEstado_VacanteInexistente_Retorna404()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CambiarEstadoAsync(
            VacanteInexistenteId,
            CrearCambioEstadoRequest(),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.VacanteInexistente, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    /// <summary>
    /// Pivot spec: si la vacante está en estado terminal (Cubierta),
    /// el servicio rechaza con 409 Conflict — el estado es inmutable.
    /// </summary>
    [Fact]
    public async Task CambiarEstado_EstadoTerminal_DevuelveEstadoTerminalInmutable()
    {
        var cubierta = new Vacante(PuestoId1, EstadoCubiertaId, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo cubierta")
        {
            Id = VacanteId1
        };
        cubierta.CambiarEstado(EstadoCubiertaId, null, motivo: "Cierre original", cerrar: true);

        var repo = new FakeVacanteWriteRepository { Datos = [cubierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CambiarEstadoAsync(
            cubierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoAbiertaId),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.EstadoTerminalInmutable, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarEstado_EstadoDestinoInexistente_Retorna404()
    {
        var abierta = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository(); // vacío → destino inexistente
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoInexistenteId),
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.EstadoVacanteInexistente, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarEstado_AEstadoTerminal_SeteaFechaCierre()
    {
        var abierta = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(
                estadoVacanteId: EstadoCubiertaId,
                motivo: "Cubierta por postulante aceptado",
                personaId: Guid.NewGuid()),
            default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(abierta.FechaCierre); // el dominio setea FechaCierre cuando cerrar=true
        Assert.Equal(EstadoCubiertaId, abierta.EstadoVacanteId);
        Assert.Single(resultado.Value!.Historial); // el historial incluye la nueva entrada
        Assert.Equal(1, uow.SaveChangesCount);
    }

    /// <summary>
    /// Pivot spec: atomicidad vacante + historial. Si SaveChangesAsync
    /// lanza <see cref="DbUpdateException"/> durante el bridge, la
    /// operación debe reportar 409 Conflict sin persistir cambios
    /// parciales. El fake fuerza la excepción en SaveChangesAsync y
    /// confirma que el resultado es Failure con categoria Conflict.
    /// </summary>
    [Fact]
    public async Task CambiarEstado_AtomicidadVacanteEHistorial_SaveChangesFalla_Retorna409()
    {
        var abierta = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [abierta] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork { ThrowOnSaveChanges = new DbUpdateException("FK violation simulated") };
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.CambiarEstadoAsync(
            abierta.Id,
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCanceladaId), // destino no-Cubierta
            default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Conflict, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.DatosInvalidos, resultado.Error.Code);
    }

    // ── ActualizarObservacionesAsync ───────────────────────────

    [Fact]
    public async Task ActualizarObservaciones_VacanteInexistente_Retorna404()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.ActualizarObservacionesAsync(
            VacanteInexistenteId, "Notas", default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.NotFound, resultado.Error!.Categoria);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarObservaciones_TextoValido_PersisteYLimpia()
    {
        var vacante = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        var repo = new FakeVacanteWriteRepository { Datos = [vacante] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.ActualizarObservacionesAsync(
            vacante.Id, "Notas actualizadas", default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Notas actualizadas", vacante.Observaciones);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarObservaciones_TextoMuyLargo_RetornaValidationFailure()
    {
        var repo = new FakeVacanteWriteRepository();
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var demasiadoLargo = new string('x', 501);
        var resultado = await servicio.ActualizarObservacionesAsync(
            VacanteId1, demasiadoLargo, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(ErrorCategoria.Validation, resultado.Error!.Categoria);
        Assert.Equal(VacanteErrorCodigo.ObservacionesMuyLargas, resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarObservaciones_NuloOLimpio_LimpiaValor()
    {
        var vacante = new Vacante(PuestoId1, EstadoAbiertaId, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "Motivo")
        {
            Id = VacanteId1
        };
        vacante.ActualizarObservaciones("Existente");
        var repo = new FakeVacanteWriteRepository { Datos = [vacante] };
        var estadoRepo = new FakeEstadoVacanteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, estadoRepo, uow);

        var resultado = await servicio.ActualizarObservacionesAsync(
            vacante.Id, null, default);

        Assert.True(resultado.IsSuccess);
        Assert.Null(vacante.Observaciones);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static VacanteServicioComandos CrearServicio(
        IVacanteRepository vacanteRepo,
        IEstadoVacanteRepository estadoRepo,
        IUnitOfWork uow,
        IOcupacionRepository? ocupacionRepo = null)
    {
        return new VacanteServicioComandos(
            vacanteRepo, estadoRepo, uow,
            new FakeConstraintViolationDetector(),
            new FakeLogger<VacanteServicioComandos>(),
            ocupacionRepo ?? new FakeOcupacionLookupRepository());
    }
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakeOcupacionLookupRepository : IOcupacionRepository
{
    public HashSet<Guid> PuestosConOcupacionActiva { get; set; } = [];

    public int AddCallCount { get; private set; }
    public Guid? LastAddedVacanteId { get; private set; }
    public Guid? LastAddedPersonaId { get; private set; }
    public Guid? LastAddedPuestoId { get; private set; }
    public DateOnly? LastAddedFechaInicio { get; private set; }

    // N2: AddAsync captura las propiedades clave de la Ocupacion derivada.
    public Task AddAsync(Ocupacion domain, CancellationToken ct = default)
    {
        AddCallCount++;
        LastAddedVacanteId = domain.VacanteId;
        LastAddedPersonaId = domain.PersonaId;
        LastAddedPuestoId = domain.PuestoId;
        LastAddedFechaInicio = domain.FechaInicio;
        return Task.CompletedTask;
    }

    // Stubs no usados por los paths N1/N2.
    public Task<Ocupacion?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Ocupacion?> GetByIdIncludingHistoryAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(Ocupacion domain, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Ocupacion>> ListAllIncludingHistoryAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(IReadOnlyList<Ocupacion> Items, int TotalCount)> QueryAsync(OcupacionListQuery query, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ExistsActiveByPersonaYPuestoAsync(Guid personaId, Guid puestoId, Guid? excludingId = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Ocupacion?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Ocupacion>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();

    public Task<bool> ExistsActiveByPuestoAsync(
        Guid puestoId, Guid? excludingId = null, CancellationToken ct = default)
        => Task.FromResult(PuestosConOcupacionActiva.Contains(puestoId));
}

internal sealed class FakeVacanteWriteRepository : IVacanteRepository
{
    public List<Vacante> Datos { get; set; } = [];
    public HashSet<Guid> AbiertasByPuesto { get; set; } = [];
    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int GetByIdForUpdateCallCount { get; private set; }
    public int RegistrarCambioEstadoCallCount { get; private set; }

    public Task AddAsync(Vacante vacante, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Datos.Add(vacante);
        return Task.CompletedTask;
    }

    public Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(v => v.Id == id));
    }

    public Task RegistrarCambioEstadoAsync(
        Vacante vacante,
        HistorialEstadoVacante historial,
        CancellationToken cancellationToken = default)
    {
        RegistrarCambioEstadoCallCount++;
        // No-op fake: la mutación ya está en el domain (vacante.CambiarEstado).
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Vacante vacante, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var idx = Datos.FindIndex(v => v.Id == vacante.Id);
        if (idx >= 0) Datos[idx] = vacante;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAbiertaByPuestoAsync(Guid puestoId, CancellationToken cancellationToken = default)
        => Task.FromResult(AbiertasByPuesto.Contains(puestoId));

    public Task<Vacante?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(v => v.Id == id));

    public Task<IReadOnlyList<Vacante>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Vacante>>(Datos);

    public Task<(IReadOnlyList<Vacante> Items, int TotalCount)> ListarAsync(
        VacanteListQuery query, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Write fake does not support ListarAsync.");
}

internal sealed class FakeEstadoVacanteRepository : IEstadoVacanteRepository
{
    public List<EstadoVacante> Datos { get; set; } = [];

    public FakeEstadoVacanteRepository()
    {
        Datos =
        [
            new EstadoVacante("Abierta", "Abierta", 1, false) { Id = Guid.Parse("20000000-0000-0000-0000-000000000001") },
            new EstadoVacante("EnSeleccion", "En Selección", 2, false) { Id = Guid.Parse("20000000-0000-0000-0000-000000000002") },
            new EstadoVacante("Cubierta", "Cubierta", 3, true) { Id = Guid.Parse("20000000-0000-0000-0000-000000000003") },
            new EstadoVacante("Cancelada", "Cancelada", 4, true) { Id = Guid.Parse("20000000-0000-0000-0000-000000000004") }
        ];
    }

    public Task<EstadoVacante?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<EstadoVacante>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EstadoVacante>>(Datos);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    /// <summary>
    /// When set, <see cref="SaveChangesAsync"/> throws the given
    /// exception. Used to simulate a constraint violation mid-bridge.
    /// </summary>
    public Exception? ThrowOnSaveChanges { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        if (ThrowOnSaveChanges is not null)
        {
            throw ThrowOnSaveChanges;
        }
        return Task.FromResult(1);
    }
}

/// <summary>
/// Fake instrumentado de <see cref="IVacanteRepository"/> que sólo persiste
/// los cambios cuando el <see cref="IUnitOfWork"/> que lo acompaña completa
/// <c>SaveChangesAsync</c> exitosamente. Si el commit falla, las mutaciones
/// quedan en el staging y no se aplican al snapshot, demostrando que la
/// atomicidad del bridge EF se respeta.
/// </summary>
internal sealed class TrackingVacanteWriteRepository : IVacanteRepository
{
    private readonly Vacante _seed;
    private Vacante _stagingVacante = default!;
    private HistorialEstadoVacante _stagingHistorial = default!;
    private bool _pending;

    public TrackingVacanteWriteRepository(Vacante seed) => _seed = seed;

    public List<Vacante> CommitedVacantes { get; } = [];
    public List<HistorialEstadoVacante> CommitedHistorial { get; } = [];

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int GetByIdForUpdateCallCount { get; private set; }
    public int RegistrarCambioEstadoCallCount { get; private set; }

    public Task AddAsync(Vacante vacante, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        _stagingVacante = vacante;
        _pending = true;
        return Task.CompletedTask;
    }

    public Task<Vacante?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult<Vacante?>(_seed);
    }

    public Task RegistrarCambioEstadoAsync(
        Vacante vacante,
        HistorialEstadoVacante historial,
        CancellationToken cancellationToken = default)
    {
        RegistrarCambioEstadoCallCount++;
        _stagingVacante = vacante;
        _stagingHistorial = historial;
        _pending = true;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Vacante vacante, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        _stagingVacante = vacante;
        _pending = true;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAbiertaByPuestoAsync(Guid puestoId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<Vacante?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<Vacante?>(_seed);

    public Task<IReadOnlyList<Vacante>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Vacante>>([_seed]);

    public Task<(IReadOnlyList<Vacante> Items, int TotalCount)> ListarAsync(
        VacanteListQuery query, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Tracking fake does not support ListarAsync.");

    public void Commit()
    {
        if (!_pending) return;
        CommitedVacantes.Add(_stagingVacante);
        if (_stagingHistorial is not null) CommitedHistorial.Add(_stagingHistorial);
        _pending = false;
    }
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