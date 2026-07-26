using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGV.Aplicacion.Auditoria;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Comandos.Validaciones;
using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Seguridad;
using SGV.Contracts.Personas.Comandos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

public sealed class PersonaServicioComandosTests
{
    private static readonly Guid PersonaIdActiva = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid PersonaIdConflicto = Guid.Parse("60000000-0000-0000-0000-000000000002");

    private static CrearPersonaRequest CrearRequest(
        string? legajo = null,
        string? nombres = null,
        string? apellidos = null) => new(
        Legajo: legajo ?? "LEG-001",
        Nombres: nombres ?? "Juan",
        Apellidos: apellidos ?? "Pérez",
        Email: "juan@test.com",
        // Issue #147: TipoDocumentoId reemplaza al string TipoDocumento.
        // Tests existentes actualizados con un Guid fijo (no necesita
        // matchear el seed real porque los fakes no enforcen FK).
        TipoDocumentoId: new Guid("11111111-1111-1111-1111-111111111111"),
        NumeroDocumento: "12345678",
        Telefono: "555-0101");

    // ── CrearAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaDtoYGuarda()
    {
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(), default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Equal("LEG-001", resultado.Value!.Legajo);
        Assert.Equal("Juan", resultado.Value.Nombres);
        Assert.Equal("Pérez", resultado.Value.Apellidos);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_LegajoDuplicadoActivo_RetornaConflictoYSinGuardar()
    {
        var existente = CrearPersonaActiva("LEG-002", PersonaIdActiva);
        var repo = new FakePersonaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(legajo: "LEG-002"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.Conflict, resultado.Error!.Type);
        Assert.Contains("legajo", resultado.Error.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_EmailDuplicadoActivo_RetornaConflictoYSinGuardar()
    {
        var existente = CrearPersonaActiva("LEG-001", PersonaIdActiva, email: "duplicado@test.com");
        var repo = new FakePersonaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(legajo: "LEG-003", nombres: "Ana", apellidos: "García")
            with { Email = "duplicado@test.com" }, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.Conflict, resultado.Error!.Type);
        Assert.Contains("email", resultado.Error.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_DocumentoDuplicadoActivo_RetornaConflictoYSinGuardar()
    {
        var existente = CrearPersonaActiva("LEG-001", PersonaIdActiva,
            email: "existente@test.com",
            tipoDocumentoId: new Guid("22222222-2222-2222-2222-222222222222"), numeroDocumento: "87654321");
        var repo = new FakePersonaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(legajo: "LEG-003", nombres: "Ana", apellidos: "García")
            with { Email = "nuevo@test.com", TipoDocumentoId = new Guid("22222222-2222-2222-2222-222222222222"), NumeroDocumento = "87654321" }, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.Conflict, resultado.Error!.Type);
        Assert.Contains("documento", resultado.Error.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_LegajoVacio_PermitidoYGuarda()
    {
        // Política vigente: Legajo es opcional (Persona.Legajo? +
        // ValidacionesDominio.Opcional + columna nullable). El
        // bootstrap del primer Administrador (issue #195) lo necesita
        // opcional; por eso no exigimos NotEmpty en el validator.
        // Antes este test verificaba que Legajo vacío devolvía un
        // FieldError; ahora verifica que la request atraviesa la
        // validación, llega al repo y persiste.
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearPersonaRequest("", "Juan", "Pérez");

        var resultado = await servicio.CrearAsync(request, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.True(string.IsNullOrEmpty(resultado.Value!.Legajo));
        Assert.Equal(1, repo.AddCallCount);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_LegajoConUnSoloEspacio_TambienEsValido()
    {
        // El validator sólo aplica MaximumLength cuando hay valor
        // (no-blanco tras IsNullOrEmpty). Espacios en blanco cuentan
        // como ausencia y deben pasar.
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearPersonaRequest("   ", "Juan", "Pérez");

        var resultado = await servicio.CrearAsync(request, default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task CrearAsync_NombresVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearPersonaRequest("LEG-001", "", "Pérez");

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("nombres", resultado.FieldErrors!.Keys);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ActualizarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda()
    {
        var existente = CrearPersonaActiva("LEG-001", PersonaIdActiva);
        var repo = new FakePersonaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(existente.Id,
            new ActualizarPersonaRequest("LEG-001", "Juan Carlos", "Pérez García", "juan@nuevo.com"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Juan Carlos", resultado.Value!.Nombres);
        Assert.Equal("Pérez García", resultado.Value.Apellidos);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_PersonaInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(),
            new ActualizarPersonaRequest("LEG-001", "Juan", "Pérez"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ActualizarAsync_LegajoConflictivo_RetornaConflictoYSinGuardar()
    {
        var activa = CrearPersonaActiva("LEG-001", PersonaIdActiva);
        var otra = CrearPersonaActiva("LEG-002", PersonaIdConflicto);
        var repo = new FakePersonaWriteRepository { Datos = [activa, otra] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ActualizarAsync(otra.Id,
            new ActualizarPersonaRequest("LEG-001", "Otra", "Persona"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── DesactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task DesactivarAsync_PersonaExistente_RetornaExitoYGuarda()
    {
        var persona = CrearPersonaActiva("LEG-001", PersonaIdActiva);
        var repo = new FakePersonaWriteRepository { Datos = [persona] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.DesactivarAsync(persona.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task DesactivarAsync_PersonaInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.DesactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── ReactivarAsync ─────────────────────────────────────────

    [Fact]
    public async Task ReactivarAsync_PersonaDesactivada_RetornaExitoYGuarda()
    {
        var persona = CrearPersonaDesactivada("LEG-001", PersonaIdActiva);
        var repo = new FakePersonaWriteRepository { Datos = [persona] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ReactivarAsync(persona.Id, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_PersonaInexistente_RetornaNoEncontradoYSinGuardar()
    {
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ReactivarAsync(Guid.NewGuid(), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.NotFound, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task ReactivarAsync_LegajoConflictivo_RetornaConflictoYSinGuardar()
    {
        var activa = CrearPersonaActiva("LEG-001", PersonaIdActiva);
        var desactivada = CrearPersonaDesactivada("LEG-001", PersonaIdConflicto);
        var repo = new FakePersonaWriteRepository { Datos = [activa, desactivada] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.ReactivarAsync(desactivada.Id, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Multiples errores de validación ─────────────────────────

    [Fact]
    public async Task CrearAsync_MultiplesErrores_EmiteTodasLasClavesCamelCase()
    {
        // Tras la relajación de Legajo (ahora opcional, alineado con
        // el dominio), una request con los tres campos String.Empty
        // sólo falla por Nombres y Apellidos. Legajo vacío sigue
        // formando parte de la request — no agrega un error de
        // validación.
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearPersonaRequest("", "", "");

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.DoesNotContain("legajo", resultado.FieldErrors!.Keys);
        Assert.Contains("nombres", resultado.FieldErrors.Keys);
        Assert.Contains("apellidos", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Issue #202: Auditoría explícita al limpiar Legajo ─────

    [Fact]
    public async Task CrearAsync_LegajoNull_PermitidoYGuarda()
    {
        // AC persona-management § "Alta de Persona": Legajo MAY omitirse.
        // Tras wire-type a string?, CrearPersonaRequest acepta null; el
        // servicio debe persistirlo y NO emitir auditoría UpdateLegajo
        // (no hay transición previa).
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var auditoria = new FakeAuditoriaServicio();
        var servicio = CrearServicio(repo, uow, auditoria);
        var request = new CrearPersonaRequest(null, "Juan", "Pérez");

        var resultado = await servicio.CrearAsync(request, default);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value);
        Assert.Null(resultado.Value!.Legajo);
        Assert.Equal(1, repo.AddCallCount);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Equal(0, auditoria.Invocaciones.Count);
    }

    [Fact]
    public async Task ActualizarAsync_LimpiarLegajo_RegistraAuditoria()
    {
        // AC persona-management § "Editar limpiando Legajo persiste null y
        // registra auditoría UpdateLegajo": Legajo="L-001" -> null debe
        // invocar IAuditoriaServicio.RegistrarAsync con Accion="UpdateLegajo",
        // LegajoAnterior="L-001" y LegajoNuevo=null.
        var persona = CrearPersonaActiva("L-001", PersonaIdActiva);
        var repo = new FakePersonaWriteRepository { Datos = [persona] };
        var uow = new FakeUnitOfWork();
        var auditoria = new FakeAuditoriaServicio();
        var servicio = CrearServicio(repo, uow, auditoria);

        var resultado = await servicio.ActualizarAsync(persona.Id,
            new ActualizarPersonaRequest(null, "Juan", "Pérez"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value!.Legajo);
        Assert.Equal(1, uow.SaveChangesCount);

        var inv = Assert.Single(auditoria.Invocaciones);
        Assert.Equal("Persona", inv.Entidad);
        Assert.Equal(persona.Id.ToString(), inv.EntityId);
        Assert.Equal("UpdateLegajo", inv.Accion);
        Assert.Equal("L-001", inv.ValoresAnteriores["LegajoAnterior"]);
        Assert.Null(inv.ValoresNuevos["LegajoNuevo"]);
    }

    [Fact]
    public async Task ActualizarAsync_LegajoSinTransicion_NoEmiteAuditoriaLegajo()
    {
        // AC persona-management § "Editar sin transición de Legajo no genera
        // fila UpdateLegajo": Legajo="L-001" -> "L-001" (sin cambio) NO debe
        // invocar RegistrarAsync. La auditoría central sigue emitiendo su
        // fila Modificacion vía interceptor, fuera del alcance de este test.
        var persona = CrearPersonaActiva("L-001", PersonaIdActiva);
        var repo = new FakePersonaWriteRepository { Datos = [persona] };
        var uow = new FakeUnitOfWork();
        var auditoria = new FakeAuditoriaServicio();
        var servicio = CrearServicio(repo, uow, auditoria);

        var resultado = await servicio.ActualizarAsync(persona.Id,
            new ActualizarPersonaRequest("L-001", "Juan Carlos", "Pérez"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(auditoria.Invocaciones);
    }

    [Fact]
    public async Task ActualizarAsync_RegistrarAsyncFalla_PersonaUpdatePersisteYNoPropagaExcepcion()
    {
        var persona = CrearPersonaActiva("L-001", PersonaIdActiva);
        var repo = new FakePersonaWriteRepository { Datos = [persona] };
        var uow = new FakeUnitOfWork();
        var auditoria = new FakeAuditoriaServicio { ThrowOnRegistrar = true };
        var logger = new ListLogger<PersonaServicioComandos>();
        var servicio = CrearServicio(repo, uow, auditoria, logger);

        var resultado = await servicio.ActualizarAsync(persona.Id,
            new ActualizarPersonaRequest(null, "Juan", "Pérez"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value!.Legajo);
        Assert.Equal(1, uow.SaveChangesCount);
        Assert.Single(logger.Warnings);
        Assert.Contains("UpdateLegajo", logger.Warnings[0]);
    }
    [Fact]
    public async Task ActualizarAsync_LegajoDuplicado_SigueRechazando()
    {
        // Regresión: la introducción de IAuditoriaServicio en el ctor no
        // debe debilitar la regla de unicidad activa para legajos no nulos.
        var activa = CrearPersonaActiva("LEG-001", PersonaIdActiva);
        var otra = CrearPersonaActiva("LEG-002", PersonaIdConflicto);
        var repo = new FakePersonaWriteRepository { Datos = [activa, otra] };
        var uow = new FakeUnitOfWork();
        var auditoria = new FakeAuditoriaServicio();
        var servicio = CrearServicio(repo, uow, auditoria);

        var resultado = await servicio.ActualizarAsync(otra.Id,
            new ActualizarPersonaRequest("LEG-001", "Otra", "Persona"), default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.Conflict, resultado.Error!.Type);
        Assert.Equal(0, uow.SaveChangesCount);
        Assert.Empty(auditoria.Invocaciones);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static PersonaServicioComandos CrearServicio(
        IPersonaRepository repo,
        IUnitOfWork uow)
    {
        return new PersonaServicioComandos(repo, uow, NullLogger<PersonaServicioComandos>.Instance);
    }

    private static PersonaServicioComandos CrearServicio(
        IPersonaRepository repo,
        IUnitOfWork uow,
        IAuditoriaServicio auditoria)
    {
        return new PersonaServicioComandos(
            repo,
            uow,
            new CrearPersonaRequestValidator(),
            new ActualizarPersonaRequestValidator(),
            auditoria,
            new FakeUsuarioActual(),
            NullLogger<PersonaServicioComandos>.Instance);
    }

    private static PersonaServicioComandos CrearServicio(
        IPersonaRepository repo,
        IUnitOfWork uow,
        IAuditoriaServicio auditoria,
        ILogger<PersonaServicioComandos> logger)
    {
        return new PersonaServicioComandos(
            repo,
            uow,
            new CrearPersonaRequestValidator(),
            new ActualizarPersonaRequestValidator(),
            auditoria,
            new FakeUsuarioActual(),
            logger);
    }
    private static Persona CrearPersonaActiva(
        string legajo, Guid? id = null,
        string? email = null,
        Guid? tipoDocumentoId = null,
        string? numeroDocumento = null)
    {
        var persona = new Persona("Juan", "Pérez", legajo, email ?? "juan@test.com")
        {
            Id = id ?? Guid.NewGuid()
        };
        if (tipoDocumentoId is not null && numeroDocumento is not null)
        {
            persona.CambiarDocumento(tipoDocumentoId, numeroDocumento);
        }
        return persona;
    }

    private static Persona CrearPersonaDesactivada(string legajo, Guid? id = null)
    {
        var persona = CrearPersonaActiva(legajo, id);
        persona.Desactivar();
        return persona;
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

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakePersonaWriteRepository : IPersonaRepository
{
    public List<Persona> Datos { get; set; } = [];

    public int AddCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public int ExistsActiveLegajoCallCount { get; private set; }
    public int ExistsActiveEmailCallCount { get; private set; }
    public int ExistsActiveDocumentoCallCount { get; private set; }
    public int GetByIdCallCount { get; private set; }
    public int GetByIdForUpdateCallCount { get; private set; }
    public int GetByIdIncludingDeletedCallCount { get; private set; }
    public int ListAllCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public int ReactivateCallCount { get; private set; }

    public Task AddAsync(Persona persona, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        Datos.Add(persona);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        var persona = Datos.FirstOrDefault(d => d.Id == id);
        if (persona is not null)
        {
            persona.Desactivar();
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveLegajoCallCount++;
        var exists = Datos.Any(d =>
            d.Legajo == legajo &&
            d.IsActive &&
            d.Id != excludingId);
        return Task.FromResult(exists);
    }

    public Task<bool> ExistsActiveEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveEmailCallCount++;
        var exists = Datos.Any(d =>
            d.Email == email &&
            d.IsActive &&
            d.Id != excludingId);
        return Task.FromResult(exists);
    }

    public Task<bool> ExistsActiveDocumentoAsync(Guid tipoDocumentoId, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveDocumentoCallCount++;
        var exists = Datos.Any(d =>
            d.TipoDocumentoId == tipoDocumentoId &&
            d.NumeroDocumento == numeroDocumento &&
            d.IsActive &&
            d.Id != excludingId);
        return Task.FromResult(exists);
    }

    public Task<Persona?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive));
    }

    public Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id && d.IsActive));
    }

    public Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdIncludingDeletedCallCount++;
        return Task.FromResult(Datos.FirstOrDefault(d => d.Id == id));
    }

    public Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        ListAllCallCount++;
        return Task.FromResult<IReadOnlyList<Persona>>(Datos.Where(d => d.IsActive).ToList());
    }

    public Task UpdateAsync(Persona persona, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var index = Datos.FindIndex(d => d.Id == persona.Id);
        if (index >= 0)
        {
            Datos[index] = persona;
        }
        return Task.CompletedTask;
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCallCount++;
        var persona = Datos.FirstOrDefault(d => d.Id == id);
        if (persona is not null)
        {
            persona.Activar();
        }
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Persona> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        PersonaSegmentoListado segmento = PersonaSegmentoListado.Activas,
        CancellationToken cancellationToken = default,
        bool? soloSinUsuario = null)
        => throw new NotSupportedException("Write-only fake does not support QueryAsync.");
}

// ── Fakes for issue #202 (auditoría al limpiar Legajo) ─────

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Warnings { get; } = [];
    public List<Exception> Exceptions { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= LogLevel.Warning)
        {
            Warnings.Add(formatter(state, exception));
            if (exception is not null)
            {
                Exceptions.Add(exception);
            }
        }
    }
}

internal sealed class FakeAuditoriaServicio : IAuditoriaServicio
{
    public List<AuditoriaInvocacion> Invocaciones { get; } = [];
    public bool ThrowOnRegistrar { get; init; }
    public Exception? RegistrarException { get; init; }

    public Task RegistrarAsync(
        string entidad,
        string entityId,
        string accion,
        string? usuarioOperadorId,
        IReadOnlyDictionary<string, object?> valoresAnteriores,
        IReadOnlyDictionary<string, object?> valoresNuevos,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnRegistrar)
        {
            throw RegistrarException ?? new InvalidOperationException("forced audit failure");
        }

        Invocaciones.Add(new AuditoriaInvocacion(
            entidad,
            entityId,
            accion,
            usuarioOperadorId,
            valoresAnteriores,
            valoresNuevos));
        return Task.CompletedTask;
    }
}
internal sealed record AuditoriaInvocacion(
    string Entidad,
    string EntityId,
    string Accion,
    string? UsuarioOperadorId,
    IReadOnlyDictionary<string, object?> ValoresAnteriores,
    IReadOnlyDictionary<string, object?> ValoresNuevos);

internal sealed class FakeUsuarioActual : IUsuarioActual
{
    public string? UserId { get; init; } = "test-user";

    public Guid? PersonaId => null;

    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public Guid? CorrelationId { get; init; }
}
