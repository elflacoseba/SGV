using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

public sealed class HabilidadServicioConsultaTests
{
    private static readonly Habilidad HabilidadActiva = new("LIDERAZGO", "Liderazgo", "Conducción", "Capacidad de liderar equipos")
    {
        Id = Guid.Parse("50000000-0000-0000-0000-000000000001")
    };

    [Fact]
    public async Task ListAsync_CuandoExistenHabilidades_RetornaListaDeDto()
    {
        var repo = new FakeHabilidadRepository { Datos = [HabilidadActiva] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(HabilidadActiva.Id, dto.Id);
        Assert.Equal(HabilidadActiva.Codigo, dto.Codigo);
        Assert.Equal(HabilidadActiva.Nombre, dto.Nombre);
        Assert.Equal(HabilidadActiva.Categoria, dto.Categoria);
        Assert.Equal(HabilidadActiva.Descripcion, dto.Descripcion);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenHabilidades_RetornaListaVacia()
    {
        var repo = new FakeHabilidadRepository { Datos = [] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoHabilidadExiste_RetornaDto()
    {
        var repo = new FakeHabilidadRepository { Datos = [HabilidadActiva] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(HabilidadActiva.Id, default);

        Assert.NotNull(resultado);
        Assert.Equal(HabilidadActiva.Id, resultado!.Id);
        Assert.Equal(HabilidadActiva.Codigo, resultado.Codigo);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoHabilidadNoExiste_RetornaNull()
    {
        var repo = new FakeHabilidadRepository { Datos = [] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }
}

internal sealed class FakeHabilidadRepository : IHabilidadRepository
{
    public List<Habilidad> Datos { get; set; } = [];

    public Task<Habilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Habilidad>>(Datos.ToList());
    }

    public Task AddAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task UpdateAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");
}
