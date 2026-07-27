using Microsoft.EntityFrameworkCore;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests <c>[MySqlFact]</c> para <see cref="PuestoRepository.QueryAsync"/>.
/// Espejo de <c>CargoRepositoryTests.QueryAsync_MySql_*</c>; cubre
/// sectores activas / eliminadas no se mezclan, búsqueda LIKE, sort
/// aplicado antes de paginar, paginación correcta y página fuera de
/// rango. Se skipean limpio sin MySQL disponible (configuración
/// estándar del repo).
/// </summary>
public sealed class PuestoRepositoryQueryAsyncTests
{
    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminados()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSDEL-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSDEL-CARGO");
        var searchToken = $"SD{Guid.NewGuid():N}"[..10];
        var puestoEntity = RepositoryTestData.CreatePuesto($"ACT-{searchToken}", unidad, cargo);
        var activo = RepositoryTestData.CreatePuesto($"ACT-{searchToken}-RE", unidad, cargo);
        var eliminado = RepositoryTestData.CreatePuesto($"DEL-{searchToken}", unidad, cargo, isDeleted: true);
        eliminado.IsActive = false;
        eliminado.IsDeleted = true;
        eliminado.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([puestoEntity, activo, eliminado]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: PuestoSegmentoListado.Eliminadas,
                default);

            Assert.Equal(1, totalCount);
            var eliminadaEncontrada = Assert.Single(items, i => i.Id == eliminado.Id);
            Assert.DoesNotContain(items, i => i.Id == puestoEntity.Id);
            Assert.DoesNotContain(items, i => i.Id == activo.Id);
            Assert.All(items, i =>
            {
                Assert.False(i.IsActive);
                Assert.True(i.IsDeleted);
            });
            Assert.Equal(eliminado.Id, eliminadaEncontrada.Id);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(puestoEntity, activo, eliminado);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SegmentoActivas_NoIncluyeEliminadas()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSACT-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSACT-CARGO");
        var searchToken = $"SA{Guid.NewGuid():N}"[..10];
        var activo = RepositoryTestData.CreatePuesto($"ACT-{searchToken}", unidad, cargo);
        var eliminado = RepositoryTestData.CreatePuesto($"DEL-{searchToken}", unidad, cargo, isDeleted: true);
        eliminado.IsActive = false;
        eliminado.IsDeleted = true;
        eliminado.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([activo, eliminado]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: PuestoSegmentoListado.Activas,
                default);

            Assert.Equal(1, totalCount);
            var activoEncontrado = Assert.Single(items, i => i.Id == activo.Id);
            Assert.DoesNotContain(items, i => i.Id == eliminado.Id);
            Assert.All(items, i =>
            {
                Assert.True(i.IsActive);
                Assert.False(i.IsDeleted);
            });
            Assert.Equal(activo.Id, activoEncontrado.Id);
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
    public async Task QueryAsync_MySql_SegmentosNoSeMezclan()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSMIX-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSMIX-CARGO");
        var searchToken = $"SM{Guid.NewGuid():N}"[..10];
        var activo = RepositoryTestData.CreatePuesto($"ACT-{searchToken}", unidad, cargo);
        var eliminado = RepositoryTestData.CreatePuesto($"DEL-{searchToken}", unidad, cargo, isDeleted: true);
        eliminado.IsActive = false;
        eliminado.IsDeleted = true;
        eliminado.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([activo, eliminado]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var (activas, totalActivas) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: PuestoSegmentoListado.Activas, default);
            var (eliminadas, totalEliminadas) = await repo.QueryAsync(
                searchToken, page: 1, pageSize: 20,
                sort: null,
                segmento: PuestoSegmentoListado.Eliminadas, default);

            Assert.Equal(1, totalActivas);
            Assert.Equal(1, totalEliminadas);
            var activaEncontrada = Assert.Single(activas, i => i.Id == activo.Id);
            var eliminadaEncontrada = Assert.Single(eliminadas, i => i.Id == eliminado.Id);
            Assert.DoesNotContain(activas, i => i.Id == eliminado.Id);
            Assert.DoesNotContain(eliminadas, i => i.Id == activo.Id);
            Assert.Equal(activo.Id, activaEncontrada.Id);
            Assert.Equal(eliminado.Id, eliminadaEncontrada.Id);
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
    public async Task QueryAsync_MySql_SearchFiltraPorCodigo_Nombre_Descripcion()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSSEARCH-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSSEARCH-CARGO");
        var codigoMatch = $"PT-SRCH-{Guid.NewGuid():N}"[..10];

        var p1 = RepositoryTestData.CreatePuesto(codigoMatch, unidad, cargo);
        var p2 = RepositoryTestData.CreatePuesto($"PT-OTRO-{Guid.NewGuid():N}"[..10], unidad, cargo);
        p2.Nombre = $"ZZZ{codigoMatch}ZZZ";
        var p3 = RepositoryTestData.CreatePuesto($"PT-OTRO-{Guid.NewGuid():N}"[..10], unidad, cargo);
        p3.Descripcion = $"texto-{codigoMatch}-final";

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([p1, p2, p3]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                codigoMatch, page: 1, pageSize: 20,
                sort: null,
                segmento: PuestoSegmentoListado.Activas,
                default);

