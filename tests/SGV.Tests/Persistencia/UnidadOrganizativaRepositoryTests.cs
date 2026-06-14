using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class UnidadOrganizativaRepositoryTests
{
    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesInactivasYEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var visible = RepositoryTestData.CreateUnidadOrganizativa("UO-VISIBLE");
        var inactive = RepositoryTestData.CreateUnidadOrganizativa("UO-INACTIVE", isActive: false);
        var deleted = RepositoryTestData.CreateUnidadOrganizativa("UO-DELETED", isDeleted: true);

        await context.UnidadesOrganizativas.AddRangeAsync([visible, inactive, deleted]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var entidades = await repo.ListAllAsync(default);

            Assert.Contains(entidades, entidad => entidad.Id == visible.Id);
            Assert.DoesNotContain(entidades, entidad => entidad.Id == inactive.Id);
            Assert.DoesNotContain(entidades, entidad => entidad.Id == deleted.Id);
            Assert.All(entidades, entidad =>
            {
                Assert.True(entidad.IsActive);
                Assert.False(entidad.IsDeleted);
            });
        }
        finally
        {
            context.UnidadesOrganizativas.RemoveRange(visible, inactive, deleted);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaEntidadCuandoExiste()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var expected = RepositoryTestData.CreateUnidadOrganizativa("UO-BY-ID");

        await context.UnidadesOrganizativas.AddAsync(expected);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var encontrada = await repo.GetByIdAsync(expected.Id, default);

            Assert.NotNull(encontrada);
            Assert.Equal(expected.Id, encontrada!.Id);
            Assert.Equal(expected.Codigo, encontrada.Codigo);
            Assert.Equal(expected.Nombre, encontrada.Nombre);
            Assert.True(encontrada.IsActive);
            Assert.False(encontrada.IsDeleted);
        }
        finally
        {
            context.UnidadesOrganizativas.Remove(expected);
            await context.SaveChangesAsync();
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
