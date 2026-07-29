using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
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

    // ===================== QueryAsync (segmented) tests =====================

    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminados()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SD{Guid.NewGuid():N}"[..10];
        var activa = RepositoryTestData.CreateCargo($"ACT-{searchToken}");
        var eliminada = RepositoryTestData.CreateCargo($"DEL-{searchToken}");
        eliminada.IsActive = false;
        eliminada.IsDeleted = true;
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<CargoEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: CargoSegmentoListado.Eliminadas,
                default);

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
            context.Set<CargoEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentoActivas_NoIncluyeEliminadas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SA{Guid.NewGuid():N}"[..10];
        var activa = RepositoryTestData.CreateCargo($"ACT-{searchToken}");
        var eliminada = RepositoryTestData.CreateCargo($"DEL-{searchToken}");
        eliminada.IsActive = false;
        eliminada.IsDeleted = true;
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<CargoEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: CargoSegmentoListado.Activas,
                default);

            Assert.Equal(1, totalCount);
            var activaEncontrada = Assert.Single(items, i => i.Id == activa.Id);
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
            context.Set<CargoEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentosNoSeMezclan()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var searchToken = $"SM{Guid.NewGuid():N}"[..10];
        var activa = RepositoryTestData.CreateCargo($"ACT-{searchToken}");
        var eliminada = RepositoryTestData.CreateCargo($"DEL-{searchToken}");
        eliminada.IsActive = false;
        eliminada.IsDeleted = true;
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<CargoEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var (activas, totalActivas) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: CargoSegmentoListado.Activas, default);
            var (eliminadas, totalEliminadas) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: CargoSegmentoListado.Eliminadas, default);

            Assert.Equal(1, totalActivas);
            Assert.Equal(1, totalEliminadas);
            var activaEncontrada = Assert.Single(activas, i => i.Id == activa.Id);
            var eliminadaEncontrada = Assert.Single(eliminadas, i => i.Id == eliminada.Id);
            Assert.DoesNotContain(activas, i => i.Id == eliminada.Id);
            Assert.DoesNotContain(eliminadas, i => i.Id == activa.Id);
            Assert.Equal(activa.Id, activaEncontrada.Id);
            Assert.Equal(eliminada.Id, eliminadaEncontrada.Id);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_ActivaYEliminada_MismoCodigo_RetornaAmbasEnDistintosSegmentos()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var codigoCompartido = $"CRG-Q-{sufijo}";

        // Entidad activa
        var activa = RepositoryTestData.CreateCargo(codigoCompartido);
        // Entidad eliminada con el mismo código (soft-delete libera el índice único)
        var eliminada = RepositoryTestData.CreateCargo(codigoCompartido);
        eliminada.IsActive = false;
        eliminada.IsDeleted = true;
        eliminada.DeletedAt = DateTime.UtcNow;

        await context.Set<CargoEntity>().AddRangeAsync([activa, eliminada]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var (activas, totalActivas) = await repo.QueryAsync(
                codigoCompartido, page: 1, pageSize: 20,
                sort: null,
                segmento: CargoSegmentoListado.Activas, default);
            var (eliminadas, totalEliminadas) = await repo.QueryAsync(
                codigoCompartido, page: 1, pageSize: 20,
                sort: null,
                segmento: CargoSegmentoListado.Eliminadas, default);

            Assert.Equal(1, totalActivas);
            Assert.Equal(1, totalEliminadas);
            Assert.Equal(activa.Id, Assert.Single(activas).Id);
            Assert.Equal(eliminada.Id, Assert.Single(eliminadas).Id);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(activa, eliminada);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_Paginacion_TotalCountProvieneDelRepositorio()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var cargos = Enumerable.Range(0, 5)
            .Select(i =>
            {
                var e = RepositoryTestData.CreateCargo($"CRG-PG-{sufijo}-{i}");
                return e;
            })
            .ToArray();

        await context.Set<CargoEntity>().AddRangeAsync(cargos);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            // Página de tamaño 2 sobre al menos 5 inserts únicos.
            var (page1, totalCount) = await repo.QueryAsync(
                $"CRG-PG-{sufijo}", page: 1, pageSize: 2,
                sort: null,
                segmento: CargoSegmentoListado.Activas, default);

            Assert.Equal(5, totalCount);
            Assert.Equal(2, page1.Count);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>()
                    .Where(c => c.Codigo.StartsWith($"CRG-PG-{sufijo}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// F-001 + F-006 (regression cross-page): con N cargos cuyos nombres rompen
    /// el orden alfabético y <c>pageSize</c> que fuerce múltiples páginas,
    /// <c>sort=nombre_desc</c> debe aplicarse ANTES del Skip/Take. Si el sort
    /// se aplicara solo en la página recibida, página 3 y página 1 podrían
    /// contener ítems arbitrarios y el orden entre páginas sería incoherente.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_MySql_SortNombreDesc_SeAplicaAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        // 12 cargos con códigos en orden natural A..L y nombres
        // deliberadamente mezclados (no correlativos con el código). Esto
        // garantiza que orden por Codigo asc ≠ orden por Nombre desc.
        var nombres = new[]
        {
            "Delta",  "Bravo",  "Charlie", "Echo",
            "Alpha",  "Zulu",   "Mike",    "Hotel",
            "Tango",  "Kilo",   "Juliet",  "Foxtrot"
        };
        var codigos = nombres.Select((_, i) => $"CRG-SRT-{sufijo}-{i:D2}").ToArray();

        var entities = new List<CargoEntity>();
        for (var i = 0; i < nombres.Length; i++)
        {
            var cargo = RepositoryTestData.CreateCargo(codigos[i], NivelIdValido, nombres[i]);
            entities.Add(cargo);
        }

        await context.Set<CargoEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);

            // Página 1 de 5 con sort=nombre_desc.
            var (page1, total1) = await repo.QueryAsync(
                $"CRG-SRT-{sufijo}", page: 1, pageSize: 5,
                sort: "nombre_desc",
                segmento: CargoSegmentoListado.Activas, default);
            // Página 3 (última) con el mismo sort.
            var (page3, total3) = await repo.QueryAsync(
                $"CRG-SRT-{sufijo}", page: 3, pageSize: 5,
                sort: "nombre_desc",
                segmento: CargoSegmentoListado.Activas, default);

            // Total = 12 (los 12 inserts de este test). page1 y page3 deberían
            // concatenarse como una sola secuencia descendente por Nombre.
            Assert.Equal(12, total1);
            Assert.Equal(12, total3);

            // Nombres de página 1 en orden descendente: Zulu, Tango, Mike, Kilo, Juliet
            Assert.Equal(new[] { "Zulu", "Tango", "Mike", "Kilo", "Juliet" },
                page1.Select(c => c.Nombre).ToArray());
            // Página 3 (los últimos 2): Bravo, Alpha
            Assert.Equal(new[] { "Bravo", "Alpha" },
                page3.Select(c => c.Nombre).ToArray());

            // Cross-page coherence: el último nombre de page1 (Juliet) debe
            // ser estrictamente mayor alfabéticamente que el primero de page3
            // (Charlie) para que la concatenación respete el orden.
            Assert.True(string.Compare(page1[^1].Nombre, page3[0].Nombre, StringComparison.OrdinalIgnoreCase) > 0,
                $"El último nombre de página 1 ('{page1[^1].Nombre}') debe ser " +
                $"mayor alfabéticamente que el primero de página 3 ('{page3[0].Nombre}').");
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>()
                    .Where(c => c.Codigo.StartsWith($"CRG-SRT-{sufijo}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Triangulación: con <c>sort=null</c>, el repositorio debe aplicar el
    /// orden por defecto (Codigo asc). Esto protege contra una refactorización
    /// que cambie el comportamiento por omisión.
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_MySql_SortNull_CaeACodigoAsc()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var entities = new[]
        {
            RepositoryTestData.CreateCargo($"CRG-NA-{sufijo}-C", NivelIdValido, "Nombre C"),
            RepositoryTestData.CreateCargo($"CRG-NA-{sufijo}-A", NivelIdValido, "Nombre A"),
            RepositoryTestData.CreateCargo($"CRG-NA-{sufijo}-B", NivelIdValido, "Nombre B"),
        };

        await context.Set<CargoEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        try
        {
            var repo = new CargoRepository(context);
            var (page, total) = await repo.QueryAsync(
                $"CRG-NA-{sufijo}", page: 1, pageSize: 10,
                sort: null,
                segmento: CargoSegmentoListado.Activas, default);

            // Total = 3 (mis 3 inserts únicos). El orden por Codigo asc
            // depende del GUID interno, así que verificamos que las páginas
            // produzcan el mismo orden si se consulta dos veces (estabilidad)
            // y que la concatenación de páginas 1+2 sea la secuencia completa.
            Assert.Equal(3, total);

            var (page1, _) = await repo.QueryAsync(
                $"CRG-NA-{sufijo}", page: 1, pageSize: 2,
                sort: null,
                segmento: CargoSegmentoListado.Activas, default);
            var (page2, _) = await repo.QueryAsync(
                $"CRG-NA-{sufijo}", page: 2, pageSize: 2,
                sort: null,
                segmento: CargoSegmentoListado.Activas, default);

            var concatenated = page1.Concat(page2).Select(c => c.Nombre).ToArray();
            Assert.Equal(3, concatenated.Length);
            Assert.Equal(concatenated.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
                concatenated);
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>()
                    .Where(c => c.Codigo.StartsWith($"CRG-NA-{sufijo}"))
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }

    // ===================== STORED soft-delete uniqueness =====================
    //
    // Regression para MariaDbStoredColumnsAndCollation. La columna computada
    // Cargos.ActiveCodigoUnique materializa NULL cuando IsDeleted=1, y NULL no
    // se considera en UNIQUE INDEX, por lo que pueden coexistir N registros
    // soft-deleted con el mismo Codigo. Si alguien revierte a UNIQUE INDEX
    // directo sobre Codigo (o a una columna VIRTUAL), este test rompe.

    [MySqlFact]
    public async Task AddAsync_MultiplesEliminadosConMismoCodigo_PermiteCreacionIlimitada()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var repo = new CargoRepository(context);
        var nivelId = NivelCargoConstantes.DirectivoId;
        var codigoCompartido = $"CRG-MULTIDEL-{Guid.NewGuid():N}".Substring(0, 22);

        var cargo1 = new Cargo(codigoCompartido, "Cargo 1", nivelId);
        var cargo2 = new Cargo(codigoCompartido, "Cargo 2", nivelId);
        var cargo3 = new Cargo(codigoCompartido, "Cargo 3", nivelId);

        try
        {
            await repo.AddAsync(cargo1, default);
            await context.SaveChangesAsync();

            await repo.DeleteAsync(cargo1.Id, default);
            await context.SaveChangesAsync();

            await repo.AddAsync(cargo2, default);
            await context.SaveChangesAsync();

            await repo.DeleteAsync(cargo2.Id, default);
            await context.SaveChangesAsync();

            await repo.AddAsync(cargo3, default);
            await context.SaveChangesAsync();

            var todos = await context.Set<CargoEntity>()
                .Where(c => c.Codigo == codigoCompartido)
                .ToListAsync();

            Assert.Equal(3, todos.Count);
            Assert.Single(todos, c => c.IsActive && !c.IsDeleted);
            Assert.Equal(2, todos.Count(c => c.IsDeleted));
        }
        finally
        {
            context.Set<CargoEntity>().RemoveRange(
                await context.Set<CargoEntity>()
                    .Where(c => c.Codigo == codigoCompartido)
                    .ToListAsync());
            await context.SaveChangesAsync();
        }
    }
}
