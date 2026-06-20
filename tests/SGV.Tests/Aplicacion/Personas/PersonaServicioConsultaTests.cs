using SGV.Aplicacion.Personas.Consultas;
using SGV.Aplicacion.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Aplicacion.Personas;

public sealed class PersonaServicioConsultaTests
{
    private static readonly Persona PersonaActiva = new("Juan", "Pérez", "LEG-001", "juan@test.com")
    {
        Id = Guid.Parse("60000000-0000-0000-0000-000000000001")
    };

    private static Persona CrearPersonaInactiva()
    {
        var p = new Persona("Ana", "García", "LEG-002", "ana@test.com")
        {
            Id = Guid.Parse("60000000-0000-0000-0000-000000000002")
        };
        p.Desactivar();
        return p;
    }

    [Fact]
    public async Task ListAsync_CuandoExistenPersonasActivas_RetornaListaDeDto()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(PersonaActiva.Id, dto.Id);
        Assert.Equal(PersonaActiva.Legajo, dto.Legajo);
        Assert.Equal(PersonaActiva.Nombres, dto.Nombres);
        Assert.Equal(PersonaActiva.Apellidos, dto.Apellidos);
        Assert.Equal(PersonaActiva.Email, dto.Email);
        Assert.Equal(PersonaActiva.TipoDocumento, dto.TipoDocumento);
        Assert.Equal(PersonaActiva.NumeroDocumento, dto.NumeroDocumento);
        Assert.Equal(PersonaActiva.Telefono, dto.Telefono);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenPersonas_RetornaListaVacia()
    {
        var repo = new FakePersonaRepository { Datos = [] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListAsync_ExcluyePersonasInactivas()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva, CrearPersonaInactiva()] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        Assert.Equal(PersonaActiva.Id, resultado[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPersonaActivaExiste_RetornaDto()
    {
        var repo = new FakePersonaRepository { Datos = [PersonaActiva] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(PersonaActiva.Id, default);

        Assert.NotNull(resultado);
        Assert.Equal(PersonaActiva.Id, resultado!.Id);
        Assert.Equal(PersonaActiva.Nombres, resultado.Nombres);
        Assert.Equal(PersonaActiva.Apellidos, resultado.Apellidos);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPersonaNoExiste_RetornaNull()
    {
        var repo = new FakePersonaRepository { Datos = [] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPersonaEstaInactiva_RetornaNull()
    {
        var personaInactiva = CrearPersonaInactiva();
        var repo = new FakePersonaRepository { Datos = [PersonaActiva, personaInactiva] };
        var servicio = new PersonaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(personaInactiva.Id, default);

        Assert.Null(resultado);
    }
}

internal sealed class FakePersonaRepository : IPersonaRepository
{
    public List<Persona> Datos { get; set; } = [];

    public Task<Persona?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id && e.IsActive));
    }

    public Task<IReadOnlyList<Persona>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Persona>>(Datos.Where(e => e.IsActive).ToList());
    }

    public Task AddAsync(Persona persona, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Persona?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Persona?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task UpdateAsync(Persona persona, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveLegajoAsync(string legajo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveDocumentoAsync(string tipoDocumento, string numeroDocumento, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");
}
