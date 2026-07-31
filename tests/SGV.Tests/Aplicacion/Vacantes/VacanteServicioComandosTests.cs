using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Vacantes.Comandos;
using SGV.Aplicacion.Vacantes.Consultas;
using SGV.Contracts.Comun;
using SGV.Contracts.Vacantes.Comandos;
using SGV.Contracts.Vacantes.Consultas;
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
        string? observaciones = null) => new(
        EstadoVacanteId: estadoVacanteId ?? EstadoEnSeleccionId,
        Motivo: motivo,
        Observaciones: observaciones);

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
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCubiertaId, motivo: "Cubierta por postulante aceptado"),
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
            CrearCambioEstadoRequest(estadoVacanteId: EstadoCubiertaId),
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
        IUnitOfWork uow)
    {
        return new VacanteServicioComandos(
            vacanteRepo, estadoRepo, uow,
            new FakeConstraintViolationDetector(),
            new FakeLogger<VacanteServicioComandos>());
    }
}

// ── Fakes ────────────────────────────────────────────────────────

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