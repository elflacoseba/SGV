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

    // ===================== Update de Codigo (Review PR1) =====================
    //
    // Cobertura MySQL real para el cambio de `Codigo` en update. Confirma que
    // el índice `IX_Cargos_ActiveCodigoUnique` actúa como árbitro final
    // (cubre caso exitoso, duplicado activo y reutilización tras soft-delete).

    [MySqlFact]
    public async Task UpdateAsync_CambiaCodigo_ActualizaColumnaActivaYComputedColumn()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var codigoInicial = $"CRG-OLD-{sufijo}";
        var codigoNuevo = $"CRG-NEW-{sufijo}";
        var entity = RepositoryTestData.CreateCargo("CRG", NivelIdValido);
        entity.Codigo = codigoInicial;
        await context.Set<CargoEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var cargo = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(cargo);

            cargo!.Actualizar(codigoNuevo, "Renombrado", NivelIdValido, "Desc nueva");
            await repo.UpdateAsync(cargo, default);
            await context.SaveChangesAsync();

            // La columna Codigo quedó persistida con el nuevo valor.
            var modificado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal(codigoNuevo, modificado!.Codigo);

            // El índice (columna computada ActiveCodigoUnique) refleja el nuevo
            // código y por lo tanto ExistsActiveCodeAsync lo encuentra.
            var existeNuevo = await repo.ExistsActiveCodeAsync(codigoNuevo, default);
            Assert.True(existeNuevo);

            // El código viejo ya no existe entre los activos.
            var existeViejo = await repo.ExistsActiveCodeAsync(codigoInicial, default);
            Assert.False(existeViejo);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>().Where(c => c.Id == entity.Id).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_CodigoDuplicadoActivo_LanzaDbUpdateException()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var codigoA = $"CRG-DUP-A-{sufijo}";
        var codigoB = $"CRG-DUP-B-{sufijo}";

        // Crea dos cargos activos con códigos distintos.
        var entityA = RepositoryTestData.CreateCargo("CRG", NivelIdValido);
        entityA.Codigo = codigoA;
        var entityB = RepositoryTestData.CreateCargo("CRG", NivelIdValido);
        entityB.Codigo = codigoB;
        await context.Set<CargoEntity>().AddRangeAsync(entityA, entityB);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);

            // Intenta cambiar el código de B al código de A → el índice único
            // activo debe rechazar la operación. Se bypasea el servicio para
            // probar el índice como árbitro final.
            var cargoB = await repo.GetByIdForUpdateAsync(entityB.Id, default);
            Assert.NotNull(cargoB);

            cargoB!.Actualizar(codigoA, "B renombrado", NivelIdValido);

            await repo.UpdateAsync(cargoB, default);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            // El mensaje debe mencionar el índice activo de cargo.
            var message = ex.InnerException?.Message ?? ex.Message;
            Assert.True(
                message.Contains("IX_Cargos_ActiveCodigoUnique", StringComparison.Ordinal)
                || message.Contains("ActiveCodigoUnique", StringComparison.Ordinal),
                $"Mensaje inesperado: {message}");
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>()
                    .Where(c => c.Id == entityA.Id || c.Id == entityB.Id)
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_CodigoSoftDeleted_PermiteReutilizarCodigo()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var codigoReuso = $"CRG-REUSE-{sufijo}";
        var codigoBInicial = $"CRG-REUSE-B-{sufijo}";

        // Cargo A: activo, con el código que será reutilizado
        var entityA = RepositoryTestData.CreateCargo("CRG", NivelIdValido);
        entityA.Codigo = codigoReuso;
        // Cargo B: activo, con código distinto
        var entityB = RepositoryTestData.CreateCargo("CRG", NivelIdValido);
        entityB.Codigo = codigoBInicial;
        await context.Set<CargoEntity>().AddRangeAsync(entityA, entityB);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);

            // Soft-delete de A (deja IsDeleted=true y columna computada = NULL)
            await repo.DeleteAsync(entityA.Id, default);
            await context.SaveChangesAsync();

            // Update de B al código de A (que ahora está soft-deleted) → debe pasar.
            var cargoB = await repo.GetByIdForUpdateAsync(entityB.Id, default);
            Assert.NotNull(cargoB);

            cargoB!.Actualizar(codigoReuso, "B reusa código de A", NivelIdValido);
            await repo.UpdateAsync(cargoB, default);
            await context.SaveChangesAsync(); // No debe lanzar

            // B ahora tiene el código reusado y sigue activo.
            var modificado = await repo.GetByIdAsync(entityB.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal(codigoReuso, modificado!.Codigo);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>()
                    .Where(c => c.Id == entityA.Id || c.Id == entityB.Id)
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }
}
