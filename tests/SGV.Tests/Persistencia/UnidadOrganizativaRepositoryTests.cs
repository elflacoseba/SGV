using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// MySQL repository integration tests for UnidadOrganizativa writes, soft-delete code reuse,
/// active-code uniqueness, and hierarchy checks. Skipped when MySQL is unavailable.
/// </summary>
public sealed class UnidadOrganizativaRepositoryTests
{
    // ===================== Read tests (existing) =====================

    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesInactivasYEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var visible = RepositoryTestData.CreateUnidadOrganizativa("UO-VISIBLE");
        var inactive = RepositoryTestData.CreateUnidadOrganizativa("UO-INACTIVE", isActive: false);
        var deleted = RepositoryTestData.CreateUnidadOrganizativa("UO-DELETED", isDeleted: true);

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([visible, inactive, deleted]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var entidades = await repo.ListAllAsync(default);

            Assert.All(entidades, entidad => Assert.IsType<UnidadOrganizativa>(entidad));
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
            context.Set<UnidadOrganizativaEntity>().RemoveRange(visible, inactive, deleted);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaEntidadCuandoExiste()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var expected = RepositoryTestData.CreateUnidadOrganizativa("UO-BY-ID");

        await context.Set<UnidadOrganizativaEntity>().AddAsync(expected);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var encontrada = await repo.GetByIdAsync(expected.Id, default);

            Assert.NotNull(encontrada);
            Assert.IsType<UnidadOrganizativa>(encontrada);
            Assert.Equal(expected.Id, encontrada!.Id);
            Assert.Equal(expected.Codigo, encontrada.Codigo);
            Assert.Equal(expected.Nombre, encontrada.Nombre);
            Assert.True(encontrada.IsActive);
            Assert.False(encontrada.IsDeleted);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(expected);
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

    // ===================== Write tests (added in PR 2) =====================

    [MySqlFact]
    public async Task AddAsync_PersisteEntidadYAsignaId()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new UnidadOrganizativaRepository(context);
        var unidad = new UnidadOrganizativa("UO-ADD", "Unidad Add Test", "TEST")
        {
            Id = Guid.NewGuid()
        };
        unidad.CambiarDatos("UO-ADD", "Unidad Add Test", "TEST", "Creada en test");

        await repo.AddAsync(unidad, default);
        await context.SaveChangesAsync();

        try
        {
            var entity = await context.Set<UnidadOrganizativaEntity>()
                .FirstOrDefaultAsync(e => e.Id == unidad.Id);
            Assert.NotNull(entity);
            Assert.Equal("UO-ADD", entity!.Codigo);
            Assert.Equal("Unidad Add Test", entity.Nombre);
            Assert.True(entity.IsActive);
            Assert.False(entity.IsDeleted);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(
                await context.Set<UnidadOrganizativaEntity>().Where(e => e.Id == unidad.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ModificaEntidadExistente()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateUnidadOrganizativa("UO-UPD");
        await context.Set<UnidadOrganizativaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var unidad = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(unidad);

            unidad!.CambiarDatos("UO-UPD-2", "Nombre actualizado", "NUEVO_TIPO", "Actualizado");
            await repo.UpdateAsync(unidad, default);
            await context.SaveChangesAsync();

            var updated = await context.Set<UnidadOrganizativaEntity>()
                .FirstOrDefaultAsync(e => e.Id == entity.Id);
            Assert.NotNull(updated);
            Assert.Equal("UO-UPD-2", updated!.Codigo);
            Assert.Equal("Nombre actualizado", updated.Nombre);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_MarcaInactivoYEliminado()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateUnidadOrganizativa("UO-DEL");
        await context.Set<UnidadOrganizativaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            await repo.DeleteAsync(entity.Id, default);
            await context.SaveChangesAsync();

            var deleted = await context.Set<UnidadOrganizativaEntity>()
                .FirstOrDefaultAsync(e => e.Id == entity.Id);
            Assert.NotNull(deleted);
            Assert.False(deleted!.IsActive);
            Assert.True(deleted.IsDeleted);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_CodigoActivoDuplicado_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateUnidadOrganizativa("UO-DUP");
        await context.Set<UnidadOrganizativaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var exists = await repo.ExistsActiveCodeAsync(entity.Codigo, cancellationToken: default);

            Assert.True(exists);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_ExcluyendoPropioId_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateUnidadOrganizativa("UO-EXCL");
        await context.Set<UnidadOrganizativaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var exists = await repo.ExistsActiveCodeAsync(entity.Codigo, entity.Id, default);

            Assert.False(exists);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task IsDescendantAsync_RelacionDirecta_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var padre = RepositoryTestData.CreateUnidadOrganizativa("UO-PADRE");
        var hijo = RepositoryTestData.CreateUnidadOrganizativa("UO-HIJO");
        hijo.UnidadPadreId = padre.Id;

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([padre, hijo]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var isDescendant = await repo.IsDescendantAsync(hijo.Id, padre.Id, default);

            Assert.True(isDescendant);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(padre, hijo);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task IsDescendantAsync_SinRelacion_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad1 = RepositoryTestData.CreateUnidadOrganizativa("UO-A");
        var unidad2 = RepositoryTestData.CreateUnidadOrganizativa("UO-B");
        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([unidad1, unidad2]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var isDescendant = await repo.IsDescendantAsync(unidad2.Id, unidad1.Id, default);

            Assert.False(isDescendant);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(unidad1, unidad2);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task SoftDelete_ReutilizaCodigo_EnNuevaUnidadActiva()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var original = RepositoryTestData.CreateUnidadOrganizativa("UO-REUSE");
        await context.Set<UnidadOrganizativaEntity>().AddAsync(original);
        await context.SaveChangesAsync();

        try
        {
            // Soft-delete original
            var repo = new UnidadOrganizativaRepository(context);
            await repo.DeleteAsync(original.Id, default);
            await context.SaveChangesAsync();

            // Same code should be available for new active unit
            var exists = await repo.ExistsActiveCodeAsync(original.Codigo, cancellationToken: default);
            Assert.False(exists);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(original);
            await context.SaveChangesAsync();
        }
    }
}
