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
            "GER-2", "Nueva Gerencia", TipoUnidadOrganizativaConstantes.InstitucionId, "Descripción actualizada", null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("GER-2", resultado.Value!.Codigo);
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
            "FAC-2", "Nueva Facultad", TipoUnidadOrganizativaConstantes.FacultadId, "Descripción actualizada", null, null);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("FAC-2", resultado.Value!.Codigo);
        Assert.Equal("Nueva Facultad", resultado.Value.Nombre);
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

    // ===== Task 1.2: Reactivate protection + hierarchy/vigencia validation =====

    [Fact]
    public async Task ReactivarAsync_PadreInactivo_RetornaConflictoYSinGuardar()
    {
        var padre = new UnidadOrganizativa("PADRE", "Padre Inactivo", TipoUnidadOrganizativaConstantes.InstitucionId)
        {
            Id = PadreId
        };
        padre.Desactivar(); // padre inactivo
        var hijo = new UnidadOrganizativa("HIJO", "Hijo", TipoUnidadOrganizativaConstantes.FacultadId, PadreId)
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
        var padre = new UnidadOrganizativa("PADRE", "Padre Activo", TipoUnidadOrganizativaConstantes.InstitucionId)
        {
            Id = PadreId
        };
        // padre stays active (default)
        var hijo = new UnidadOrganizativa("HIJO", "Hijo", TipoUnidadOrganizativaConstantes.FacultadId, PadreId)
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
    public async Task CrearAsync_JerarquiaInvalida_RetornaValidacionYSinGuardar()
    {
        // Facultad under Direccion is not allowed — the policy codigo check must reject it
        var repo = new FakeUnidadOrganizativaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        // Direccion is NOT a valid parent for Facultad (Facultad only accepts Institucion)
        var padre = CrearUnidadActiva("DIR", PadreId, tipoId: TipoUnidadOrganizativaConstantes.DireccionId);
        repo.Datos.Add(padre);
        var request = new CrearUnidadOrganizativaRequest(
            "FAC", "Facultad de Prueba",
            TipoUnidadOrganizativaConstantes.FacultadId,
            null, null, null, PadreId);

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("JerarquiaInvalida", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_VigenciaFueraDelPadre_RetornaValidacionYSinGuardar()
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

        Assert.False(resultado.IsSuccess);
        Assert.Equal(UnidadOrganizativaErrorType.Validation, resultado.Error!.Type);
        Assert.Equal("VigenciaFueraDelPadre", resultado.Error.Code);
        Assert.Equal(0, uow.SaveChangesCount);
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
    public async Task ActualizarAsync_CodigoVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("", "Nombre", TipoUnidadOrganizativaConstantes.AreaId);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_RequestInvalidoNoBuscaUnidad()
    {
        var repo = new FakeUnidadOrganizativaWriteRepository(); // empty — no data
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("", "Nombre", TipoUnidadOrganizativaConstantes.AreaId);

        // Id is irrelevant because shape validation fires before GetByIdForUpdateAsync
        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(), request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
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
    public async Task ActualizarAsync_CodigoVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("", "Nombre", TipoUnidadOrganizativaConstantes.AreaId);

        var resultado = await servicio.ActualizarAsync(existente.Id, request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("codigo", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Codigo", resultado.FieldErrors.Keys);
        // Update path must not touch the unit nor the duplicate check.
        Assert.Equal(0, repo.GetByIdForUpdateCallCount);
        Assert.Equal(0, repo.ExistsActiveCodeCallCount);
        Assert.Equal(0, repo.GetByIdCallCount);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal(0, repo.IsDescendantCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_NombreVacio_EmiteClaveCamelCaseYSinConsultarRepos()
    {
        var existente = CrearUnidadActiva("GER");
        var repo = new FakeUnidadOrganizativaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = new UnidadOrganizativaServicioComandos(repo, FakeTipoRepo, uow);
        var request = new ActualizarUnidadOrganizativaRequest("GER", "", TipoUnidadOrganizativaConstantes.AreaId);

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
        var request = new ActualizarUnidadOrganizativaRequest("GER", "Gerencia", Guid.Empty);

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

    private static UnidadOrganizativa CrearUnidadActiva(
        string codigo, Guid? id = null, Guid? padreId = null, Guid? tipoId = null)
    {
        var tipo = tipoId ?? TipoUnidadOrganizativaConstantes.InstitucionId;
        var unidad = new UnidadOrganizativa(codigo, codigo, tipo, padreId)
        {
            Id = id ?? Guid.NewGuid()
        };
        unidad.CambiarDatos(codigo, codigo, tipo, null);
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
        return Task.FromResult(unidad);
    }

    public Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        var unidad = Datos.FirstOrDefault(d => d.Id == id && d.IsActive && !d.IsDeleted);
        return Task.FromResult(unidad);
    }

    public Task<UnidadOrganizativa?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdIncludingDeletedCallCount++;
        var unidad = Datos.FirstOrDefault(d => d.Id == id);
        return Task.FromResult(unidad);
    }

    public Task<bool> IsDescendantAsync(Guid candidateDescendantId, Guid ancestorId, CancellationToken cancellationToken = default)
    {
        IsDescendantCallCount++;
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
}
