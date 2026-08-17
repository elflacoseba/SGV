using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests <c>[MySqlFact]</c> para <see cref="UnidadOrganizativaRepository.QueryAsync"/>
/// centrados en el contrato de sort server-side introducido en issue #282.
/// Espejo de <c>PuestoRepositoryQueryAsyncTests</c>: cubre el caso feliz
/// (sort aplica antes del Skip/Take), el fallback (sort desconocido cae al
/// default Codigo ASC), la coherencia entre páginas y la normalización de
/// whitespace en el search. Se skipean limpio sin MySQL disponible
/// (configuración estándar del repo).
/// </summary>
public sealed class UnidadOrganizativaRepositoryQueryAsyncTests
{
    [MySqlFact]
    public async Task QueryAsync_MySql_SortNombreDesc_SeAplicaAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = $"UO-SRTN-{Guid.NewGuid():N}"[..8];

        // Códigos en orden ascendente (A..D) pero nombres deliberadamente en
        // orden descendente (Zulu..Whisky). Esto garantiza que orden por
        // Codigo asc ≠ orden por Nombre desc: si el sort no se aplica
        // antes del Skip/Take, página 1 con pageSize=2 devolvería una
        // pareja incoherente.
        var nombres = new[] { "Zulu", "Yankee", "Xray", "Whisky" };
        var codigos = nombres.Select((_, i) => $"{sufijo}-{i:D2}").ToArray();
        var entities = new List<UnidadOrganizativaEntity>();
        for (var i = 0; i < nombres.Length; i++)
        {
            var u = RepositoryTestData.CreateUnidadOrganizativa(codigos[i]);
            u.Nombre = nombres[i];
            entities.Add(u);
        }

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);

            // Página 1 con sort=nombre_desc.
            var (page1, total1) = await repo.QueryAsync(
                sufijo, null, null, null, page: 1, pageSize: 2,
                sort: "nombre_desc",
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);
            // Página 2 (última) con el mismo sort.
            var (page2, total2) = await repo.QueryAsync(
                sufijo, null, null, null, page: 2, pageSize: 2,
                sort: "nombre_desc",
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);

            Assert.Equal(4, total1);
            Assert.Equal(4, total2);

            // Página 1 (top 2 por Nombre desc): Zulu, Yankee
            Assert.Equal(new[] { "Zulu", "Yankee" }, page1.Select(u => u.Nombre).ToArray());
            // Página 2 (siguientes 2): Xray, Whisky
            Assert.Equal(new[] { "Xray", "Whisky" }, page2.Select(u => u.Nombre).ToArray());

            // Coherencia cross-page: el último de page1 (Yankee) debe ser
            // alfabéticamente mayor que el primero de page2 (Whisky) para
            // que la concatenación respete el orden.
            Assert.True(string.Compare(page1[^1].Nombre, page2[0].Nombre, StringComparison.OrdinalIgnoreCase) > 0,
                $"El último nombre de página 1 ('{page1[^1].Nombre}') debe ser " +
                $"mayor alfabéticamente que el primero de página 2 ('{page2[0].Nombre}').");
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(entities);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SortCodigoDesc_AplicaOrdenAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = $"UO-SRTC-{Guid.NewGuid():N}"[..8];

        // Inserción deliberadamente NO ordenada por Codigo para que el
        // sort aplicado ANTES del Skip/Take sea observable.
        var entities = new[]
        {
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-A"),
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-C"),
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-B"),
        };

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                sufijo, null, null, null, page: 1, pageSize: 10,
                sort: "codigo_desc",
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);

            Assert.Equal(3, totalCount);
            var codigos = items.Select(u => u.Codigo).ToArray();
            // Sort Codigo desc: C, B, A (ignora el orden de inserción).
            Assert.Equal(
                codigos.OrderByDescending(c => c, StringComparer.Ordinal).ToArray(),
                codigos);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(entities);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SortTipoAsc_OrdenaPorNombreDeTipoAntesDePaginar()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = $"UO-SRTT-{Guid.NewGuid():N}"[..8];

        // Dos unidades con tipos distintos. El repo ordena por
        // TipoUnidadOrganizativa!.Nombre (no por Codigo) cuando sort=tipo_asc.
        var tipoArea = new TipoUnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"TAR-{Guid.NewGuid():N}"[..8],
            Nombre = "Area-Test"
        };
        var tipoDireccion = new TipoUnidadOrganizativaEntity
        {
            Id = Guid.NewGuid(),
            Codigo = $"TDR-{Guid.NewGuid():N}"[..8],
            Nombre = "Direccion-Test"
        };

        var uDireccion = RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-DIR");
        uDireccion.TipoUnidadOrganizativaId = tipoDireccion.Id;
        var uArea = RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-ARE");
        uArea.TipoUnidadOrganizativaId = tipoArea.Id;

        await context.Set<TipoUnidadOrganizativaEntity>().AddRangeAsync([tipoArea, tipoDireccion]);
        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync([uDireccion, uArea]);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);
            var (items, totalCount) = await repo.QueryAsync(
                sufijo, null, null, null, page: 1, pageSize: 10,
                sort: "tipo_asc",
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);

            Assert.Equal(2, totalCount);
            var nombres = items.Select(u => u.TipoUnidadOrganizativa!.Nombre).ToArray();
            // tipo_asc: Area-Test (A) antes de Direccion-Test (D).
            Assert.Equal(new[] { "Area-Test", "Direccion-Test" }, nombres);
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(uDireccion, uArea);
            context.Set<TipoUnidadOrganizativaEntity>().RemoveRange(tipoArea, tipoDireccion);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SortNull_CaeACodigoAsc()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = $"UO-SRTN-{Guid.NewGuid():N}"[..8];

        var entities = new[]
        {
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-C"),
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-A"),
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-B"),
        };

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);

            // sort=null debe producir orden estable por Codigo ASC
            // (mismas consultas → mismo orden).
            var (page1, _) = await repo.QueryAsync(
                sufijo, null, null, null, page: 1, pageSize: 10,
                sort: null,
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);
            var (page2, _) = await repo.QueryAsync(
                sufijo, null, null, null, page: 1, pageSize: 10,
                sort: null,
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);

            Assert.Equal(page1.Select(u => u.Codigo).ToArray(),
                         page2.Select(u => u.Codigo).ToArray());
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(entities);
            await context.SaveChangesAsync();
        }
    }

    [MySqlFact]
    public async Task QueryAsync_MySql_SortDesconocido_CaeACodigoAsc()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var sufijo = $"UO-SRTU-{Guid.NewGuid():N}"[..8];

        var entities = new[]
        {
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-C"),
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-A"),
            RepositoryTestData.CreateUnidadOrganizativa($"{sufijo}-B"),
        };

        await context.Set<UnidadOrganizativaEntity>().AddRangeAsync(entities);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);

            var (conUnknown, _) = await repo.QueryAsync(
                sufijo, null, null, null, page: 1, pageSize: 10,
                sort: "sort_inexistente_en_la_whitelist",
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);
            var (conNull, _) = await repo.QueryAsync(
                sufijo, null, null, null, page: 1, pageSize: 10,
                sort: null,
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);

            // Mismo orden en ambos casos (default Codigo ASC).
            Assert.Equal(conNull.Select(u => u.Codigo).ToArray(),
                         conUnknown.Select(u => u.Codigo).ToArray());
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().RemoveRange(entities);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Issue #282: la capa de servicio trimea el search ANTES de invocar al
    /// repo. Aún así, el repo también trimea como defensa en profundidad;
    /// aquí verificamos que un search con whitespace al borde termina
    /// matcheando el término sin espacios (mismo resultado que sin trim).
    /// </summary>
    [MySqlFact]
    public async Task QueryAsync_MySql_SearchConTrimAlBorde_MatcheaIgualQueSinEspacios()
    {
        await using var context = new TestSgvDbContextFactory().CreateDbContext([]);
        var token = $"UO-TRM-{Guid.NewGuid():N}"[..10];

        var u = RepositoryTestData.CreateUnidadOrganizativa(token);
        u.Nombre = $"Unidad {token}";

        await context.Set<UnidadOrganizativaEntity>().AddAsync(u);
        await context.SaveChangesAsync();

        try
        {
            var repo = new UnidadOrganizativaRepository(context);

            var (sinTrim, totalSinTrim) = await repo.QueryAsync(
                token, null, null, null, page: 1, pageSize: 20,
                sort: null,
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);
            var (conTrim, totalConTrim) = await repo.QueryAsync(
                $"  {token}  ", null, null, null, page: 1, pageSize: 20,
                sort: null,
                segmento: UnidadOrganizativaSegmentoListado.Activas, default);

            Assert.Equal(totalSinTrim, totalConTrim);
            Assert.Equal(sinTrim.Select(x => x.Id).ToArray(),
                         conTrim.Select(x => x.Id).ToArray());
        }
        finally
        {
            context.Set<UnidadOrganizativaEntity>().Remove(u);
            await context.SaveChangesAsync();
        }
    }
}