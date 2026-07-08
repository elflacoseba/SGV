using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Organizacion;
using System.Data;
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new UnidadOrganizativaRepository(context);
        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }

    // ===================== Write tests (added in PR 2) =====================

    [MySqlFact]
    public async Task AddAsync_PersisteEntidadYAsignaId()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new UnidadOrganizativaRepository(context);
        var unidad = new UnidadOrganizativa("UO-ADD", "Unidad Add Test", TipoUnidadOrganizativaConstantes.AreaId, "Creada en test", null)
        {
            Id = Guid.NewGuid()
        };

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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateUnidadOrganizativa("UO-UPD");
        await context.Set<UnidadOrganizativaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var unidad = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(unidad);

            var codigoOriginal = unidad!.Codigo;

            // PR1 compatibility: Actualizar retorna una nueva instancia via `with`.
            // El test verifica que el Codigo PERSISTIDO es el ORIGINAL, no el que
            // se intento setear, porque Actualizar no acepta codigo como parametro.
            var actualizada = unidad.Actualizar(
                "Nombre actualizado",
                "Actualizado",
                TipoUnidadOrganizativaConstantes.DireccionId,
                null,
                null,
                null);
            await repo.UpdateAsync(actualizada, default);
            await context.SaveChangesAsync();

            var updated = await context.Set<UnidadOrganizativaEntity>()
                .FirstOrDefaultAsync(e => e.Id == entity.Id);
            Assert.NotNull(updated);
            // El Codigo persistido debe ser el ORIGINAL, no el que el test intento
            // pasar al Actualizar. Esto valida el invariante en la capa de persistencia.
            Assert.Equal(codigoOriginal, updated!.Codigo);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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

    // ===================== ReactivateAsync tests =====================

    [MySqlFact]
    public async Task ReactivateAsync_UnidadEliminada_RestauraFlags()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateUnidadOrganizativa("UO-REACT", isDeleted: true, isActive: false);
        entity.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            await repo.ReactivateAsync(entity.Id, default);
            await context.SaveChangesAsync();

            var reactivated = await context.Set<UnidadOrganizativaEntity>()
                .FirstOrDefaultAsync(e => e.Id == entity.Id);
            Assert.NotNull(reactivated);
            Assert.True(reactivated!.IsActive);
            Assert.False(reactivated.IsDeleted);
            Assert.Null(reactivated.DeletedAt);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task ReactivateAsync_UnidadInexistente_NoLanzaExcepcion()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new UnidadOrganizativaRepository(context);

        var exception = await Record.ExceptionAsync(() =>
            repo.ReactivateAsync(Guid.NewGuid(), default));

        Assert.Null(exception);
    }

    // ===================== QueryAsync tests =====================

    [MySqlFact]
    public async Task QueryAsync_SinFiltros_RetornaTodasLasActivas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var searchToken = $"SF{Guid.NewGuid():N}"[..10];
        var u1 = RepositoryTestData.CreateUnidadOrganizativa($"UO-{searchToken}-A");
        var u2 = RepositoryTestData.CreateUnidadOrganizativa($"UO-{searchToken}-B");
        u1.Nombre = $"Unidad {searchToken} A";
        u2.Nombre = $"Unidad {searchToken} B";
        var activeCountBefore = await context.Set<UnidadOrganizativaEntity>()
            .CountAsync(u => u.IsActive && !u.IsDeleted);

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([u1, u2]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            const int pageSize = 10;
            var pages = (int)Math.Ceiling((activeCountBefore + 2d) / pageSize);
            var allItems = new List<UnidadOrganizativa>();
            var observedTotalCount = 0;

            for (var page = 1; page <= pages; page++)
            {
                var (pageItems, totalCount) = await repo.QueryAsync(
                    null, null, null, null, page, pageSize, default);

                observedTotalCount = totalCount;
                allItems.AddRange(pageItems);
            }

            Assert.Equal(activeCountBefore + 2, observedTotalCount);
            Assert.Contains(allItems, i => i.Id == u1.Id);
            Assert.Contains(allItems, i => i.Id == u2.Id);
            Assert.Contains(allItems, i => i.Codigo.Contains(searchToken));
            Assert.All(allItems, i =>
            {
                Assert.True(i.IsActive);
                Assert.False(i.IsDeleted);
            });
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_FiltroPorTipoUnidadOrganizativaId_RetornaSoloCoincidencias()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var u1 = RepositoryTestData.CreateUnidadOrganizativa("UO-Q-TIPO-1");
        // Create a dedicated TipoUnidadOrganizativa to avoid collisions with seeded data.
        var tipoFiltro = new TipoUnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"TEST-FILTRO-{Guid.NewGuid().ToString("N")[..8]}",
            Nombre = "Tipo Filtro Test"
        };
        var u2 = new UnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"UO-Q-TIPO-2-{Guid.NewGuid().ToString("N")[..8]}",
            Nombre = "Unidad con otro tipo",
            TipoUnidadOrganizativaId = tipoFiltro.Id,
            IsActive = true
        };

        await context.Set<TipoUnidadOrganizativaEntity>().AddAsync(tipoFiltro);
        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([u1, u2]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                null, tipoFiltro.Id, null, null, 1, 20, default);

            Assert.Equal(1, totalCount);
            Assert.Contains(items, i => i.Id == u2.Id);
            Assert.DoesNotContain(items, i => i.Id == u1.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(u1, u2);
            context.Set<TipoUnidadOrganizativaEntity>().Remove(tipoFiltro);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_BusquedaPorCodigo_RetornaCoincidencias()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
    public async Task QueryAsync_SegmentoActivas_RetornaSoloActivas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SA{Guid.NewGuid():N}"[..10];
        var activa = RepositoryTestData.CreateUnidadOrganizativa($"UO-{searchToken}");
        activa.Nombre = $"Unidad {searchToken}";
        var eliminada = RepositoryTestData.CreateUnidadOrganizativa($"DEL-{searchToken}", isDeleted: true, isActive: false);
        eliminada.Nombre = $"Unidad eliminada {searchToken}";
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, null, null, null, 1, 20,
                UnidadOrganizativaSegmentoListado.Activas, default);

            var activaEncontrada = Assert.Single(items, i => i.Id == activa.Id);
            Assert.Equal(1, totalCount);
            Assert.DoesNotContain(items, i => i.Id == eliminada.Id);
            Assert.All(items, i =>
            {
                Assert.True(i.IsActive);
                Assert.False(i.IsDeleted);
            });
            Assert.Equal(activa.Id, activaEncontrada.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_SegmentoEliminadas_RetornaSoloEliminadas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SD{Guid.NewGuid():N}"[..10];
        var activa = RepositoryTestData.CreateUnidadOrganizativa($"ACT-{searchToken}");
        activa.Nombre = $"Unidad activa {searchToken}";
        var eliminada = RepositoryTestData.CreateUnidadOrganizativa($"UO-{searchToken}", isDeleted: true, isActive: false);
        eliminada.Nombre = $"Unidad eliminada {searchToken}";
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, null, null, null, 1, 20,
                UnidadOrganizativaSegmentoListado.Eliminadas, default);

            var eliminadaEncontrada = Assert.Single(items, i => i.Id == eliminada.Id);
            Assert.Equal(1, totalCount);
            Assert.DoesNotContain(items, i => i.Id == activa.Id);
            Assert.All(items, i =>
            {
                Assert.False(i.IsActive);
                Assert.True(i.IsDeleted);
            });
            Assert.Equal(eliminada.Id, eliminadaEncontrada.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_SegmentosNoSeMezclan()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SM{Guid.NewGuid():N}"[..10];
        var activa = RepositoryTestData.CreateUnidadOrganizativa($"ACT-{searchToken}");
        activa.Nombre = $"Unidad activa {searchToken}";
        var eliminada = RepositoryTestData.CreateUnidadOrganizativa($"DEL-{searchToken}", isDeleted: true, isActive: false);
        eliminada.Nombre = $"Unidad eliminada {searchToken}";
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (activas, totalActivas) = await repo.QueryAsync(
                searchToken, null, null, null, 1, 20,
                UnidadOrganizativaSegmentoListado.Activas, default);
            var (eliminadas, totalEliminadas) = await repo.QueryAsync(
                searchToken, null, null, null, 1, 20,
                UnidadOrganizativaSegmentoListado.Eliminadas, default);

            var activaEncontrada = Assert.Single(activas, i => i.Id == activa.Id);
            var eliminadaEncontrada = Assert.Single(eliminadas, i => i.Id == eliminada.Id);
            Assert.Equal(1, totalActivas);
            Assert.Equal(1, totalEliminadas);
            Assert.DoesNotContain(activas, i => i.Id == eliminada.Id);
            Assert.DoesNotContain(eliminadas, i => i.Id == activa.Id);
            Assert.Equal(activa.Id, activaEncontrada.Id);
            Assert.Equal(eliminada.Id, eliminadaEncontrada.Id);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_SinResultados_RetornaListaVaciaYTotalCero()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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

    // ===================== Mapper reflection guard (PR2) =====================

    /// <summary>
    /// Asserts that the persistence mapper for <see cref="UnidadOrganizativaEntity"/>
    /// does NOT call the internal <c>SetProperty</c> helper. That helper uses
    /// <see cref="BindingFlags.NonPublic"/> reflection to assign properties via
    /// <see cref="System.Reflection.PropertyInfo.SetValue(object, object?)"/>,
    /// which bypasses the C# <c>init</c>-only modifier at runtime. The design
    /// requires pure <c>with</c> expressions on the record so the
    /// <c>init</c>-only contract of <see cref="UnidadOrganizativa"/> holds
    /// end-to-end.
    /// </summary>
    [Fact]
    public void ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(UnidadOrganizativaRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(UnidadOrganizativaEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(UnidadOrganizativaEntity) not found.");
        var methodBody = method.GetMethodBody()
            ?? throw new InvalidOperationException(
                "ToDomain has no IL body (abstract/extern?).");
        var il = methodBody.GetILAsByteArray()
            ?? throw new InvalidOperationException(
                "ToDomain IL body returned no bytes.");
        var module = method.Module;

        MethodInfo? setPropertyCall = null;
        for (var i = 0; i < il.Length; i++)
        {
            // call = 0x28, callvirt = 0x6F. Both consume a 4-byte metadata token.
            if ((il[i] != 0x28 && il[i] != 0x6F) || i + 4 >= il.Length)
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, i + 1);
            try
            {
                if (module.ResolveMethod(token) is MethodInfo called
                    && called.Name == "SetProperty"
                    && called.DeclaringType == mapperType)
                {
                    setPropertyCall = called;
                    break;
                }
            }
            catch (ArgumentException)
            {
                // Token may resolve to a field reference (ld*fld) rather than a method.
            }

            i += 4;
        }

        Assert.Null(setPropertyCall);
    }
}
