using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Repository tests for Cargo read and write operations.
/// </summary>
public sealed class CargoRepositoryTests
{
    private static readonly Guid NivelIdValido = NivelCargoConstantes.DirectivoId;

    // ===================== Read tests =====================

    [MySqlFact]
    public async Task ListAllAsync_ExcluyeEntidadesInactivasYEliminadas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var visible = RepositoryTestData.CreateCargo("CRG-VISIBLE", NivelIdValido);
        var inactive = RepositoryTestData.CreateCargo("CRG-INACTIVE", NivelIdValido);
        inactive.IsActive = false;

        await context.Set<CargoEntity>().AddRangeAsync([visible, inactive]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var entidades = await repo.ListAllAsync(default);

            Assert.All(entidades, entidad => Assert.IsType<Cargo>(entidad));
            Assert.Contains(entidades, entidad => entidad.Id == visible.Id);
            Assert.DoesNotContain(entidades, entidad => entidad.Id == inactive.Id);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(visible, inactive);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListAllAsync_RetornaCargosOrdenadosPorCodigo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new CargoRepository(context);
        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }

    // ===================== Write tests =====================

    [MySqlFact]
    public async Task AddAsync_AgregaCargo_YLuegoSePuedeConsultar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoRepository(context);
        var cargo = new Cargo("TEST-CRG-01", "Test Cargo", NivelIdValido, "Test desc");

        await repo.AddAsync(cargo, default);
        await context.SaveChangesAsync();

        try
        {
            var obtenido = await repo.GetByIdAsync(cargo.Id, default);
            Assert.NotNull(obtenido);
            Assert.Equal(cargo.Codigo, obtenido!.Codigo);
            Assert.Equal(cargo.Nombre, obtenido.Nombre);
            Assert.Equal(cargo.NivelId, obtenido.NivelId);
            Assert.Equal(cargo.Descripcion, obtenido.Descripcion);
            Assert.True(obtenido.IsActive);
            Assert.False(obtenido.IsDeleted);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>().Where(c => c.Id == cargo.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_RetornaCargoActivo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-UPDATE", NivelIdValido);
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
            Assert.True(obtenido.IsActive);
        }
        finally
        {
            context.Set<CargoEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_CargoInactivo_RetornaNull()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-INACT", NivelIdValido);
        entity.IsActive = false;
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var obtenido = await repo.GetByIdForUpdateAsync(entity.Id, default);

            Assert.Null(obtenido);
        }
        finally
        {
            context.Set<CargoEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdIncludingDeletedAsync_RetornaCargoInactivo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-DEL", NivelIdValido);
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var obtenido = await repo.GetByIdIncludingDeletedAsync(entity.Id, default);

            Assert.NotNull(obtenido);
            Assert.Equal(entity.Id, obtenido!.Id);
        }
        finally
        {
            context.Set<CargoEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ModificaCampos()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-MOD", NivelIdValido);
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var cargo = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(cargo);

            cargo!.Actualizar(entity.Codigo, "Modificado", NivelCargoConstantes.ConduccionMediaId, "Desc modificada");
            await repo.UpdateAsync(cargo, default);
            await context.SaveChangesAsync();

            var modificado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal("Modificado", modificado!.Nombre);
            Assert.Equal(NivelCargoConstantes.ConduccionMediaId, modificado.NivelId);
            Assert.Equal("Desc modificada", modificado.Descripcion);
            Assert.Equal(entity.Codigo, modificado.Codigo); // Codigo unchanged in this scenario
        }
        finally
        {
            context.Set<CargoEntity>().Remove(
                await context.Set<CargoEntity>().FirstAsync(c => c.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_MarcaComoInactivoYEliminado()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-DEL2", NivelIdValido);
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
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
            context.Set<CargoEntity>().Remove(
                await context.Set<CargoEntity>().FirstAsync(c => c.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ReactivateAsync_RestauraEstadoActivo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-REACT", NivelIdValido);
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
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
            context.Set<CargoEntity>().Remove(
                await context.Set<CargoEntity>().FirstAsync(c => c.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_CodigoExistente_RetornaTrue()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-EXIST", NivelIdValido);
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);

            var exists = await repo.ExistsActiveCodeAsync(entity.Codigo, default);

            Assert.True(exists);
        }
        finally
        {
            context.Set<CargoEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_ExcluyendoId_RetornaFalse()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateCargo("CRG-EXCL", NivelIdValido);
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);

            var exists = await repo.ExistsActiveCodeAsync(entity.Codigo, entity.Id, default);

            Assert.False(exists);
        }
        finally
        {
            context.Set<CargoEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
