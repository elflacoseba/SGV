using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
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
        var unidad = new UnidadOrganizativa("UO-ADD", "Unidad Add Test", TipoUnidadOrganizativaConstantes.AreaId)
        {
            Id = Guid.NewGuid()
        };
        unidad.CambiarDatos("UO-ADD", "Unidad Add Test", TipoUnidadOrganizativaConstantes.AreaId, "Creada en test");

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

            unidad!.CambiarDatos("UO-UPD-2", "Nombre actualizado", TipoUnidadOrganizativaConstantes.DireccionId, "Actualizado");
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

    // ===================== HasActiveChildrenAsync tests =====================

    [MySqlFact]
    public async Task HasActiveChildrenAsync_UnidadConHijaActiva_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var padre = RepositoryTestData.CreateUnidadOrganizativa("UO-PADRE");
        var hija = RepositoryTestData.CreateUnidadOrganizativa("UO-HIJA");
        hija.UnidadPadreId = padre.Id;

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([padre, hija]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.HasActiveChildrenAsync(padre.Id, default);

            Assert.True(result);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(padre, hija);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task HasActiveChildrenAsync_UnidadSinHijas_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var padre = RepositoryTestData.CreateUnidadOrganizativa("UO-SIN-HIJAS");

        await context.Set<UnidadOrganizativaEntity>().AddAsync(padre);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.HasActiveChildrenAsync(padre.Id, default);

            Assert.False(result);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(padre);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task HasActiveChildrenAsync_UnidadConHijaInactiva_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var padre = RepositoryTestData.CreateUnidadOrganizativa("UO-PADRE");
        var hijaInactiva = RepositoryTestData.CreateUnidadOrganizativa("UO-HIJA-INACT", isActive: false);
        hijaInactiva.UnidadPadreId = padre.Id;

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([padre, hijaInactiva]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.HasActiveChildrenAsync(padre.Id, default);

            Assert.False(result);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(padre, hijaInactiva);
            await context.SaveChangesAsync();
        }
    }

    // ===================== HasActivePuestosAsync tests =====================

    [MySqlFact]
    public async Task HasActivePuestosAsync_UnidadConPuestoActivo_RetornaTrue()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("UO-PUESTO");
        var cargo = RepositoryTestData.CreateCargo("CARGO-PUESTO");
        var puesto = RepositoryTestData.CreatePuesto("PUESTO-ACT", unidad, cargo, isActive: true);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puesto);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.HasActivePuestosAsync(unidad.Id, default);

            Assert.True(result);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puesto);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task HasActivePuestosAsync_UnidadSinPuestos_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("UO-SIN-PUESTOS");

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.HasActivePuestosAsync(unidad.Id, default);

            Assert.False(result);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task HasActivePuestosAsync_UnidadConPuestoEliminado_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("UO-PUESTO-DEL");
        var cargo = RepositoryTestData.CreateCargo("CARGO-DEL");
        var puesto = RepositoryTestData.CreatePuesto("PUESTO-DEL", unidad, cargo, isActive: false, isDeleted: true);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puesto);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.HasActivePuestosAsync(unidad.Id, default);

            Assert.False(result);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puesto);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task HasActivePuestosAsync_UnidadConPuestoInactivo_RetornaFalse()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("UO-PUESTO-INACT");
        var cargo = RepositoryTestData.CreateCargo("CARGO-INACT");
        var puesto = RepositoryTestData.CreatePuesto("PUESTO-INACT", unidad, cargo, isActive: false);

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddAsync(puesto);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.HasActivePuestosAsync(unidad.Id, default);

            Assert.False(result);
        }
        finally
        {
            context.Set<PuestoEntity>().Remove(puesto);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    // ===================== QueryAsync tests =====================

    [MySqlFact]
    public async Task QueryAsync_SinFiltros_RetornaTodasLasActivas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var u1 = RepositoryTestData.CreateUnidadOrganizativa("UO-Q-A");
        var u2 = RepositoryTestData.CreateUnidadOrganizativa("UO-Q-B");

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([u1, u2]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                null, null, null, null, 1, 20, default);

            Assert.True(totalCount >= 2, $"Expected totalCount >= 2, got {totalCount}");
            Assert.Contains(items, i => i.Id == u1.Id);
            Assert.Contains(items, i => i.Id == u2.Id);
            Assert.All(items, i =>
            {
                Assert.True(i.IsActive);
                Assert.False(i.IsDeleted);
            });
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(u1, u2);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_FiltroPorTipoUnidadOrganizativaId_RetornaSoloCoincidencias()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var u1 = RepositoryTestData.CreateUnidadOrganizativa("UO-Q-TIPO-1");
        // u2 will use a different TipoUnidadOrganizativaId
        var u2 = new UnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"UO-Q-TIPO-2-{Guid.NewGuid().ToString("N")[..8]}",
            Nombre = "Unidad con otro tipo",
            TipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.DireccionId,
            IsActive = true
        };

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([u1, u2]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                null, TipoUnidadOrganizativaConstantes.DireccionId, null, null, 1, 20, default);

            Assert.Equal(1, totalCount);
            Assert.Contains(items, i => i.Id == u2.Id);
            Assert.DoesNotContain(items, i => i.Id == u1.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(u1, u2);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_BusquedaPorCodigo_RetornaCoincidencias()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var u1 = RepositoryTestData.CreateUnidadOrganizativa("UO-SEARCH");
        var u2 = RepositoryTestData.CreateUnidadOrganizativa("OTRA-UO");

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([u1, u2]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                u1.Codigo, null, null, null, 1, 20, default);

            Assert.True(totalCount >= 1, $"Expected totalCount >= 1, got {totalCount}");
            Assert.Contains(items, i => i.Id == u1.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(u1, u2);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_BusquedaPorNombre_RetornaCoincidencias()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var u1 = new UnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"UO-NSEARCH-1-{prefix}",
            Nombre = $"NombreBuscado-{prefix}",
            TipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId,
            IsActive = true
        };
        var u2 = new UnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"UO-NSEARCH-2-{prefix}",
            Nombre = $"OtroNombre-{prefix}",
            TipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId,
            IsActive = true
        };

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([u1, u2]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                "NombreBuscado", null, null, null, 1, 20, default);

            Assert.True(totalCount >= 1, $"Expected totalCount >= 1, got {totalCount}");
            Assert.Contains(items, i => i.Id == u1.Id);
            Assert.DoesNotContain(items, i => i.Id == u2.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(u1, u2);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_FiltroPorUnidadPadreId_RetornaSoloCoincidencias()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var padre = RepositoryTestData.CreateUnidadOrganizativa("UO-PADRE-FILTER");
        var hija = RepositoryTestData.CreateUnidadOrganizativa("UO-HIJA-FILTER");
        hija.UnidadPadreId = padre.Id;
        var otra = RepositoryTestData.CreateUnidadOrganizativa("UO-OTRA-FILTER");

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([padre, hija, otra]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                null, null, padre.Id, null, 1, 20, default);

            Assert.True(totalCount >= 1, $"Expected totalCount >= 1, got {totalCount}");
            Assert.Contains(items, i => i.Id == hija.Id);
            Assert.DoesNotContain(items, i => i.Id == otra.Id);
            Assert.DoesNotContain(items, i => i.Id == padre.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(padre, hija, otra);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_Paginacion_RetornaPaginaCorrecta()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var unidades = Enumerable.Range(0, 5)
            .Select(i => RepositoryTestData.CreateUnidadOrganizativa($"UO-PAGE-{i}"))
            .ToArray();

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync(unidades);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (itemsPage1, totalCount) = await repo.QueryAsync(
                null, null, null, null, 1, 2, default);

            Assert.True(totalCount >= 5, $"Expected totalCount >= 5, got {totalCount}");
            Assert.Equal(2, itemsPage1.Count);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(unidades);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_SinResultados_RetornaListaVaciaYTotalCero()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);

        var repo = new UnidadOrganizativaRepository(context);
        var (items, totalCount) = await repo.QueryAsync(
            "NO_EXISTE_ESTE_CODIGO_12345", null, null, null, 1, 20, default);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    // ===================== ListTreeAsync tests =====================

    [MySqlFact]
    public async Task ListTreeAsync_IncluyeTipoUnidadOrganizativa()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var u = new UnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"UO-TREE-TIPO-{Guid.NewGuid().ToString("N")[..8]}",
            Nombre = "Unidad Tree Tipo",
            TipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId,
            IsActive = true
        };

        await context.Set<UnidadOrganizativaEntity>().AddAsync(u);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.ListTreeAsync(default);

            var found = Assert.Single(result, i => i.Id == u.Id);
            Assert.Equal(TipoUnidadOrganizativaConstantes.AreaId, found.TipoUnidadOrganizativaId);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(u);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListTreeAsync_RetornaUnidadesActivasConTipoUnidad()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var u = RepositoryTestData.CreateUnidadOrganizativa("UO-TREE");

        await context.Set<UnidadOrganizativaEntity>().AddAsync(u);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.ListTreeAsync(default);

            var found = Assert.Single(result, i => i.Id == u.Id);
            Assert.Equal(u.Codigo, found.Codigo);
            Assert.Equal(u.Nombre, found.Nombre);
            Assert.True(found.IsActive);
            Assert.False(found.IsDeleted);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(u);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ListTreeAsync_ExcluyeUnidadesInactivasYEliminadas()
    {
        await using var context = new SgvDbContextFactory().CreateDbContext([]);
        var activa = RepositoryTestData.CreateUnidadOrganizativa("UO-TREE-ACT", isActive: true);
        var inactiva = RepositoryTestData.CreateUnidadOrganizativa("UO-TREE-INACT", isActive: false);
        var eliminada = RepositoryTestData.CreateUnidadOrganizativa("UO-TREE-DEL", isActive: false, isDeleted: true);

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([activa, inactiva, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var result = await repo.ListTreeAsync(default);

            Assert.Contains(result, i => i.Id == activa.Id);
            Assert.DoesNotContain(result, i => i.Id == inactiva.Id);
            Assert.DoesNotContain(result, i => i.Id == eliminada.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(activa, inactiva, eliminada);
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
