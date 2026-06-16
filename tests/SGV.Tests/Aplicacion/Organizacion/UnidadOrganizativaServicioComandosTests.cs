using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class UnidadOrganizativaServicioComandosTests
{
    private static readonly Guid UnidadId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PadreId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid HijoId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    private static readonly FakeTipoUnidadOrganizativaRepository FakeTipoRepo = new()
    {
        Datos =
        [
            new("Institucion", "Institución") { Id = TipoUnidadOrganizativaConstantes.InstitucionId },
            new("Facultad", "Facultad") { Id = TipoUnidadOrganizativaConstantes.FacultadId },
            new("Secretaria", "Secretaría") { Id = TipoUnidadOrganizativaConstantes.SecretariaId },
            new("Direccion", "Dirección") { Id = TipoUnidadOrganizativaConstantes.DireccionId },
            new("Departamento", "Departamento") { Id = TipoUnidadOrganizativaConstantes.DepartamentoId },
            new("Division", "División") { Id = TipoUnidadOrganizativaConstantes.DivisionId },
            new("Area", "Área") { Id = TipoUnidadOrganizativaConstantes.AreaId },
        ]
    };

    private static CrearUnidadOrganizativaRequest CrearRequest(string? codigo = null, Guid? padreId = null)
        => new(
            codigo ?? "GER",
            "Gerencia General",
            TipoUnidadOrganizativaConstantes.DireccionId,
            "Máxima autoridad ejecutiva",
            null,
            null,
            padreId);

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("GER", resultado.Value!.Codigo);
        Assert.Equal("Gerencia General", resultado.Value.Nombre);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_CodigoDuplicado_RetornaConflictoYSinGuardar()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest("GER"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest(
            "GER", "Gerencia General", Guid.NewGuid(), null, null, null, null);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("TipoUnidadNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_PadreInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(padreId: PadreId), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_VigenciaInvalida_RetornaValidacionYSinGuardar()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest(
            "GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId, null,
            new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1), null);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "GER-2", "Nueva Gerencia", TipoUnidadOrganizativaConstantes.AreaId, "Descripción actualizada", null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("GER-2", resultado.Value!.Codigo);
        Assert.Equal("Nueva Gerencia", resultado.Value.Nombre);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CodigoDuplicado_RetornaConflictoYSinGuardar()
    {
        var a = CrearUnidadActiva("GER-A", UnidadId);
        var b = CrearUnidadActiva("GER-B", PadreId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [a, b] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("GER-B", "A", TipoUnidadOrganizativaConstantes.AreaId, null, null, null);

        var resultado = await servicio.ActualizarAsync(UnidadId, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("GER", "G", Guid.NewGuid(), null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("TipoUnidadNoExiste", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_UnidadInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("GER", "G", TipoUnidadOrganizativaConstantes.AreaId, null, null, null);

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(), request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarUnidadPadreAsync_PadreValido_RetornaDtoYGuarda()
    {
        var unidad = CrearUnidadActiva("GER", UnidadId);
        var padre = CrearUnidadActiva("PADRE", PadreId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [unidad, padre] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CambiarUnidadPadreAsync(UnidadId, new CambiarUnidadPadreRequest(PadreId), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(PadreId, resultado.Value!.UnidadPadreId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarUnidadPadreAsync_PadrePropio_RetornaValidacionYSinGuardar()
    {
        var unidad = CrearUnidadActiva("GER", UnidadId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [unidad] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CambiarUnidadPadreAsync(UnidadId, new CambiarUnidadPadreRequest(UnidadId), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarUnidadPadreAsync_PadreDescendiente_RetornaConflictoYSinGuardar()
    {
        var padre = CrearUnidadActiva("PADRE", PadreId);
        var hijo = CrearUnidadActiva("HIJO", HijoId, PadreId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre, hijo] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CambiarUnidadPadreAsync(PadreId, new CambiarUnidadPadreRequest(HijoId), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarUnidadPadreAsync_PadreInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var unidad = CrearUnidadActiva("GER", UnidadId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [unidad] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CambiarUnidadPadreAsync(UnidadId, new CambiarUnidadPadreRequest(Guid.NewGuid()), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task EliminarAsync_UnidadExistente_RetornaExitoYGuarda()
    {
        var unidad = CrearUnidadActiva("GER", UnidadId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [unidad] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.EliminarAsync(UnidadId, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task EliminarAsync_UnidadInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.EliminarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    private static UnidadOrganizativa CrearUnidadActiva(string codigo, Guid? id = null, Guid? padreId = null)
    {
        var unidad = new UnidadOrganizativa(codigo, codigo, TipoUnidadOrganizativaConstantes.AreaId, padreId)
        {
            Id = id ?? Guid.NewGuid()
        };
        unidad.CambiarDatos(codigo, codigo, TipoUnidadOrganizativaConstantes.AreaId, null);
        return unidad;
    }
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

internal sealed class FakeUnidadOrganizativaWriteRepository : IUnidadOrganizativaRepository
{
    public List<UnidadOrganizativa> Datos { get; set; } = [];

    public Task AddAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
    {
        Datos.Add(unidad);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unidad = Datos.FirstOrDefault(d => d.Id == id);
        if (unidad is not null)
        {
            unidad.Desactivar();
            Datos.Remove(unidad);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        var duplicado = Datos.Any(d =>
            d.Codigo == codigo &&
            d.IsActive &&
            !d.IsDeleted &&
            d.Id != excludingId);
        return Task.FromResult(duplicado);
    }

    public Task<UnidadOrganizativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unidad = Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted);
        return Task.FromResult(unidad);
    }

    public Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unidad = Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted);
        return Task.FromResult(unidad);
    }

    public Task<bool> IsDescendantAsync(Guid candidateDescendantId, Guid ancestorId, CancellationToken cancellationToken = default)
    {
        var current = Datos.FirstOrDefault(d => d.Id == candidateDescendantId);
        while (current is not null && current.UnidadPadreId.HasValue)
        {
            if (current.UnidadPadreId == ancestorId)
            {
                return Task.FromResult(true);
            }

            current = Datos.FirstOrDefault(d => d.Id == current.UnidadPadreId.Value);
        }

        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<UnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<UnidadOrganizativa>>(Datos.Where(d => d.IsActive && !d.IsDeleted).ToList());
    }

    public Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
    {
        var index = Datos.FindIndex(d => d.Id == unidad.Id);
        if (index >= 0)
        {
            Datos[index] = unidad;
        }

        return Task.CompletedTask;
    }
}
