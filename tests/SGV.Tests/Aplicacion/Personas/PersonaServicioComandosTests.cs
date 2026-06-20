using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Personas.Comandos;
using SGV.Aplicacion.Personas.Consultas;
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
        TipoDocumento: "DNI",
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
            tipoDocumento: "DNI", numeroDocumento: "87654321");
        var repo = new FakePersonaWriteRepository { Datos = [existente] };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);

        var resultado = await servicio.CrearAsync(CrearRequest(legajo: "LEG-003", nombres: "Ana", apellidos: "García")
            with { Email = "nuevo@test.com", TipoDocumento = "DNI", NumeroDocumento = "87654321" }, default);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(PersonaErrorType.Conflict, resultado.Error!.Type);
        Assert.Contains("documento", resultado.Error.Code, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    [Fact]
    public async Task CrearAsync_LegajoVacio_RetornaFieldErrorsSinConsultarRepos()
    {
        var repo = new FakePersonaWriteRepository
        {
            Datos = [CrearPersonaActiva("LEG-001", PersonaIdActiva)]
        };
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearPersonaRequest("", "Juan", "Pérez");

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("legajo", resultado.FieldErrors!.Keys);
        Assert.DoesNotContain("Legajo", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.ExistsActiveLegajoCallCount);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
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
        var repo = new FakePersonaWriteRepository();
        var uow = new FakeUnitOfWork();
        var servicio = CrearServicio(repo, uow);
        var request = new CrearPersonaRequest("", "", "");

        var resultado = await servicio.CrearAsync(request, default);

        Assert.False(resultado.IsSuccess);
        Assert.NotNull(resultado.FieldErrors);
        Assert.Contains("legajo", resultado.FieldErrors!.Keys);
        Assert.Contains("nombres", resultado.FieldErrors.Keys);
        Assert.Contains("apellidos", resultado.FieldErrors.Keys);
        Assert.Equal(0, repo.AddCallCount);
        Assert.Equal(0, uow.SaveChangesCount);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static PersonaServicioComandos CrearServicio(
        IPersonaRepository repo,
        IUnitOfWork uow)
    {
        return new PersonaServicioComandos(repo, uow);
    }

    private static Persona CrearPersonaActiva(
        string legajo, Guid? id = null,
        string? email = null,
        string? tipoDocumento = null,
        string? numeroDocumento = null)
    {
        var persona = new Persona("Juan", "Pérez", legajo, email ?? "juan@test.com")
        {
            Id = id ?? Guid.NewGuid()
        };
        if (tipoDocumento is not null && numeroDocumento is not null)
        {
            persona.CambiarDocumento(tipoDocumento, numeroDocumento);
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

    public Task<bool> ExistsActiveDocumentoAsync(string tipoDocumento, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        ExistsActiveDocumentoCallCount++;
        var exists = Datos.Any(d =>
            d.TipoDocumento == tipoDocumento &&
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
}
