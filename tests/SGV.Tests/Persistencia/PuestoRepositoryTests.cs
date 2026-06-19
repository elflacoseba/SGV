using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Organizacion;
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

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([visible, inactive, deleted]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var entidades = await repo.ListAllAsync(default);

            Assert.All(entidades, entidad => Assert.IsType<Puesto>(entidad));
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
            context.Set<PuestoEntity>().RemoveRange(visible, inactive, deleted);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
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

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(visible);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var entidades = await repo.ListAllAsync(default);

            var encontrado = Assert.Single(entidades, entidad => entidad.Id == visible.Id);
            Assert.IsType<Puesto>(encontrado);
            Assert.NotNull(encontrado.UnidadOrganizativa);
            Assert.NotNull(encontrado.Cargo);
            Assert.IsType<UnidadOrganizativa>(encontrado.UnidadOrganizativa);
            Assert.IsType<Cargo>(encontrado.Cargo);
            Assert.Equal(unidad.Id, encontrado.UnidadOrganizativaId);
            Assert.Equal(unidad.Nombre, encontrado.UnidadOrganizativa.Nombre);
            Assert.Equal(cargo.Id, encontrado.CargoId);
            Assert.Equal(cargo.Nombre, encontrado.Cargo.Nombre);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(visible);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
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

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(expected);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var encontrada = await repo.GetByIdAsync(expected.Id, default);

            Assert.NotNull(encontrada);
            Assert.IsType<Puesto>(encontrada);
            Assert.Equal(expected.Id, encontrada!.Id);
            Assert.NotNull(encontrada.UnidadOrganizativa);
            Assert.NotNull(encontrada.Cargo);
            Assert.IsType<UnidadOrganizativa>(encontrada.UnidadOrganizativa);
            Assert.IsType<Cargo>(encontrada.Cargo);
            Assert.Equal(unidad.Id, encontrada.UnidadOrganizativaId);
            Assert.Equal(unidad.Nombre, encontrada.UnidadOrganizativa.Nombre);
            Assert.Equal(cargo.Id, encontrada.CargoId);
            Assert.Equal(cargo.Nombre, encontrada.Cargo.Nombre);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(expected);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task AddAsync_PersistePuestoActivoConRelaciones()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-ADD-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-ADD-CARGO");

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.SaveChangesAsync();

        var repo = new PuestoRepository(context);
        var puesto = new Puesto(unidad.Id, cargo.Id, "PUESTO-ADD", "Puesto Add Test")
        {
            Id = Guid.NewGuid()
        };

        try
        {
            await repo.AddAsync(puesto, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<PuestoEntity>()
                .FirstOrDefaultAsync(p => p.Id == puesto.Id);

            Assert.NotNull(entity);
            Assert.Equal("PUESTO-ADD", entity!.Codigo);
            Assert.Equal("Puesto Add Test", entity.Nombre);
            Assert.True(entity.IsActive);
            Assert.False(entity.IsDeleted);
            Assert.Equal(unidad.Id, entity.UnidadOrganizativaId);
            Assert.Equal(cargo.Id, entity.CargoId);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(context.Set<PuestoEntity>().Where(p => p.Codigo == "PUESTO-ADD"));
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_RetornaPuestoActivoConRelaciones()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-UPD-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-UPD-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-UPD", unidad, cargo);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var puesto = await repo.GetByIdForUpdateAsync(puestoEntity.Id, default);

            Assert.NotNull(puesto);
            Assert.Equal(puestoEntity.Id, puesto!.Id);
            Assert.NotNull(puesto.UnidadOrganizativa);
            Assert.NotNull(puesto.Cargo);
            Assert.Equal(unidad.Nombre, puesto.UnidadOrganizativa.Nombre);
            Assert.Equal(cargo.Nombre, puesto.Cargo.Nombre);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdForUpdateAsync_Eliminado_RetornaNull()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-UPD2-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-UPD2-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-UPD2", unidad, cargo, isDeleted: true);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var puesto = await repo.GetByIdForUpdateAsync(puestoEntity.Id, default);

            Assert.Null(puesto);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdIncludingDeletedAsync_RetornaIncluyendoEliminados()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-INCDEL-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-INCDEL-CARGO");
        var activo = RepositoryTestData.CreatePuesto("PUESTO-INCDEL-ACT", unidad, cargo);
        var eliminado = RepositoryTestData.CreatePuesto("PUESTO-INCDEL-DEL", unidad, cargo, isDeleted: true);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([activo, eliminado]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var encontradoActivo = await repo.GetByIdIncludingDeletedAsync(activo.Id, default);
            var encontradoEliminado = await repo.GetByIdIncludingDeletedAsync(eliminado.Id, default);

            Assert.NotNull(encontradoActivo);
            Assert.Equal(activo.Id, encontradoActivo!.Id);
            Assert.Equal(activo.IsDeleted, encontradoActivo.IsDeleted);

            Assert.NotNull(encontradoEliminado);
            Assert.Equal(eliminado.Id, encontradoEliminado!.Id);
            Assert.Equal(eliminado.IsDeleted, encontradoEliminado.IsDeleted);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(activo, eliminado);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_AplicaBajaLogica()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-DEL-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-DEL-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-DEL", unidad, cargo);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);

            await repo.DeleteAsync(puestoEntity.Id, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<PuestoEntity>()
                .FirstOrDefaultAsync(p => p.Id == puestoEntity.Id);

            Assert.NotNull(entity);
            Assert.False(entity!.IsActive);
            Assert.True(entity.IsDeleted);
            Assert.NotNull(entity.DeletedAt);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ReactivateAsync_RestauraEstadoActivo()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-REACT-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-REACT-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-REACT", unidad, cargo, isDeleted: true);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);

            await repo.ReactivateAsync(puestoEntity.Id, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<PuestoEntity>()
                .FirstOrDefaultAsync(p => p.Id == puestoEntity.Id);

            Assert.NotNull(entity);
            Assert.True(entity!.IsActive);
            Assert.False(entity.IsDeleted);
            Assert.Null(entity.DeletedAt);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ReactivateAsync_ConservaRelaciones()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-REACT-REL-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-REACT-REL-CARGO");
        var superior = RepositoryTestData.CreatePuesto("PUESTO-REACT-REL-SUP", unidad, cargo);
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-REACT-REL", unidad, cargo, isDeleted: true);
        puestoEntity.PuestoSuperiorId = superior.Id;

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync(superior, puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);

            await repo.ReactivateAsync(puestoEntity.Id, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<PuestoEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == puestoEntity.Id);

            Assert.NotNull(entity);
            Assert.Equal(unidad.Id, entity!.UnidadOrganizativaId);
            Assert.Equal(cargo.Id, entity.CargoId);
            Assert.Equal(superior.Id, entity.PuestoSuperiorId);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(superior, puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_Activo_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-EXIST-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-EXIST-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-EXIST", unidad, cargo);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);

            var existe = await repo.ExistsActiveCodeAsync(puestoEntity.Codigo, default);

            Assert.True(existe);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_SinActivo_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var repo = new PuestoRepository(context);

        var existe = await repo.ExistsActiveCodeAsync("NO-EXISTE-99999", default);

        Assert.False(existe);
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_Eliminado_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-EXDEL-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-EXDEL-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-EXDEL", unidad, cargo, isDeleted: true);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);

            var existe = await repo.ExistsActiveCodeAsync(puestoEntity.Codigo, default);

            Assert.False(existe);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ExistsActiveCodeAsync_ExcluyendoId_IgnoraElPropio()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-EXID-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-EXID-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-EXID", unidad, cargo);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);

            var existe = await repo.ExistsActiveCodeAsync(puestoEntity.Codigo, puestoEntity.Id, default);

            Assert.False(existe);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_ActualizaCamposEditables()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PUESTO-UPDT-UO");
        var cargo = RepositoryTestData.CreateCargo("PUESTO-UPDT-CARGO");
        var puestoEntity = RepositoryTestData.CreatePuesto("PUESTO-UPDT", unidad, cargo);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puestoEntity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);

            var puesto = await repo.GetByIdForUpdateAsync(puestoEntity.Id, default);
            Assert.NotNull(puesto);

            puesto!.Actualizar("Nombre Actualizado", "Descripción actualizada", null);
            await repo.UpdateAsync(puesto, default);
            await context.SaveChangesAsync();

            var entity = await context.Set<PuestoEntity>()
                .FirstOrDefaultAsync(p => p.Id == puestoEntity.Id);

            Assert.NotNull(entity);
            Assert.Equal("Nombre Actualizado", entity!.Nombre);
            Assert.Equal("Descripción actualizada", entity.Descripcion);
            Assert.Equal(puestoEntity.Codigo, entity.Codigo); // Sin cambios (inmutable)
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puestoEntity);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }
}
