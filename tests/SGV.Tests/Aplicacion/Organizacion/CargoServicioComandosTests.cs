using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class CargoServicioComandosTests
{
    private static readonly Guid CargoId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid NivelIdValido = NivelCargoConstantes.DirectivoId;
    private static readonly Guid NivelIdInexistente = Guid.Parse("99999999-0000-0000-0000-000000000001");

    private static readonly FakeNivelCargoRepo FakeNivelRepo = new()
    {
        Datos =
        [
            new("Directivo", "Directivo", 1, 1) { Id = NivelCargoConstantes.DirectivoId },
            new("ConduccionMedia", "Conducción Media", 2, 2) { Id = NivelCargoConstantes.ConduccionMediaId },
            new("Operativo", "Operativo", 3, 3) { Id = NivelCargoConstantes.OperativoId },
            new("Academico", "Académico", 4, 4) { Id = NivelCargoConstantes.AcademicoId },
        ]
    };

    private static CrearCargoRequest CrearRequest(string? codigo = null, Guid? nivelId = null)
        => new(
            codigo ?? "DIR-01",
            "Director General",
            nivelId ?? NivelIdValido,
            "Máxima autoridad directiva");

    // ── CrearAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var repo = new FakeCargoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("DIR-01", resultado.Value!.Codigo);
        Assert.Equal("Director General", resultado.Value.Nombre);
        Assert.Equal(NivelIdValido, resultado.Value.NivelId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar()
    {
        var existente = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest("DIR-01"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_NivelIdInexistente_RetornaValidacionYSinGuardar()
    {
        var repo = new FakeCargoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(nivelId: NivelIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("NivelCargoNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ActualizarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda()
    {
        var existente = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new ActualizarCargoRequest(
            "DIR-01",
            "Director General Actualizado",
            NivelCargoConstantes.OperativoId,
            "Descripción actualizada");

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Director General Actualizado", resultado.Value!.Nombre);
        Assert.Equal(NivelCargoConstantes.OperativoId, resultado.Value.NivelId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CargoInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeCargoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new ActualizarCargoRequest("DIR-01", "Nombre", NivelIdValido);

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(), request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_NivelIdInexistente_RetornaValidacionYSinGuardar()
    {
        var existente = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new ActualizarCargoRequest("DIR-01", "Nombre", NivelIdInexistente);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("NivelCargoNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── DesactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task DesactivarAsync_CargoExistente_RetornaExitoYGuarda()
    {
        var cargo = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [cargo] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.DesactivarAsync(cargo.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task DesactivarAsync_CargoInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeCargoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.DesactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task DesactivarAsync_ConPuestosActivos_RetornaConflictoYSinGuardar()
    {
        var cargo = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository
        {
            Datos = [cargo],
            PuestosPorCargo = new Dictionary<Guid, List<Puesto>>
            {
                [cargo.Id] = [new Puesto(Guid.NewGuid(), Guid.NewGuid(), "PUESTO-001", "Puesto Activo")]
            }
        };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.DesactivarAsync(cargo.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CargoConPuestosActivos", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ReactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ReactivarAsync_CargoDesactivado_RetornaExitoYGuarda()
    {
        var cargo = CrearCargoDesactivado("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [cargo] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.ReactivarAsync(cargo.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_CargoInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeCargoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.ReactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_CodigoConflictivo_RetornaConflictoYSinGuardar()
    {
        var activo = CrearCargoActivo("DIR-01", Guid.Parse("80000000-0000-0000-0000-0000000000A1"));
        var desactivado = CrearCargoDesactivado("DIR-01", CargoId);
        var repo = new FakeCargoWriteRepository { Datos = [activo, desactivado] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);

        var resultado = await servicio.ReactivarAsync(desactivado.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Short-circuit: validation before repo calls ────────────

    [Fact]
    public async Task CrearAsync_CodigoVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var repo = new FakeCargoWriteRepository
        {
            Datos = [CrearCargoActivo("DIR-01")]
        };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new CrearCargoRequest("", "Nombre", Guid.NewGuid());

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Codigo", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_NombreVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var repo = new FakeCargoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new CrearCargoRequest("NUEVO", "", Guid.NewGuid());

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombre", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Nombre", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_MultiplesErrores_EmiteTodasLasClavesCamelCaseYSinConsultarRepos()
    {
        var repo = new FakeCargoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new CrearCargoRequest("", "", Guid.Empty);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Contains("nombre", resultado.FieldErrors.Keys);
        Assert.Contains("nivelId", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Codigo", resultado.FieldErrors.Keys);
        Assert.DoesNotContain("Nombre", resultado.FieldErrors.Keys);
        Assert.DoesNotContain("NivelId", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ActualizarAsync — unicidad activa de Codigo ────────────

    [Fact]
    public async Task ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar()
    {
        var existente = CrearCargoActivo("DIR-01");
        var otroActivo = CrearCargoActivo("DIR-02", Guid.Parse("80000000-0000-0000-0000-0000000000A1"));
        var repo = new FakeCargoWriteRepository { Datos = [existente, otroActivo] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new ActualizarCargoRequest("DIR-02", "Director Renombrado", NivelIdValido);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_MismoCodigoEnOtroCargoEliminado_PermiteOperacion()
    {
        var existente = CrearCargoActivo("DIR-01");
        var otroEliminado = CrearCargoDesactivado("DIR-02", Guid.Parse("80000000-0000-0000-0000-0000000000A1"));
        var repo = new FakeCargoWriteRepository { Datos = [existente, otroEliminado] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new ActualizarCargoRequest("DIR-02", "Director Renombrado", NivelIdValido);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("DIR-02", resultado.Value!.Codigo);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos()
    {
        var existente = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        var request = new ActualizarCargoRequest("", "Nombre", NivelIdValido);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdForUpdateCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoDuplicado_RaceCondition_DevuelveConflicto()
    {
        var existente = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [existente] };
        // Pre-check passes (no duplicate yet), but SaveChanges simulates DB index violation.
        var uow = new FakeThrowingUnitOfWork(
            new DbUpdateException("Duplicate entry for key 'IX_Cargos_ActiveCodigoUnique'"));
        var servicio = new CargoServicioComandos(
            repo, FakeNivelRepo, uow,
            new FakeConstraintViolationDetector(),
            new CrearCargoRequestValidator(),
            new ActualizarCargoRequestValidator());
        var request = new ActualizarCargoRequest("OTRO", "Director Renombrado", NivelIdValido);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(CargoErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoSinCambio_NoFallaValidacionUnicidad()
    {
        var existente = CrearCargoActivo("DIR-01");
        var repo = new FakeCargoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new CargoServicioComandos(repo, FakeNivelRepo, uow);
        // Mismo Codigo que el cargo existente: la unicidad no debe dispararse porque
        // el repo la evalúa con excludingId y el fake lo respeta.
        var request = new ActualizarCargoRequest("DIR-01", "Director Renombrado", NivelIdValido);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("DIR-01", resultado.Value!.Codigo);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static Cargo CrearCargoActivo(string codigo, Guid? id = null)
    {
        var cargo = new Cargo(codigo, codigo, NivelIdValido)
        {
            Id = id ?? Guid.NewGuid()
        };
        return cargo;
    }

    private static Cargo CrearCargoDesactivado(string codigo, Guid? id = null)
    {
        var cargo = CrearCargoActivo(codigo, id);
        // Puestos collection is empty, so Desactivar() won't throw
        cargo.Desactivar();
        return cargo;
    }
}

internal sealed class FakeConstraintViolationDetector : IConstraintViolationDetector
{
    public bool IsConstraintViolation(DbUpdateException exception) => true;
}

internal sealed class FakeThrowingUnitOfWork : IUnitOfWork
{
    private readonly DbUpdateException _exceptionToThrow;

    public FakeThrowingUnitOfWork(DbUpdateException exceptionToThrow)
    {
        _exceptionToThrow = exceptionToThrow;
    }

    public int SaveChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        throw _exceptionToThrow;
    }
}

internal sealed class FakeCargoWriteRepository : ICargoRepository
{
    public List<Cargo> Datos { get; set; } = [];

    // Per-method call counters
    public int AddCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public int ExistsActiveCodeCallCount { get; private set; }
    public int GetByIdCallCount { get; private set; }
    public int GetByIdForUpdateCallCount { get; private set; }
    public int GetByIdIncludingDeletedCallCount { get; private set; }
    public int ListAllCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int ReactivateCallCount { get; private set; }
    public int HasActivePuestosCallCount { get; private set; }

    /// <summary>
    /// Optional dictionary to simulate active puestos per cargo for testing delete protection.
    /// </summary>
    public Dictionary<Guid, List<Puesto>> PuestosPorCargo { get; set; } = [];

    public Task AddAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Datos.Add(cargo);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        var cargo = Datos.FirstOrDefault(d => d.Id == id);
        if (cargo is not null)
        {
            cargo.Desactivar();
            Datos.Remove(cargo);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveCodeCallCount++;
        var duplicado = Datos.Any(d =>
            d.Codigo == codigo &&
            d.IsActive &&
            d.Id != excludingId);
        return Task.FromResult(duplicado);
    }

    public Task<Cargo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive));
    }

    public Task<Cargo?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive));
    }

    public Task<Cargo?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdIncludingDeletedCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id));
    }

    public Task<IReadOnlyList<Cargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        ListAllCallCount++;
        return Task.FromResult<IReadOnlyList<Cargo>>(Datos.Where(d => d.IsActive).ToList());
    }

    public Task UpdateAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var index = Datos.FindIndex(d => d.Id == cargo.Id);
        if (index >= 0)
        {
            Datos[index] = cargo;
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasActivePuestosAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        HasActivePuestosCallCount++;
        var hasPuestos = PuestosPorCargo.TryGetValue(cargoId, out var puestos)
            && puestos.Any(p => p.IsActive && !p.IsDeleted);
        return Task.FromResult(hasPuestos);
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCallCount++;
        var cargo = Datos.FirstOrDefault(d => d.Id == id);
        if (cargo is not null)
        {
            cargo.Activar();
            if (!Datos.Contains(cargo))
            {
                Datos.Add(cargo);
            }
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeNivelCargoRepo : INivelCargoRepository
{
    public List<NivelCargo> Datos { get; set; } = [];

    public Task<NivelCargo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<IReadOnlyList<NivelCargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<NivelCargo>>(Datos.ToList());
    }

    public Task<NivelCargo?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Codigo == codigo));
    }
}
