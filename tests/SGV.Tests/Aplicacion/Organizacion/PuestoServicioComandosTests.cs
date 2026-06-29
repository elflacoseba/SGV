using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class PuestoServicioComandosTests
{
    private static readonly Guid PuestoId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid UnidadIdValida = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid CargoIdValido = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid UnidadIdInexistente = Guid.Parse("99999999-0000-0000-0000-000000000001");
    private static readonly Guid CargoIdInexistente = Guid.Parse("99999999-0000-0000-0000-000000000002");
    private static readonly Guid PuestoSuperiorIdValido = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid PuestoSuperiorIdInexistente = Guid.Parse("99999999-0000-0000-0000-000000000003");
    private static readonly Guid PuestoSuperiorIdInactivo = Guid.Parse("c0000000-0000-0000-0000-000000000003");

    private static CrearPuestoRequest CrearRequest(
        string? codigo = null,
        Guid? unidadId = null,
        Guid? cargoId = null,
        Guid? puestoSuperiorId = null)
        => new(
            codigo ?? "GER-001",
            "Gerente General",
            unidadId ?? UnidadIdValida,
            cargoId ?? CargoIdValido,
            puestoSuperiorId,
            "Máxima autoridad ejecutiva");

    // ── CrearAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("GER-001", resultado.Value!.Codigo);
        Assert.Equal("Gerente General", resultado.Value.Nombre);
        Assert.Equal(UnidadIdValida, resultado.Value.UnidadOrganizativaId);
        Assert.Equal(CargoIdValido, resultado.Value.CargoId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar()
    {
        var existente = CrearPuestoActivo("GER-001");
        var puestoRepo = new FakePuestoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest("GER-001"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_UnidadOrganizativaInexistente_RetornaValidacionYSinGuardar()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var unidadRepo = new FakeUnidadOrganizativaReadRepository();
        var servicio = CrearServicio(puestoRepo, uow, unidadRepo: unidadRepo);

        var resultado = await servicio.CrearAsync(CrearRequest(unidadId: UnidadIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("UnidadOrganizativaNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_CargoInexistente_RetornaValidacionYSinGuardar()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var cargoRepo = new FakeCargoReadRepository();
        var servicio = CrearServicio(puestoRepo, uow, cargoRepo: cargoRepo);

        var resultado = await servicio.CrearAsync(CrearRequest(cargoId: CargoIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("CargoNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PuestoSuperiorValido_RetornaExitoYGuarda()
    {
        var superior = CrearPuestoActivo("SUP-001", PuestoSuperiorIdValido);
        var puestoRepo = new FakePuestoWriteRepository { Datos = [superior] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(puestoSuperiorId: PuestoSuperiorIdValido), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(PuestoSuperiorIdValido, resultado.Value!.PuestoSuperiorId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PuestoSuperiorInexistente_RetornaValidacionYSinGuardar()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(puestoSuperiorId: PuestoSuperiorIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("PuestoSuperiorNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PuestoSuperiorInactivo_RetornaValidacionYSinGuardar()
    {
        var inactivo = CrearPuestoInactivo("SUP-INACT", PuestoSuperiorIdInactivo);
        var puestoRepo = new FakePuestoWriteRepository { Datos = [inactivo] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(puestoSuperiorId: PuestoSuperiorIdInactivo), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("PuestoSuperiorNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ActualizarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda()
    {
        var existente = CrearPuestoActivo("GER-001");
        var puestoRepo = new FakePuestoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarPuestoRequest("Gerente General Actualizado", "Nueva descripción"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Gerente General Actualizado", resultado.Value!.Nombre);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(),
            new ActualizarPuestoRequest("Nombre"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoSuperiorValido_RetornaDtoActualizadoYGuarda()
    {
        var existente = CrearPuestoActivo("GER-001");
        var superior = CrearPuestoActivo("SUP-001", PuestoSuperiorIdValido);
        var puestoRepo = new FakePuestoWriteRepository { Datos = [existente, superior] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarPuestoRequest("Gerente General Actualizado", null, PuestoSuperiorIdValido), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Gerente General Actualizado", resultado.Value!.Nombre);
        Assert.Equal(PuestoSuperiorIdValido, resultado.Value.PuestoSuperiorId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoSuperiorInexistente_RetornaValidacionYSinGuardar()
    {
        var existente = CrearPuestoActivo("GER-001");
        var puestoRepo = new FakePuestoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarPuestoRequest("Nombre", null, PuestoSuperiorIdInexistente), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("PuestoSuperiorNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoSuperiorInactivo_RetornaValidacionYSinGuardar()
    {
        var existente = CrearPuestoActivo("GER-001");
        var inactivo = CrearPuestoInactivo("SUP-INACT", PuestoSuperiorIdInactivo);
        var puestoRepo = new FakePuestoWriteRepository { Datos = [existente, inactivo] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarPuestoRequest("Nombre", null, PuestoSuperiorIdInactivo), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("PuestoSuperiorNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PuestoSuperiorAutorreferencia_RetornaValidacionYSinGuardar()
    {
        var existente = CrearPuestoActivo("GER-001");
        var puestoRepo = new FakePuestoWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarPuestoRequest("Nombre", null, existente.Id), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("PuestoSuperiorInvalido", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── DesactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task DesactivarAsync_PuestoExistente_RetornaExitoYGuarda()
    {
        var puesto = CrearPuestoActivo("GER-001");
        var puestoRepo = new FakePuestoWriteRepository { Datos = [puesto] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.DesactivarAsync(puesto.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task DesactivarAsync_PuestoInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.DesactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ReactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ReactivarAsync_PuestoDesactivado_RetornaExitoYGuarda()
    {
        var puesto = CrearPuestoDesactivado("GER-001");
        var puestoRepo = new FakePuestoWriteRepository { Datos = [puesto] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(puesto.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_PuestoInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_CodigoConflictivo_RetornaConflictoYSinGuardar()
    {
        var activo = CrearPuestoActivo("GER-001", Guid.Parse("c0000000-0000-0000-0000-0000000000A1"));
        var desactivado = CrearPuestoDesactivado("GER-001", PuestoId);
        var puestoRepo = new FakePuestoWriteRepository { Datos = [activo, desactivado] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);

        var resultado = await servicio.ReactivarAsync(desactivado.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PuestoErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Short-circuit: validation before repo calls ────────────

    [Fact]
    public async Task CrearAsync_CodigoVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var puestoRepo = new FakePuestoWriteRepository
        {
            Datos = [CrearPuestoActivo("GER-001")]
        };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);
        var request = new CrearPuestoRequest("", "Nombre", Guid.NewGuid(), Guid.NewGuid());

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Codigo", resultado.FieldErrors.Keys);
        Assert.Equal(0, puestoRepo.ExistsActiveCodeCallCount);
        Assert.Equal(0, puestoRepo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_MultiplesErrores_EmiteTodasLasClavesCamelCase()
    {
        var puestoRepo = new FakePuestoWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(puestoRepo, uow);
        var request = new CrearPuestoRequest("", "", Guid.Empty, Guid.Empty);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Contains("nombre", resultado.FieldErrors.Keys);
        Assert.Contains("unidadOrganizativaId", resultado.FieldErrors!.Keys);
        Assert.Contains("cargoId", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Codigo", resultado.FieldErrors.Keys);
        Assert.DoesNotContain("Nombre", resultado.FieldErrors.Keys);
        Assert.Equal(0, puestoRepo.ExistsActiveCodeCallCount);
        Assert.Equal(0, puestoRepo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static PuestoServicioComandos CrearServicio(
        IPuestoRepository puestoRepo,
        IUnitOfWork uow,
        IUnidadOrganizativaRepository? unidadRepo = null,
        ICargoRepository? cargoRepo = null)
    {
        var fakeUnidadRepo = unidadRepo ?? new FakeUnidadOrganizativaReadRepository
        {
            Datos = [CrearUnidadMock()]
        };
        var fakeCargoRepo = cargoRepo ?? new FakeCargoReadRepository
        {
            Datos = [CrearCargoMock()]
        };
        return new PuestoServicioComandos(puestoRepo, fakeUnidadRepo, fakeCargoRepo, uow);
    }

    private static UnidadOrganizativa CrearUnidadMock()
    {
        var unidad = new UnidadOrganizativa("GER", "Gerencia General", Guid.Parse("70000000-0000-0000-0000-000000000001"))
        {
            Id = UnidadIdValida
        };
        return unidad;
    }

    private static Cargo CrearCargoMock()
    {
        var cargo = new Cargo("DIRECTOR", "Director", Guid.Parse("70000000-0000-0000-0000-000000000001"))
        {
            Id = CargoIdValido
        };
        return cargo;
    }

    private static Puesto CrearPuestoActivo(string codigo, Guid? id = null)
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, codigo, codigo)
        {
            Id = id ?? Guid.NewGuid()
        };
        return puesto;
    }

    private static Puesto CrearPuestoDesactivado(string codigo, Guid? id = null)
    {
        var puesto = CrearPuestoActivo(codigo, id);
        puesto.Desactivar();
        return puesto;
    }

    private static Puesto CrearPuestoInactivo(string codigo, Guid? id = null)
    {
        var puesto = CrearPuestoActivo(codigo, id);
        puesto.Desactivar();
        return puesto;
    }
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakePuestoWriteRepository : IPuestoRepository
{
    public List<Puesto> Datos { get; set; } = [];

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

    public Task AddAsync(Puesto puesto, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Datos.Add(puesto);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        var puesto = Datos.FirstOrDefault(d => d.Id == id);
        if (puesto is not null)
        {
            puesto.Desactivar();
            Datos.Remove(puesto);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveCodeCallCount++;
        var duplicado = Datos.Any(d =>
            d.Codigo == codigo &&
            d.IsActive &&
            !d.IsDeleted &&
            d.Id != excludingId);
        return Task.FromResult(duplicado);
    }

    public Task<Puesto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted));
    }

    public Task<Puesto?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted));
    }

    public Task<Puesto?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdIncludingDeletedCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id));
    }

    public Task<IReadOnlyList<Puesto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        ListAllCallCount++;
        return Task.FromResult<IReadOnlyList<Puesto>>(Datos.Where(d => d.IsActive && !d.IsDeleted).ToList());
    }

    public Task UpdateAsync(Puesto puesto, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var index = Datos.FindIndex(d => d.Id == puesto.Id);
        if (index >= 0)
        {
            Datos[index] = puesto;
        }

        return Task.CompletedTask;
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCallCount++;
        var puesto = Datos.FirstOrDefault(d => d.Id == id);
        if (puesto is not null)
        {
            puesto.Activar();
            if (!Datos.Contains(puesto))
            {
                Datos.Add(puesto);
            }
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeUnidadOrganizativaReadRepository : IUnidadOrganizativaRepository
{
    public List<UnidadOrganizativa> Datos { get; set; } = [];

    public Task<UnidadOrganizativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted));

    public Task<IReadOnlyList<UnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UnidadOrganizativa>>(Datos.Where(d => d.IsActive && !d.IsDeleted).ToList());

    public Task AddAsync(UnidadOrganizativa unidad, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UnidadOrganizativa?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasActiveChildrenAsync(Guid unidadId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasActivePuestosAsync(Guid unidadId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> IsDescendantAsync(Guid candidateDescendantId, Guid ancestorId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<UnidadOrganizativa>> ListTreeAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<(IReadOnlyList<UnidadOrganizativa> Items, int TotalCount)> QueryAsync(string? search, Guid? tipoUnidadOrganizativaId, Guid? unidadPadreId, DateOnly? vigenteEn, int page, int pageSize, UnidadOrganizativaSegmentoListado segmento = UnidadOrganizativaSegmentoListado.Activas, CancellationToken ct = default) => throw new NotSupportedException();
    public Task ReactivateAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class FakeCargoReadRepository : ICargoRepository
{
    public List<Cargo> Datos { get; set; } = [];

    public Task<Cargo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted));

    public Task<IReadOnlyList<Cargo>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Cargo>>(Datos.Where(d => d.IsActive && !d.IsDeleted).ToList());

    public Task AddAsync(Cargo cargo, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Cargo?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Cargo?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasActivePuestosAsync(Guid cargoId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task ReactivateAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateAsync(Cargo cargo, CancellationToken ct = default) => throw new NotSupportedException();
}
