using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Issue #273 (refactor): cubre <c>EstadoVacanteRepository.GetByCodigoAsync</c>,
/// el método agregado para que la capa de Aplicación resuelva el catálogo por
/// <c>Codigo</c> sin tener que cargar el catálogo completo y filtrar en
/// memoria. Los seeds canónicos vienen de
/// <c>EstadoVacanteConstantes.Semilla</c> (bloque <c>20000000-…</c>).
/// </summary>
public sealed class EstadoVacanteRepositoryTests
{
    [MySqlFact]
    public async Task GetByCodigoAsync_Abierta_RetornaEstadoVacante()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new EstadoVacanteRepository(context);

        var entidad = await repo.GetByCodigoAsync("Abierta", default);

        Assert.NotNull(entidad);
        Assert.Equal("Abierta", entidad!.Codigo);
        Assert.Equal("Abierta", entidad.Nombre);
        Assert.False(entidad.EsTerminal);
    }

    [MySqlFact]
    public async Task GetByCodigoAsync_EnSeleccion_RetornaEstadoVacante()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new EstadoVacanteRepository(context);

        var entidad = await repo.GetByCodigoAsync("EnSeleccion", default);

        Assert.NotNull(entidad);
        Assert.Equal("EnSeleccion", entidad!.Codigo);
        Assert.Equal("En Selección", entidad.Nombre);
    }

    [MySqlFact]
    public async Task GetByCodigoAsync_CodigoInexistente_RetornaNull()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new EstadoVacanteRepository(context);

        var entidad = await repo.GetByCodigoAsync("NoExiste", default);

        Assert.Null(entidad);
    }

    [MySqlFact]
    public async Task GetByCodigoAsync_CodigoVacio_LanzaArgumentException()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new EstadoVacanteRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.GetByCodigoAsync(string.Empty, default));
    }
}
