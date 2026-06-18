using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class NivelCargoServicioConsultaTests
{
    private static readonly FakeNivelCargoReadRepository Repo = new()
    {
        Datos =
        [
            new("Directivo", "Directivo", 1, 1) { Id = NivelCargoConstantes.DirectivoId },
            new("ConduccionMedia", "Conducción Media", 2, 2) { Id = NivelCargoConstantes.ConduccionMediaId },
            new("Operativo", "Operativo", 3, 3) { Id = NivelCargoConstantes.OperativoId },
            new("Academico", "Académico", 4, 4) { Id = NivelCargoConstantes.AcademicoId },
        ]
    };

    [Fact]
    public async Task ListAsync_CuandoExistenRegistros_RetornaListaCompleta()
    {
        var servicio = new NivelCargoServicioConsulta(Repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Equal(4, resultado.Count);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenRegistros_RetornaListaVacia()
    {
        var repoVacio = new FakeNivelCargoReadRepository { Datos = [] };
        var servicio = new NivelCargoServicioConsulta(repoVacio);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoRegistroExiste_RetornaDto()
    {
        var servicio = new NivelCargoServicioConsulta(Repo);

        var resultado = await servicio.GetByIdAsync(NivelCargoConstantes.DirectivoId, default);

        Assert.NotNull(resultado);
        Assert.Equal(NivelCargoConstantes.DirectivoId, resultado!.Id);
        Assert.Equal("Directivo", resultado.Codigo);
        Assert.Equal("Directivo", resultado.Nombre);
        Assert.Equal(1, resultado.ValorNumerico);
        Assert.Equal(1, resultado.Orden);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoRegistroNoExiste_RetornaNull()
    {
        var servicio = new NivelCargoServicioConsulta(Repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }
}

internal sealed class FakeNivelCargoReadRepository : INivelCargoRepository
{
    public List<NivelCargo> Datos { get; set; } = [];

    public Task<NivelCargo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<IReadOnlyList<NivelCargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<NivelCargo>>(Datos.ToList());
    }

    public Task<NivelCargo?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Codigo == codigo));
    }
}
