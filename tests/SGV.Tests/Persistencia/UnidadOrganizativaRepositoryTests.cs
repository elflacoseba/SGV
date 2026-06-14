using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class UnidadOrganizativaRepositoryTests
{
    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        // Ensure test entity does not exist
        var repo = new UnidadOrganizativaRepository(context);
        var entidades = await repo.ListAllAsync(default);

        // Seed data should include UnidadesOrganizativas that are not deleted
        Assert.All(entidades, e => Assert.False(e.IsDeleted));
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaEntidadCuandoExiste()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new UnidadOrganizativaRepository(context);
        var entidades = await repo.ListAllAsync(default);

        if (entidades.Count > 0)
        {
            var primera = entidades[0];
            var encontrada = await repo.GetByIdAsync(primera.Id, default);

            Assert.NotNull(encontrada);
            Assert.Equal(primera.Id, encontrada!.Id);
            Assert.Equal(primera.Codigo, encontrada.Codigo);
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaNull_CuandoNoExiste()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new UnidadOrganizativaRepository(context);
        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }
}
