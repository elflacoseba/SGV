using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class PuestoRepositoryTests
{
    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new PuestoRepository(context);
        var entidades = await repo.ListAllAsync(default);

        Assert.All(entidades, e => Assert.False(e.IsDeleted));
    }

    [MySqlFact]
    public async Task ListAllAsync_IncluyeRelacionesUnidadOrganizativaYCargo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new PuestoRepository(context);
        var entidades = await repo.ListAllAsync(default);

        foreach (var puesto in entidades)
        {
            Assert.NotNull(puesto.UnidadOrganizativa);
            Assert.NotNull(puesto.Cargo);
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_IncluyeRelaciones()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new PuestoRepository(context);
        var entidades = await repo.ListAllAsync(default);

        if (entidades.Count > 0)
        {
            var primera = entidades[0];
            var encontrada = await repo.GetByIdAsync(primera.Id, default);

            Assert.NotNull(encontrada);
            Assert.NotNull(encontrada!.UnidadOrganizativa);
            Assert.NotNull(encontrada.Cargo);
        }
    }
}
