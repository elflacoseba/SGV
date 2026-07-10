using Microsoft.EntityFrameworkCore;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new HabilidadRepository(context);
        var entidades = await repo.ListAllAsync(default);

        Assert.NotEmpty(entidades);
        for (var i = 1; i < entidades.Count; i++)
        {
            Assert.True(string.Compare(entidades[i - 1].Codigo, entidades[i].Codigo, StringComparison.Ordinal) <= 0);
        }
    }

    // ===================== Query segmentada (activas / eliminadas) =====================

    [MySqlFact]
    public async Task QueryAsync_SegmentoEliminadas_ExcluyeActivas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new HabilidadRepository(context);
        var entityActiva = RepositoryTestData.CreateHabilidad("HAB-ACTIVA");
        var entityEliminada = RepositoryTestData.CreateHabilidad("HAB-ELIM");
        entityEliminada.IsActive = false;
        entityEliminada.IsDeleted = true;
        entityEliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<HabilidadEntity>().AddRangeAsync(entityActiva, entityEliminada);
        await context.SaveChangesAsync();

        try
        {
            var (items, totalCount) = await repo.QueryAsync(
                search: null,
                page: 1,
                pageSize: 50,
                sort: null,
                segmento: HabilidadSegmentoListado.Eliminadas);

            Assert.Contains(items, h => h.Id == entityEliminada.Id);
            Assert.DoesNotContain(items, h => h.Id == entityActiva.Id);
            Assert.True(totalCount >= 1);
        }
        finally
        {
            context.Set<HabilidadEntity>().RemoveRange(
                await context.Set<HabilidadEntity>()
                    .Where(h => h.Id == entityActiva.Id || h.Id == entityEliminada.Id)
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_SortNombreDesc_AplicaAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new HabilidadRepository(context);
        // RepositoryTestData.CreateHabilidad appends a Guid suffix; capture
        // the resulting Codigo values to compare against the query result.
        var e1 = RepositoryTestData.CreateHabilidad("HAB-SORT-Z");
        var e2 = RepositoryTestData.CreateHabilidad("HAB-SORT-Y");
        var e3 = RepositoryTestData.CreateHabilidad("HAB-SORT-X");
        e1.Nombre = "Zeta";
        e2.Nombre = "Yankee";
        e3.Nombre = "Xray";
        var entities = new[] { e1, e2, e3 };

        await context.Set<HabilidadEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var expectedCodes = entities.Select(e => e.Codigo).ToList();
        var searchKey = expectedCodes[0][..Math.Min(8, expectedCodes[0].Length)];

        try
        {
            var (items, _) = await repo.QueryAsync(
                search: searchKey,
                page: 1,
                pageSize: 50,
                sort: "nombre_desc",
                segmento: HabilidadSegmentoListado.Activas);

            // Solo nos importan los recién insertados.
            var nuevos = items
                .Where(h => expectedCodes.Contains(h.Codigo))
                .Select(h => h.Nombre)
                .ToList();

            Assert.Equal(new[] { "Zeta", "Yankee", "Xray" }, nuevos);
        }
        finally
        {
            context.Set<HabilidadEntity>().RemoveRange(
                await context.Set<HabilidadEntity>()
                    .Where(h => h.Id == e1.Id || h.Id == e2.Id || h.Id == e3.Id)
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_SortDesconocido_CaeACodigoAsc()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new HabilidadRepository(context);
        // RepositoryTestData.CreateHabilidad appends a Guid suffix; use it
        // directly as our search/expected.
        var e1 = RepositoryTestData.CreateHabilidad("HAB-UNKN-A");
        var e2 = RepositoryTestData.CreateHabilidad("HAB-UNKN-B");
        var entities = new[] { e1, e2 };

        await context.Set<HabilidadEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var expectedCodes = entities.Select(e => e.Codigo).OrderBy(c => c).ToArray();

        try
        {
            var (items, _) = await repo.QueryAsync(
                search: "HAB-UNKN",
                page: 1,
                pageSize: 50,
                sort: "no_existe_este_sort",
                segmento: HabilidadSegmentoListado.Activas);

            var nuevos = items
                .Where(h => expectedCodes.Contains(h.Codigo))
                .Select(h => h.Codigo)
                .ToArray();

            // codigo_asc ordena alfabéticamente ascendente
            Assert.Equal(expectedCodes, nuevos);
        }
        finally
        {
            context.Set<HabilidadEntity>().RemoveRange(
                await context.Set<HabilidadEntity>()
                    .Where(h => h.Id == e1.Id || h.Id == e2.Id)
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task GetByIdAsync_RetornaNull_CuandoNoExiste()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);

        var repo = new HabilidadRepository(context);
        var noExiste = await repo.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(noExiste);
    }

    // ===================== Write tests =====================

    [MySqlFact]
    public async Task AddAsync_AgregaHabilidad_YLuegoSePuedeConsultar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-MOD");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            var habilidad = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(habilidad);

            var nuevoCodigo = entity.Codigo + "-V2";
            habilidad!.Actualizar(nuevoCodigo, "Modificado", "NuevaCategoria", "Desc modificada");
            await repo.UpdateAsync(habilidad, default);
            await context.SaveChangesAsync();

            var modificado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal("Modificado", modificado!.Nombre);
            Assert.Equal("NuevaCategoria", modificado.Categoria);
            Assert.Equal("Desc modificada", modificado.Descripcion);
            Assert.Equal(nuevoCodigo, modificado.Codigo);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(
                await context.Set<HabilidadEntity>().FirstAsync(h => h.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_MismoCodigo_NoViolaIndice()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var entity = RepositoryTestData.CreateHabilidad("HAB-SAME");
        await context.Set<HabilidadEntity>().AddAsync(entity);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            var habilidad = await repo.GetByIdForUpdateAsync(entity.Id, default);
            Assert.NotNull(habilidad);

            // Reenviar el mismo Codigo no debe chocar contra el índice único
            // porque el filtro `excludingId` lo excluye (a nivel de servicio) y
            // porque a nivel de DB la fila que se está actualizando no viola
            // consigo misma.
            habilidad!.Actualizar(entity.Codigo, "Nombre actualizado", null, null);
            await repo.UpdateAsync(habilidad, default);
            await context.SaveChangesAsync();

            var modificado = await repo.GetByIdAsync(entity.Id, default);
            Assert.NotNull(modificado);
            Assert.Equal(entity.Codigo, modificado!.Codigo);
            Assert.Equal("Nombre actualizado", modificado.Nombre);
        }
        finally
        {
            context.Set<HabilidadEntity>().Remove(
                await context.Set<HabilidadEntity>().FirstAsync(h => h.Id == entity.Id));
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task UpdateAsync_CodigoDuplicadoDeOtraActiva_ThrowsDbUpdateException()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var codigoCompartido = $"UNIQ-UP-{sufijo}";

        var objetivo = RepositoryTestData.CreateHabilidad("HAB-TGT");
        var otra = RepositoryTestData.CreateHabilidad("HAB-OTR");
        objetivo.Codigo = $"{codigoCompartido}-A";
        otra.Codigo = $"{codigoCompartido}-B";

        await context.Set<HabilidadEntity>().AddRangeAsync(objetivo, otra);
        await context.SaveChangesAsync();

        try
        {
            var repo = new HabilidadRepository(context);
            var habilidad = await repo.GetByIdForUpdateAsync(objetivo.Id, default);
            Assert.NotNull(habilidad);

            // El test emula la carrera: el servicio no hizo pre-check y llega
            // un Codigo que ya pertenece a otra activa. La BD debe rechazar
            // con DbUpdateException por el índice único activo.
            habilidad!.Actualizar(otra.Codigo, "Nombre objetivo", null, null);
            await repo.UpdateAsync(habilidad, default);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("IX_Habilidades_ActiveCodigoUnique", ex.InnerException?.Message ?? ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            context.Set<HabilidadEntity>().RemoveRange(
                await context.Set<HabilidadEntity>()
                    .Where(h => h.Id == objetivo.Id || h.Id == otra.Id)
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task DeleteAsync_MarcaComoInactivoYEliminado()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
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

    // ===================== Coverage: unique index violation =====================

    [MySqlFact]
    public async Task AddAsync_DuplicateActiveCodigo_LanzaDbUpdateException()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new HabilidadRepository(context);
        var codigoCompartido = "UNIQ-DUP-" + Guid.NewGuid().ToString("N")[..8];

        var habilidad1 = new Habilidad(codigoCompartido, "Primera", "Test", "Desc 1");
        await repo.AddAsync(habilidad1, default);
        await context.SaveChangesAsync();

        try
        {
            var habilidad2 = new Habilidad(codigoCompartido, "Segunda", "Test", "Desc 2");
            await repo.AddAsync(habilidad2, default);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("unique", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            context.Set<HabilidadEntity>().RemoveRange(
                await context.Set<HabilidadEntity>().Where(h => h.Codigo == codigoCompartido).ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    // ===================== Coverage: soft-delete with references =====================

    [MySqlFact]
    public async Task DeleteAsync_HabilidadReferenciada_NoAlteraCargoHabilidad()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new HabilidadRepository(context);

        var cargoEntity = RepositoryTestData.CreateCargo("CRG-REF", NivelCargoConstantes.DirectivoId);
        var habilidadEntity = RepositoryTestData.CreateHabilidad("HAB-REF");

        await context.Set<CargoEntity>().AddAsync(cargoEntity);
        await context.Set<HabilidadEntity>().AddAsync(habilidadEntity);
        await context.SaveChangesAsync();

        var cargoHabilidad = new CargoHabilidadEntity
        {
            Id = Guid.NewGuid(),
            CargoId = cargoEntity.Id,
            HabilidadId = habilidadEntity.Id,
            NivelRequeridoId = DatosSemilla.NivelBasicoId,
            Ponderacion = 1.0m,
            EsObligatoria = true
        };

        await context.Set<CargoHabilidadEntity>().AddAsync(cargoHabilidad);
        await context.SaveChangesAsync();

        try
        {
            // Act: soft-delete the referenced Habilidad via the repository
            await repo.DeleteAsync(habilidadEntity.Id, default);
            await context.SaveChangesAsync();

            // Assert: CargoHabilidad row still exists with same HabilidadId
            var referencia = await context.Set<CargoHabilidadEntity>()
                .FirstOrDefaultAsync(ch => ch.Id == cargoHabilidad.Id);

            Assert.NotNull(referencia);
            Assert.Equal(habilidadEntity.Id, referencia!.HabilidadId);
            Assert.Equal(cargoEntity.Id, referencia.CargoId);
        }
        finally
        {
            context.Set<CargoHabilidadEntity>().Remove(cargoHabilidad);
            context.Set<CargoEntity>().Remove(cargoEntity);
            context.Set<HabilidadEntity>().Remove(habilidadEntity);
            await context.SaveChangesAsync();
        }
    }
}
