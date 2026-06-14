using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class PuestoRepositoryTests
{
    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesInactivasYEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-UO-LIST");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-CARGO-LIST");
        var visible = RepositoryTestData.CreatePuesto("PUESTO-VISIBLE", unidad, cargo);
        var inactive = RepositoryTestData.CreatePuesto("PUESTO-INACTIVE", unidad, cargo, isActive: false);
        var deleted = RepositoryTestData.CreatePuesto("PUESTO-DELETED", unidad, cargo, isDeleted: true);

        await context.UnidadesOrganizativas.AddAsync(unidad);
        await context.Cargos.AddAsync(cargo);
        await context.Puestos.AddRangeAsync([visible, inactive, deleted]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
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
            context.Puestos.RemoveRange(visible, inactive, deleted);
            context.Cargos.Remove(cargo);
            context.UnidadesOrganizativas.Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListAllAsync_IncluyeRelacionesUnidadOrganizativaYCargo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-UO-REL");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-CARGO-REL");
        var visible = RepositoryTestData.CreatePuesto("PUESTO-REL", unidad, cargo);

        await context.UnidadesOrganizativas.AddAsync(unidad);
        await context.Cargos.AddAsync(cargo);
        await context.Puestos.AddAsync(visible);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var entidades = await repo.ListAllAsync(default);

            var encontrado = Assert.Single(entidades, entidad => entidad.Id == visible.Id);
            Assert.NotNull(encontrado.UnidadOrganizativa);
            Assert.NotNull(encontrado.Cargo);
            Assert.Equal(unidad.Id, encontrado.UnidadOrganizativaId);
            Assert.Equal(unidad.Nombre, encontrado.UnidadOrganizativa.Nombre);
            Assert.Equal(cargo.Id, encontrado.CargoId);
            Assert.Equal(cargo.Nombre, encontrado.Cargo.Nombre);
        }
        finally
        {
            context.Puestos.Remove(visible);
            context.Cargos.Remove(cargo);
            context.UnidadesOrganizativas.Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_IncluyeRelaciones()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-UO-BY-ID");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-CARGO-BY-ID");
        var expected = RepositoryTestData.CreatePuesto("PUESTO-BY-ID", unidad, cargo);

        await context.UnidadesOrganizativas.AddAsync(unidad);
        await context.Cargos.AddAsync(cargo);
        await context.Puestos.AddAsync(expected);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var encontrada = await repo.GetByIdAsync(expected.Id, default);

            Assert.NotNull(encontrada);
            Assert.Equal(expected.Id, encontrada!.Id);
            Assert.NotNull(encontrada.UnidadOrganizativa);
            Assert.NotNull(encontrada.Cargo);
            Assert.Equal(unidad.Id, encontrada.UnidadOrganizativaId);
            Assert.Equal(unidad.Nombre, encontrada.UnidadOrganizativa.Nombre);
            Assert.Equal(cargo.Id, encontrada.CargoId);
            Assert.Equal(cargo.Nombre, encontrada.Cargo.Nombre);
        }
        finally
        {
            context.Puestos.Remove(expected);
            context.Cargos.Remove(cargo);
            context.UnidadesOrganizativas.Remove(unidad);
            await context.SaveChangesAsync();
        }
    }
}
