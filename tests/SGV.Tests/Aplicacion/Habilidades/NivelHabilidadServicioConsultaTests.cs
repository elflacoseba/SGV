using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

/// <summary>
/// Tests for <see cref="NivelHabilidadServicioConsulta"/>. Mirrors the
/// <see cref="NivelCargoServicioConsulta"/> contract: list and get-by-id
/// returning <see cref="NivelHabilidadDto"/>.
/// </summary>
public sealed class NivelHabilidadServicioConsultaTests
{
    private static readonly NivelHabilidad Basico = new("BASICO", "Básico", 1, 1)
    {
        Id = Guid.Parse("91000000-0000-0000-0000-000000000001")
    };
    private static readonly NivelHabilidad Avanzado = new("AVANZADO", "Avanzado", 3, 3)
    {
        Id = Guid.Parse("91000000-0000-0000-0000-000000000002")
    };

    [Fact]
    public async Task ListAsync_CuandoExistenRegistros_RetornaListaCompleta()
    {
        var repo = new FakeNivelHabilidadRepository { Datos = [Basico, Avanzado] };
        var servicio = new NivelHabilidadServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(Basico.Id, resultado[0].Id);
        Assert.Equal("Básico", resultado[0].Nombre);
        Assert.Equal(Basico.Orden, resultado[0].Orden);
        Assert.Equal(Avanzado.Id, resultado[1].Id);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenRegistros_RetornaListaVacia()
    {
        var repo = new FakeNivelHabilidadRepository { Datos = [] };
        var servicio = new NivelHabilidadServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_RetornaDto_CuandoExiste()
    {
        var repo = new FakeNivelHabilidadRepository { Datos = [Basico, Avanzado] };
        var servicio = new NivelHabilidadServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Basico.Id, default);

        Assert.NotNull(resultado);
        Assert.Equal(Basico.Id, resultado!.Id);
        Assert.Equal("Básico", resultado.Nombre);
        Assert.Equal(Basico.ValorNumerico, resultado.ValorNumerico);
    }

    [Fact]
    public async Task GetByIdAsync_RetornaNull_CuandoNoExiste()
    {
        var repo = new FakeNivelHabilidadRepository { Datos = [] };
        var servicio = new NivelHabilidadServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }
}

internal sealed class FakeNivelHabilidadRepository : INivelHabilidadRepository
{
    public List<NivelHabilidad> Datos { get; set; } = [];

    public Task<NivelHabilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<NivelHabilidad>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NivelHabilidad>>(Datos.ToList());
}