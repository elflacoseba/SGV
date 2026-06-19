using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Repository tests for Habilidad read and write operations.
/// </summary>
public sealed class HabilidadRepositoryTests
{
    // ===================== Read tests =====================

    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new HabilidadRepository(context);
        var entidades = await repo.ListAllAsync(default);

        // Seed data includes active Habilidades that are not deleted
        Assert.NotEmpty(entidades);
        Assert.All(entidades, entidad => Assert.IsType<Habilidad>(entidad));
        Assert.All(entidades, e => Assert.False(e.IsDeleted));
    }

    [MySqlFact]
    public async Task ListAllAsync_RetornaHabilidadesOrdenadasPorCodigo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new HabilidadRepository(context);
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

        var repo = new HabilidadRepository(context);
        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }

    // ===================== Write tests =====================

    [MySqlFact]
    public async Task AddAsync_AgregaHabilidad_YLuegoSePuedeConsultar()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new HabilidadRepository(context);
        var habilidad = new Habilidad("TEST-HAB-01", "Test Habilidad", "Test", "Test desc");

        await repo.AddAsync(habilidad, default);
        await context.SaveChangesAsync();

        try
        {
            var obtenido = await repo.GetByIdAsync(habilidad.Id, default);
            Assert.NotNull(obtenido);
            Assert.Equal(habilidad.Codigo, obtenido!.Codigo);
            Assert.Equal(habilidad.Nombre, obtenido.Nombre);
            Assert.Equal(habilidad.Categoria, obtenido.Categoria);
            Assert.Equal(habilidad.Descripcion, obtenido.Descripcion);
            Assert.True(obtenido.IsActive);
            Assert.False(obtenido.IsDeleted);
        }
        finally
        {
            context.Set<HabilidadEntity>().RemoveRange(
                await context.Set<HabilidadEntity>().Where(h => h.Id == habilidad.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_RetornaHabilidadActiva()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-UPDATE");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
            Assert.True(obtenido.IsActive);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_HabilidadInactiva_RetornaNull()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-INACT");
        entity.IsActive = false;
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.Null(obtenido);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdIncludingDeletedAsync_RetornaHabilidadInactiva()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-DEL");
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            var obtenido = await repo.GetByIdIncludingDeletedAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ModificaCampos()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-MOD");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            var habilidad = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(habilidad);

            habilidad!.Actualizar("Modificado", "NuevaCategoria", "Desc modificada");
            await repo.UpdateAsync(habilidad, default);
            await context.SaveChangesAsync();

            var modificado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal("Modificado", modificado!.Nombre);
            Assert.Equal("NuevaCategoria", modificado.Categoria);
            Assert.Equal("Desc modificada", modificado.Descripcion);
            Assert.Equal(entity.Codigo, modificado.Codigo); // Codigo unchanged
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(
                await context.Set<HabilidadEntity>().FirstAsync(h => h.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_MarcaComoInactivoYEliminado()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-DEL2");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            await repo.DeleteAsync(entity.Id, default);
            await context.SaveChangesAsync();

            // Should not appear in active query
            var activo = await repo.GetByIdAsync(entity.Id, default);
            Assert.Null(activo);

            // Should appear in including-deleted query
            var incluyendoEliminado = await repo.GetByIdIncludingDeletedAsync(entity.Id, default);
            Assert.NotNull(incluyendoEliminado);
            Assert.False(incluyendoEliminado!.IsActive);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(
                await context.Set<HabilidadEntity>().FirstAsync(h => h.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ReactivateAsync_RestauraEstadoActivo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-REACT");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            await repo.DeleteAsync(entity.Id, default);
            await context.SaveChangesAsync();

            await repo.ReactivateAsync(entity.Id, default);
            await context.SaveChangesAsync();

            var reactivado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(reactivado);
            Assert.True(reactivado!.IsActive);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(
                await context.Set<HabilidadEntity>().FirstAsync(h => h.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_CodigoExistente_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-EXIST");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);

            var exists = await repo.ExistsActiveCodeAsync(entity.Codigo, default);

            Assert.True(exists);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_ExcluyendoId_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-EXCL");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);

            var exists = await repo.ExistsActiveCodeAsync(entity.Codigo, entity.Id, default);

            Assert.False(exists);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
