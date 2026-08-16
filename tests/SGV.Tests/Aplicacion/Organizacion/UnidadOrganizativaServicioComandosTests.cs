using SGV.Aplicacion.Comun.Persistencia;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
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

    private static CrearUnidadOrganizativaRequest CrearRequest(string? codigo = null, Guid? padreId = null, Guid? tipoId = null)
        => new(
            codigo ?? "GER",
            "Gerencia General",
            tipoId ?? TipoUnidadOrganizativaConstantes.InstitucionId,
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
    public async Task CrearAsync_DatosValidos_ConPadreJerarquiaValida_RetornaDtoYGuarda()
    {
        var padre = CrearUnidadActiva("INST", PadreId, tipoId: TipoUnidadOrganizativaConstantes.InstitucionId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest(
            "FAC", "Facultad de Prueba",
            TipoUnidadOrganizativaConstantes.FacultadId, null, null, null, PadreId);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("FAC", resultado.Value!.Codigo);
        Assert.Equal("Facultad de Prueba", resultado.Value.Nombre);
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
            "Nueva Gerencia", TipoUnidadOrganizativaConstantes.InstitucionId, "Descripción actualizada", null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        // Codigo se preserva: el request no acepta codigo y Actualizar no lo expone.
        Assert.Equal("GER", resultado.Value!.Codigo);
        Assert.Equal("Nueva Gerencia", resultado.Value.Nombre);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_DatosValidos_JerarquiaValida_RetornaDtoActualizadoYGuarda()
    {
        var padre = CrearUnidadActiva("INST", PadreId, tipoId: TipoUnidadOrganizativaConstantes.InstitucionId);
        var existente = CrearUnidadActiva("FAC", UnidadId, PadreId, TipoUnidadOrganizativaConstantes.FacultadId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre, existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Nueva Facultad", TipoUnidadOrganizativaConstantes.FacultadId, "Descripción actualizada", null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("FAC", resultado.Value!.Codigo);
        Assert.Equal("Nueva Facultad", resultado.Value.Nombre);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PreservaCodigoOriginal()
    {
        // Regresion critica: aunque el request ya no acepta Codigo, garantizamos
        // que el Codigo persistido es exactamente el original. Si el servicio
        // recibiera un codigo "HACKED" por contrato previo, debe seguir devolviendo
        // el codigo original. El test crea la unidad con "RECT" y verifica que el
        // resultado de ActualizarAsync mantiene "RECT" aunque el resto cambie.
        var existente = CrearUnidadActiva("RECT");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Rectorado Modificado", TipoUnidadOrganizativaConstantes.InstitucionId,
            "Nueva descripcion", null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("RECT", resultado.Value!.Codigo);
        Assert.Equal("Rectorado Modificado", resultado.Value.Nombre);
        // La entidad persistida en el repo debe seguir teniendo el Codigo original.
        var persistida = repo.Datos.Single(u => u.Id == existente.Id);
        Assert.Equal("RECT", persistida.Codigo);
    }

    [Fact]
    public async Task ActualizarAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("G", Guid.NewGuid(), null, null, null, null);

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
        var request = new ActualizarUnidadOrganizativaRequest("G", TipoUnidadOrganizativaConstantes.AreaId, null, null, null, null);

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(), request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ===== WU-2: PUT valida integridad del padre (issue #277) =====
    // Spec: "PUT con padre inexistente retorna 404 sin persistir"
    //       "PUT con padre descendiente retorna 409 sin persistir"
    //       "PUT con padre válido persiste normalmente"
    //       "PUT con padre null persiste normalmente"

    [Fact]
    public async Task ActualizarAsync_PadreInexistente_RetornaNotFoundYSinGuardar()
    {
        var existente = CrearUnidadActiva("GER", UnidadId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Gerencia Actualizada",
            TipoUnidadOrganizativaConstantes.InstitucionId,
            null, null, null, Guid.NewGuid());

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal("UnidadPadreNoEncontrada", resultado.Error.Code);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PadreDescendiente_RetornaConflictYSinGuardar()
    {
        // padre → hijo (hijo es descendiente de padre). Intentar cambiar padre del padre a hijo = ciclo.
        var padre = CrearUnidadActiva("PADRE", PadreId);
        var hijo = CrearUnidadActiva("HIJO", HijoId, PadreId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre, hijo] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Padre a reasignar",
            TipoUnidadOrganizativaConstantes.InstitucionId,
            null, null, null, HijoId);

        var resultado = await servicio.ActualizarAsync(PadreId, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("CicloJerarquico", resultado.Error.Code);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PadreValidoYPersisteNormalmente()
    {
        // padre != existente, y no es descendiente de existente.
        var padre = CrearUnidadActiva("PADRE-OK", PadreId);
        var existente = CrearUnidadActiva("EXIST", UnidadId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre, existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Exist actualizado",
            TipoUnidadOrganizativaConstantes.AreaId,
            null, null, null, PadreId);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(PadreId, resultado.Value!.UnidadPadreId);
        Assert.Equal(1, repo.UpdateCallCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PadreNull_PersisteNormalmente()
    {
        var existente = CrearUnidadActiva("EXIST", UnidadId, unidadPadreId: PadreId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Exist sin padre",
            TipoUnidadOrganizativaConstantes.AreaId,
            null, null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value!.UnidadPadreId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CambiarUnidadPadreAsync_PadreValido_RetornaDtoYGuarda()
    {
        var unidad = CrearUnidadActiva("FAC", UnidadId, tipoId: TipoUnidadOrganizativaConstantes.FacultadId);
        var padre = CrearUnidadActiva("INST", PadreId, tipoId: TipoUnidadOrganizativaConstantes.InstitucionId);
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

    // ===== Task 1.1: Delete protection — active children / puestos =====

    [Fact]
    public async Task EliminarAsync_ConHijasActivas_RetornaConflictoYSinGuardar()
    {
        var padre = CrearUnidadActiva("PADRE", UnidadId);
        var hijo = CrearUnidadActiva("HIJO", HijoId, UnidadId);
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre, hijo] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.EliminarAsync(UnidadId, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("UnidadConHijasActivas", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task EliminarAsync_ConPuestosActivos_RetornaConflictoYSinGuardar()
    {
        var unidad = CrearUnidadActiva("GER", UnidadId);
        var repo = new FakeUnidadOrganizativaWriteRepository
        {
            Datos = [unidad],
            PuestosPorUnidad = new Dictionary<Guid, List<Puesto>>
            {
                [UnidadId] = [new Puesto(UnidadId, Guid.NewGuid(), "PUESTO-001", "Puesto Activo")]
            }
        };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.EliminarAsync(UnidadId, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("UnidadConPuestosActivos", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ===== Task 1.2: Reactivate protection =====

    [Fact]
    public async Task ReactivarAsync_PadreInactivo_RetornaConflictoYSinGuardar()
    {
        var padre = new UnidadOrganizativa("PADRE", "Padre Inactivo", TipoUnidadOrganizativaConstantes.InstitucionId, null, null)
        {
            Id = PadreId
        };
        padre.Desactivar(); // padre inactivo
        var hijo = new UnidadOrganizativa("HIJO", "Hijo", TipoUnidadOrganizativaConstantes.FacultadId, null, PadreId)
        {
            Id = HijoId
        };
        hijo.Desactivar(); // hijo también inactivo
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre, hijo] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.ReactivarAsync(HijoId, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal("PadreInactivo", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_PadreActivo_RetornaExitoYGuarda()
    {
        var padre = new UnidadOrganizativa("PADRE", "Padre Activo", TipoUnidadOrganizativaConstantes.InstitucionId, null, null)
        {
            Id = PadreId
        };
        // padre stays active (default)
        var hijo = new UnidadOrganizativa("HIJO", "Hijo", TipoUnidadOrganizativaConstantes.FacultadId, null, PadreId)
        {
            Id = HijoId
        };
        hijo.Desactivar();
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [padre, hijo] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.ReactivarAsync(HijoId, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_ConTiposArbitrarios_RetornaDtoYGuarda()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var padre = CrearUnidadActiva("DIR", PadreId, tipoId: TipoUnidadOrganizativaConstantes.DireccionId);
        repo.Datos.Add(padre);
        var request = new CrearUnidadOrganizativaRequest(
            "FAC", "Facultad de Prueba",
            TipoUnidadOrganizativaConstantes.FacultadId,
            null, null, null, PadreId);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(PadreId, resultado.Value!.UnidadPadreId);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_ConVigenciaIndependienteDelPadre_RetornaDtoYGuarda()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var padre = CrearUnidadActiva("PADRE", PadreId);
        padre.DefinirVigencia(new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));
        repo.Datos.Add(padre);
        // Hija vigente DESPUÉS del rango del padre
        var request = new CrearUnidadOrganizativaRequest(
            "HIJA", "Hija fuera de rango",
            TipoUnidadOrganizativaConstantes.FacultadId,
            null,
            new DateOnly(2025, 7, 1), new DateOnly(2025, 12, 31),
            PadreId);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal(new DateOnly(2025, 7, 1), resultado.Value!.VigenteDesde);
        Assert.Equal(new DateOnly(2025, 12, 31), resultado.Value.VigenteHasta);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    // ---- Short-circuit: validation before repository checks ----

    [Fact]
    public async Task CrearAsync_CodigoVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository
        {
            Datos = [CrearUnidadActiva("GER")]
        };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest("", "Nombre", Guid.NewGuid());

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_NombreVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository
        {
            Datos = [CrearUnidadActiva("GER")]
        };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest("NUEVO", "", Guid.NewGuid());

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombre", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_MultiplesErrores_RetornaTodosLosCampos()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest("", "", Guid.Empty);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Contains("nombre", resultado.FieldErrors.Keys);
        Assert.Contains("tipoUnidadOrganizativaId", resultado.FieldErrors.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_NombreVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("", TipoUnidadOrganizativaConstantes.AreaId, null, null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombre", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_RequestInvalidoNoBuscaUnidad()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository(); // empty — no data
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("", TipoUnidadOrganizativaConstantes.AreaId, null, null, null, null);

        // Id is irrelevant because shape validation fires before GetByIdForUpdateAsync
        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(), request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombre", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ---- Remediation (verify-report CRITICAL 1 + 2):
    //      camelCase contract for FieldErrors + zero repo calls on invalid shape ----

    [Fact]
    public async Task CrearAsync_CodigoVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository
        {
            Datos = [CrearUnidadActiva("GER")]
        };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest("", "Nombre", Guid.NewGuid());

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        // CRITICAL 1: HTTP contract demands lowercase keys.
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Codigo", resultado.FieldErrors.Keys);
        // CRITICAL 2: short-circuit must avoid repository/business calls.
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdCallCount);
        Assert.Equal(0, repo.GetByIdForUpdateCallCount);
        Assert.Equal(0, repo.IsDescendantCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_NombreVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository
        {
            Datos = [CrearUnidadActiva("GER")]
        };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest("NUEVO", "", Guid.NewGuid());

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombre", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Nombre", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_TipoUnidadOrganizativaIdVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository
        {
            Datos = [CrearUnidadActiva("GER")]
        };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest("GER", "Gerencia", Guid.Empty);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("tipoUnidadOrganizativaId", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("TipoUnidadOrganizativaId", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_MultiplesErrores_EmiteTodasLasClavesCamelCaseYSinConsultarRepos()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest("", "", Guid.Empty);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Contains("nombre", resultado.FieldErrors.Keys);
        Assert.Contains("tipoUnidadOrganizativaId", resultado.FieldErrors.Keys);
        // No PascalCase leakage at all.
        Assert.DoesNotContain("Codigo", resultado.FieldErrors.Keys);
        Assert.DoesNotContain("Nombre", resultado.FieldErrors.Keys);
        Assert.DoesNotContain("TipoUnidadOrganizativaId", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_NombreVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("", TipoUnidadOrganizativaConstantes.AreaId, null, null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombre", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Nombre", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.GetByIdForUpdateCallCount);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdCallCount);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_TipoUnidadOrganizativaIdVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("Gerencia", Guid.Empty, null, null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("tipoUnidadOrganizativaId", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("TipoUnidadOrganizativaId", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.GetByIdForUpdateCallCount);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdCallCount);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ===== Issue #279: DTO de respuesta con navegaciones hidratadas =====
    // Spec: "POST/PUT/PATCH /{id}/unidad-padre/PATCH /{id}/reactivar deben
    //        devolver tipoUnidadNombre, unidadPadreCodigo y unidadPadreNombre
    //        correctos, no strings vacíos ni nulls, cuando los IDs
    //        referenciados existen."

    private static FakeUnidadOrganizativaWriteRepository RepoConTipo()
    {
        // El fake simula el `Include` del repo de producción sólo cuando
        // TipoRepo está seteado. Los tests de issue #279 lo asignan para
        // que la rehidratación post-save exponga las navegaciones en el DTO.
        return new FakeUnidadOrganizativaWriteRepository { TipoRepo = FakeTipoRepo };
    }

    [Fact]
    public async Task CrearAsync_DtoIncluyeTipoUnidadNombreYPadreSiExisten()
    {
        var padre = CrearUnidadActiva("PADRE-279", PadreId, tipoId: TipoUnidadOrganizativaConstantes.InstitucionId);
        var repo = RepoConTipo();
        repo.Datos.Add(padre);
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new CrearUnidadOrganizativaRequest(
            "HIJO-279", "Hijo 279",
            TipoUnidadOrganizativaConstantes.FacultadId, null, null, null, PadreId);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        // tipoUnidadOrganizativaId=FacultadId -> Nombre="Facultad"
        Assert.Equal("Facultad", resultado.Value!.TipoUnidadNombre);
        Assert.Equal(padre.Codigo, resultado.Value.UnidadPadreCodigo);
        Assert.Equal(padre.Nombre, resultado.Value.UnidadPadreNombre);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_CambiaTipo_RetornaNombreTipoNuevoEnDto()
    {
        var existente = CrearUnidadActiva("EXIST-279", UnidadId, tipoId: TipoUnidadOrganizativaConstantes.InstitucionId);
        var repo = RepoConTipo();
        repo.Datos.Add(existente);
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Exist actualizado",
            TipoUnidadOrganizativaConstantes.DireccionId,
            null, null, null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        // La nav stale apuntaría a "Institución" (el tipo original). El re-fetch
        // vía GetByIdAsync debe traer el nombre del nuevo tipo.
        Assert.Equal("Dirección", resultado.Value!.TipoUnidadNombre);
        Assert.Equal(TipoUnidadOrganizativaConstantes.DireccionId, resultado.Value.TipoUnidadOrganizativaId);
    }

    [Fact]
    public async Task ActualizarAsync_CambiaPadre_RetornaCodigoYNombrePadreNuevoEnDto()
    {
        var padre = CrearUnidadActiva("PADRE-279", PadreId, tipoId: TipoUnidadOrganizativaConstantes.InstitucionId);
        var existente = CrearUnidadActiva("EXIST-279", UnidadId);
        var repo = RepoConTipo();
        repo.Datos.Add(padre);
        repo.Datos.Add(existente);
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest(
            "Exist con padre",
            TipoUnidadOrganizativaConstantes.AreaId,
            null, null, null, PadreId);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(padre.Codigo, resultado.Value!.UnidadPadreCodigo);
        Assert.Equal(padre.Nombre, resultado.Value.UnidadPadreNombre);
        Assert.Equal(PadreId, resultado.Value.UnidadPadreId);
    }

    [Fact]
    public async Task CambiarUnidadPadreAsync_Exitoso_DtoIncluyeCodigoYNombreNuevoPadre()
    {
        var padre = CrearUnidadActiva("NUEVO-PADRE", PadreId, tipoId: TipoUnidadOrganizativaConstantes.InstitucionId);
        var unidad = CrearUnidadActiva("EXIST", UnidadId, tipoId: TipoUnidadOrganizativaConstantes.FacultadId);
        var repo = RepoConTipo();
        repo.Datos.Add(unidad);
        repo.Datos.Add(padre);
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.CambiarUnidadPadreAsync(UnidadId, new CambiarUnidadPadreRequest(PadreId), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(padre.Codigo, resultado.Value!.UnidadPadreCodigo);
        Assert.Equal(padre.Nombre, resultado.Value.UnidadPadreNombre);
    }

    [Fact]
    public async Task ReactivarAsync_Exitoso_DtoIncluyeNavegacionesHidratas()
    {
        var padre = new UnidadOrganizativa("PADRE", "Padre Activo", TipoUnidadOrganizativaConstantes.InstitucionId, null, null)
        {
            Id = PadreId
        };
        var hijo = new UnidadOrganizativa("HIJO", "Hijo 279", TipoUnidadOrganizativaConstantes.FacultadId, null, PadreId)
        {
            Id = HijoId
        };
        hijo.Desactivar();
        var repo = RepoConTipo();
        repo.Datos.Add(padre);
        repo.Datos.Add(hijo);
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);

        var resultado = await servicio.ReactivarAsync(HijoId, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Facultad", resultado.Value!.TipoUnidadNombre);
        Assert.Equal(padre.Codigo, resultado.Value.UnidadPadreCodigo);
        Assert.Equal(padre.Nombre, resultado.Value.UnidadPadreNombre);
    }

    private static UnidadOrganizativa CrearUnidadActiva(
        string codigo, Guid? id = null, Guid? unidadPadreId = null, Guid? tipoId = null)
    {
        var tipo = tipoId ?? TipoUnidadOrganizativaConstantes.InstitucionId;
        return new UnidadOrganizativa(codigo, codigo, tipo, null, unidadPadreId)
        {
            Id = id ?? Guid.NewGuid()
        };
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

    // Per-method call counters used to assert short-circuit behavior on invalid requests.
    public int AddCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public int ExistsActiveCodeCallCount { get; private set; }
    public int GetByIdCallCount { get; private set; }
    public int GetByIdForUpdateCallCount { get; private set; }
    public int GetByIdIncludingDeletedCallCount { get; private set; }
    public int IsDescendantCallCount { get; private set; }
    public int ListAllCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int ReactivateCallCount { get; private set; }
    public int HasActiveChildrenCallCount { get; private set; }
    public int HasActivePuestosCallCount { get; private set; }

    /// <summary>
    /// Optional dictionary to simulate active puestos per unit for testing delete protection.
    /// </summary>
    public Dictionary<Guid, List<Puesto>> PuestosPorUnidad { get; set; } = [];

    /// <summary>
    /// Optional: when set, the read methods rebuild the returned domain entity with
    /// <see cref="UnidadOrganizativa.TipoUnidadOrganizativa"/> and
    /// <see cref="UnidadOrganizativa.UnidadPadre"/> navigations resolved from this
    /// repo and the sibling entries in <see cref="Datos"/>. This mirrors the
    /// production <c>Include(...)</c> behavior so issue #279 tests can assert on
    /// DTO nav fields without hitting EF Core.
    /// </summary>
    public ITipoUnidadOrganizativaRepository? TipoRepo { get; set; }

    public Task AddAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Datos.Add(unidad);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
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
        ExistsActiveCodeCallCount++;
        var duplicado = Datos.Any(d =>
            d.Codigo == codigo &&
            d.IsActive &&
            !d.IsDeleted &&
            d.Id != excludingId);
        return Task.FromResult(duplicado);
    }

    public Task<UnidadOrganizativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        var unidad = Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted);
        return Task.FromResult(HidratarConNavegaciones(unidad));
    }

    public Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        var unidad = Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted);
        return Task.FromResult(HidratarConNavegaciones(unidad));
    }

    public Task<UnidadOrganizativa?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdIncludingDeletedCallCount++;
        var unidad = Datos.FirstOrDefault(d => d.Id == id);
        return Task.FromResult(HidratarConNavegaciones(unidad));
    }

    /// <summary>
    /// Re-hydrates the unit with navigation properties populated via the
    /// factory <see cref="UnidadOrganizativa.Reconstitute"/> (internal), so the
    /// returned instance carries TipoUnidadOrganizativa + UnidadPadre like the
    /// production EF query does. When <see cref="TipoRepo"/> is null (legacy
    /// tests that don't care about navs) the original instance is returned.
    /// </summary>
    private UnidadOrganizativa? HidratarConNavegaciones(UnidadOrganizativa? source)
    {
        if (source is null || TipoRepo is null)
        {
            return source;
        }

        var tipo = TipoRepo.GetByIdAsync(source.TipoUnidadOrganizativaId).GetAwaiter().GetResult();
        UnidadOrganizativa? padre = null;
        if (source.UnidadPadreId.HasValue)
        {
            padre = Datos.FirstOrDefault(d => d.Id == source.UnidadPadreId.Value && d.IsActive && !d.IsDeleted);
            padre = HidratarConNavegaciones(padre);
        }

        return UnidadOrganizativa.Reconstitute(
            source.Id,
            source.Codigo,
            source.Nombre,
            source.TipoUnidadOrganizativaId,
            source.Descripcion,
            source.UnidadPadreId,
            source.VigenteDesde,
            source.VigenteHasta,
            source.IsActive,
            padre,
            tipo,
            source.CreatedAt,
            source.CreatedByUserId,
            source.UpdatedAt,
            source.UpdatedByUserId,
            source.IsDeleted,
            source.DeletedAt,
            source.DeletedByUserId);
    }

    public Task<bool> IsDescendantAsync(Guid candidateDescendantId, Guid ancestorId, CancellationToken cancellationToken = default)
    {
        IsDescendantCallCount++;
        // Mirror production (issue #277): visited-set bound so a cycle in
        // the fake data raises the canonical code instead of looping.
        var visited = new HashSet<Guid>(capacity: 16);
        var current = Datos.FirstOrDefault(d => d.Id == candidateDescendantId);
        while (current is not null && current.UnidadPadreId.HasValue)
        {
            if (!visited.Add(current.Id))
            {
                throw new InvalidOperationException("CicloJerarquico");
            }

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
        ListAllCallCount++;
        return Task.FromResult<IReadOnlyList<UnidadOrganizativa>>(Datos.Where(d => d.IsActive && !d.IsDeleted).ToList());
    }

    public Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var index = Datos.FindIndex(d => d.Id == unidad.Id);
        if (index >= 0)
        {
            Datos[index] = unidad;
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasActiveChildrenAsync(Guid unidadId, CancellationToken cancellationToken = default)
    {
        HasActiveChildrenCallCount++;
        return Task.FromResult(Datos.Any(d =>
            d.UnidadPadreId == unidadId && d.IsActive && !d.IsDeleted));
    }

    public Task<bool> HasActivePuestosAsync(Guid unidadId, CancellationToken cancellationToken = default)
    {
        HasActivePuestosCallCount++;
        var hasPuestos = PuestosPorUnidad.TryGetValue(unidadId, out var puestos)
            && puestos.Any(p => p.IsActive && !p.IsDeleted);
        return Task.FromResult(hasPuestos);
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCallCount++;
        var unidad = Datos.FirstOrDefault(d => d.Id == id);
        if (unidad is not null)
        {
            unidad.Activar();
            if (!Datos.Contains(unidad))
            {
                Datos.Add(unidad);
            }
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<UnidadOrganizativa> Items, int TotalCount)> QueryAsync(
        string? search, Guid? tipoUnidadOrganizativaId, Guid? unidadPadreId,
        DateOnly? vigenteEn, int page, int pageSize,
        UnidadOrganizativaSegmentoListado segmento = UnidadOrganizativaSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        var filtered = Datos.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(u =>
                u.Codigo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (tipoUnidadOrganizativaId.HasValue)
            filtered = filtered.Where(u => u.TipoUnidadOrganizativaId == tipoUnidadOrganizativaId.Value);
        if (unidadPadreId.HasValue)
            filtered = filtered.Where(u => u.UnidadPadreId == unidadPadreId.Value);
        var list = filtered.ToList();
        var total = list.Count;
        var pagedItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var result = ((IReadOnlyList<UnidadOrganizativa>)pagedItems, total);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<UnidadOrganizativa>> ListTreeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<UnidadOrganizativa>>(
            Datos.Where(u => u.IsActive).OrderBy(u => u.Codigo).ToList());
    }
}
