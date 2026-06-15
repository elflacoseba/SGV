using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests de repositorio para Cargo. Se restaurarán completamente en PR 2
/// cuando los repositorios mapeen de *Entity a tipos de Dominio.
/// </summary>
public sealed class CargoRepositoryTests
{
    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new CargoRepository(context);
        var entidades = await repo.ListAllAsync(default);

        // Seed data includes active Cargos that are not deleted
        Assert.NotEmpty(entidades);
        Assert.All(entidades, entidad => Assert.IsType<Cargo>(entidad));
        Assert.All(entidades, e => Assert.False(e.IsDeleted));
    }

    [MySqlFact]
    public async Task ListAllAsync_RetornaCargosOrdenadosPorCodigo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new CargoRepository(context);
        var entidades = await repo.ListAllAsync(default);

        Assert.NotEmpty(entidades);
        for (var i = 1; i < entidades.Count; i++)
        {
            Assert.True(string.Compare(entidades[i - 1].Codigo, entidades[i].Codigo, StringComparison.Ordinal) <= 0);
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaNull_CuandoNoExiste()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new CargoRepository(context);
        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }
}