            Assert.Equal(3, totalCount);
            Assert.Equal(3, items.Count);
            Assert.Contains(items, i => i.Id == p1.Id);
            Assert.Contains(items, i => i.Id == p2.Id);
            Assert.Contains(items, i => i.Id == p3.Id);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(p1, p2, p3);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_Paginacion_TotalCountProvieneDelRepositorio()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSPG-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSPG-CARGO");
        var sufijo = $"PT-PG-{Guid.NewGuid():N}"[..10];
        var puestos = Enumerable.Range(0, 5)
            .Select(i => RepositoryTestData.CreatePuesto($"{sufijo}-{i}", unidad, cargo))
            .ToArray();

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync(puestos);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var (page1, totalCount) = await repo.QueryAsync(
                sufijo, page: 1, pageSize: 2,
                sort: null,
                segmento: PuestoSegmentoListado.Activas, default);

            Assert.Equal(5, totalCount);
            Assert.Equal(2, page1.Count);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(puestos);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_PaginaFueraDeRango_RetornaColeccionVaciaSinMezclarSegmentos()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSOOR-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSOOR-CARGO");
        var sufijo = $"PT-OOR-{Guid.NewGuid():N}"[..10];
        var activo = RepositoryTestData.CreatePuesto($"ACT-{sufijo}", unidad, cargo);
        var eliminado = RepositoryTestData.CreatePuesto($"DEL-{sufijo}", unidad, cargo, isDeleted: true);
        eliminado.IsActive = false;
        eliminado.IsDeleted = true;
        eliminado.DeletedAt = DateTime.UtcNow;

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync([activo, eliminado]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            // Página 99 sobre 1 row → colección vacía, TotalCount = 1, no
            // se cuelan eliminadas en el segmento Activas.
            var (items, totalCount) = await repo.QueryAsync(
                sufijo, page: 99, pageSize: 20,
                sort: null,
                segmento: PuestoSegmentoListado.Activas, default);

            Assert.Empty(items);
            Assert.Equal(1, totalCount);
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
    public async Task QueryAsync_MySql_SortCodigoAsc_AplicaOrdenAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSRTC-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSRTC-CARGO");
        var sufijo = $"PT-RTC-{Guid.NewGuid():N}"[..8];
        var puestos = new[]
        {
            RepositoryTestData.CreatePuesto($"{sufijo}-C", unidad, cargo),
            RepositoryTestData.CreatePuesto($"{sufijo}-A", unidad, cargo),
            RepositoryTestData.CreatePuesto($"{sufijo}-B", unidad, cargo),
        };

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync(puestos);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                sufijo, page: 1, pageSize: 10,
                sort: "codigo_asc",
                segmento: PuestoSegmentoListado.Activas, default);

            Assert.Equal(3, totalCount);
            var codigos = items.Select(p => p.Codigo).ToArray();
            // El sort por Codigo ascendente debe producir orden
            // determinístico independiente del GUID.
            Assert.Equal(codigos.OrderBy(c => c, StringComparer.Ordinal), codigos);
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(puestos);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SortNombreDesc_AplicaOrdenAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var unidad = RepositoryTestData.CreateUnidadOrganizativa("PT-QSRTN-UO");
        var cargo = RepositoryTestData.CreateCargo("PT-QSRTN-CARGO");
        var sufijo = $"PT-RTN-{Guid.NewGuid():N}"[..8];

        // Códigos en orden ascendente pero nombres en orden descendente.
        var nombres = new[] { "Zulu", "Yankee", "Xray", "Whisky" };
        var codigos = nombres.Select((_, i) => $"{sufijo}-{i:D2}").ToArray();
        var entities = new List<PuestoEntity>();
        for (var i = 0; i < nombres.Length; i++)
        {
            var p = RepositoryTestData.CreatePuesto(codigos[i], unidad, cargo);
            p.Nombre = nombres[i];
            entities.Add(p);
        }

        await context.Set<UnidadOrganizativaEntity>().AddAsync(unidad);
        await context.Set<CargoEntity>().AddAsync(cargo);
        await context.Set<PuestoEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        try
        {
            var repo = new PuestoRepository(context);
            var (page1, totalCount) = await repo.QueryAsync(
                sufijo, page: 1, pageSize: 10,
                sort: "nombre_desc",
                segmento: PuestoSegmentoListado.Activas, default);

            Assert.Equal(4, totalCount);
            Assert.Equal(new[] { "Zulu", "Yankee", "Xray", "Whisky" },
                page1.Select(p => p.Nombre).ToArray());
        }
        finally
        {
            context.Set<PuestoEntity>().RemoveRange(entities);
            context.Set<CargoEntity>().Remove(cargo);
            context.Set<UnidadOrganizativaEntity>().Remove(unidad);
            await context.SaveChangesAsync();
        }
    }
}
