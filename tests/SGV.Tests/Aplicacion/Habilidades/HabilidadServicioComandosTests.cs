using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Habilidades.Comandos;
using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

public sealed class HabilidadServicioComandosTests
{
    private static readonly Guid HabilidadIdActiva = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid HabilidadIdConflicto = Guid.Parse("50000000-0000-0000-0000-000000000002");

    private static CrearHabilidadRequest CrearRequest(string? codigo = null) => new(
        codigo ?? "COM01",
        "Comunicación",
        "Blandas",
        "Capacidad de comunicar");

    // ── CrearAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var repo = new FakeHabilidadWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("COM01", resultado.Value!.Codigo);
        Assert.Equal("Comunicación", resultado.Value.Nombre);
        Assert.Equal("Blandas", resultado.Value.Categoria);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar()
    {
        var existente = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest("COM01"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(HabilidadErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_CodigoDuplicadoEnHabilidadInactiva_RetornaExito()
    {
        // Una habilidad inactiva con el mismo código NO bloquea la creación
        // porque la unicidad es solo entre habilidades activas.
        var inactiva = CrearHabilidadInactiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [inactiva] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest("COM01"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_CodigoVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var repo = new FakeHabilidadWriteRepository
        {
            Datos = [CrearHabilidadActiva("COM01", HabilidadIdActiva)]
        };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearHabilidadRequest("", "Nombre", null, null);

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
    public async Task CrearAsync_MultiplesErrores_EmiteTodasLasClavesCamelCase()
    {
        var repo = new FakeHabilidadWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearHabilidadRequest("", "", null, null);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Contains("nombre", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ActualizarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda()
    {
        var existente = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarHabilidadRequest("COM01", "Comunicación Efectiva", "Blandas/Avanzadas", "Nueva descripción"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Comunicación Efectiva", resultado.Value!.Nombre);
        Assert.Equal("COM01", resultado.Value.Codigo);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_HabilidadInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeHabilidadWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(),
            new ActualizarHabilidadRequest("COM01", "Nombre"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(HabilidadErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("HabilidadNoEncontrada", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoValido_PersisteYCambiaCodigo()
    {
        var existente = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarHabilidadRequest("COM02", "Comunicación Efectiva", null, null), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("COM02", resultado.Value!.Codigo);
        Assert.Equal("COM02", existente.Codigo);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_MismoCodigo_NoSeTrataComoDuplicado()
    {
        // El pre-check debe excluir el id de la habilidad que se está editando,
        // de modo que reenviar el mismo Codigo no dispare conflicto.
        var existente = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarHabilidadRequest("COM01", "Comunicación Actualizada", null, null), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("COM01", resultado.Value!.Codigo);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar()
    {
        var objetivo = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var otra = CrearHabilidadActiva("COM02", HabilidadIdConflicto);
        var repo = new FakeHabilidadWriteRepository { Datos = [objetivo, otra] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(objetivo.Id,
            new ActualizarHabilidadRequest("COM02", "Nombre nuevo", null, null), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(HabilidadErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal("COM01", objetivo.Codigo);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoDeEliminada_PermiteReutilizar()
    {
        // Una habilidad con soft-delete no bloquea la unicidad activa.
        var objetivo = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var eliminada = CrearHabilidadInactiva("COM02", HabilidadIdConflicto);
        var repo = new FakeHabilidadWriteRepository { Datos = [objetivo, eliminada] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(objetivo.Id,
            new ActualizarHabilidadRequest("COM02", "Comunicación renombrada", null, null), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("COM02", objetivo.Codigo);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos()
    {
        var existente = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarHabilidadRequest("", "Comunicación", null, null), default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Equal(0, repo.GetByIdForUpdateCallCount);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_DbUpdateExceptionPorIndiceUnicoEnSaveChanges_TraduceACodigoDuplicado()
    {
        // Safety-net: el pre-check de ExistsActiveCodeAsync cubre el camino
        // feliz, pero existe una ventana de carrera entre el check y
        // SaveChangesAsync. Si el índice IX_Habilidades_ActiveCodigoUnique
        // se dispara en SaveChanges (otra transacción escribió el mismo
        // Codigo activo entre medio), el catch con
        // IsActiveCodigoUniqueViolation debe mapear a CodigoDuplicado /
        // Conflict para no exponer un 500 genérico al cliente.
        var existente = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [existente] };
        // El pre-check NO detecta duplicado (otra habilidad entra en carrera
        // después del check), pero SaveChangesAsync sí lo detecta.
        var innerDup = new Exception(
            "Duplicate entry 'COM02' for key 'habilidades.IX_Habilidades_ActiveCodigoUnique'");
        var uow = new FakeThrowingDbUpdateUnitOfWork(
            new DbUpdateException("Duplicate entry", innerDup));
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarHabilidadRequest("COM02", "Comunicación renombrada", null, null), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(HabilidadErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_NombreVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var existente = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarHabilidadRequest("COM01", "", null, null), default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombre", resultado.FieldErrors!.Keys);
        Assert.Equal(0, repo.GetByIdForUpdateCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── DesactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task DesactivarAsync_HabilidadExistente_RetornaExitoYGuarda()
    {
        var habilidad = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [habilidad] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.DesactivarAsync(habilidad.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task DesactivarAsync_HabilidadInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeHabilidadWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.DesactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(HabilidadErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ReactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ReactivarAsync_HabilidadDesactivada_RetornaExitoYGuarda()
    {
        var habilidad = CrearHabilidadDesactivada("COM01", HabilidadIdActiva);
        var repo = new FakeHabilidadWriteRepository { Datos = [habilidad] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ReactivarAsync(habilidad.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_HabilidadInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeHabilidadWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ReactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(HabilidadErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_CodigoConflictivo_RetornaConflictoYSinGuardar()
    {
        var activo = CrearHabilidadActiva("COM01", HabilidadIdActiva);
        var desactivado = CrearHabilidadDesactivada("COM01", HabilidadIdConflicto);
        var repo = new FakeHabilidadWriteRepository { Datos = [activo, desactivado] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ReactivarAsync(desactivado.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(HabilidadErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CodigoDuplicado", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static HabilidadServicioComandos CrearServicio(
        IHabilidadRepository repo,
        IUnitOfWork uow)
    {
        return new HabilidadServicioComandos(repo, uow);
    }

    private static Habilidad CrearHabilidadActiva(string codigo, Guid? id = null)
    {
        var habilidad = new Habilidad(codigo, $"Nombre {codigo}")
        {
            Id = id ?? Guid.NewGuid()
        };
        return habilidad;
    }

    private static Habilidad CrearHabilidadInactiva(string codigo, Guid? id = null)
    {
        var habilidad = CrearHabilidadActiva(codigo, id);
        habilidad.Desactivar();
        return habilidad;
    }

    private static Habilidad CrearHabilidadDesactivada(string codigo, Guid? id = null)
    {
        var habilidad = CrearHabilidadActiva(codigo, id);
        habilidad.Desactivar();
        return habilidad;
    }
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakeHabilidadWriteRepository : IHabilidadRepository
{
    public List<Habilidad> Datos { get; set; } = [];

    public int AddCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public int ExistsActiveCodeCallCount { get; private set; }
    public int GetByIdCallCount { get; private set; }
    public int GetByIdForUpdateCallCount { get; private set; }
    public int GetByIdIncludingDeletedCallCount { get; private set; }
    public int ListAllCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int ReactivateCallCount { get; private set; }

    public Task AddAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Datos.Add(habilidad);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        var habilidad = Datos.FirstOrDefault(d => d.Id == id);
        if (habilidad is not null)
        {
            habilidad.Desactivar();
            Datos.Remove(habilidad);
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

    public Task<Habilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted));
    }

    public Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted));
    }

    public Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdIncludingDeletedCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id));
    }

    public Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        ListAllCallCount++;
        return Task.FromResult<IReadOnlyList<Habilidad>>(Datos.Where(d => d.IsActive && !d.IsDeleted).ToList());
    }

    public Task UpdateAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var index = Datos.FindIndex(d => d.Id == habilidad.Id);
        if (index >= 0)
        {
            Datos[index] = habilidad;
        }
        return Task.CompletedTask;
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCallCount++;
        var habilidad = Datos.FirstOrDefault(d => d.Id == id);
        if (habilidad is not null)
        {
            habilidad.Activar();
            if (!Datos.Contains(habilidad))
            {
                Datos.Add(habilidad);
            }
        }
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Habilidad> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        HabilidadSegmentoListado segmento = HabilidadSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(IReadOnlyList<Habilidad>, int)>(([], 0));
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

internal sealed class FakeThrowingDbUpdateUnitOfWork : IUnitOfWork
{
    private readonly DbUpdateException _exceptionToThrow;

    public FakeThrowingDbUpdateUnitOfWork(DbUpdateException exceptionToThrow)
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
