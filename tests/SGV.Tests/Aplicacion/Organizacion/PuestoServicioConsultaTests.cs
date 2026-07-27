using System.Reflection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class PuestoServicioConsultaTests
{
    private static readonly Guid UnidadId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CargoId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid PuestoId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    private static Puesto CrearPuestoConNavigations(string? codigo = null)
    {
        var unidad = new UnidadOrganizativa("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId)
        {
            Id = UnidadId
        };
        var cargo = new Cargo("DIRECTOR", "Director", Guid.Parse("70000000-0000-0000-0000-000000000001"), null)
        {
            Id = CargoId
        };

        Puesto puesto;
        if (codigo is null)
        {
            // Caso canónico usado por los tests existentes: nombre =
            // "Gerente General", descripción canónica.
            puesto = new Puesto(UnidadId, CargoId, "GER-001", "Gerente General")
            {
                Id = PuestoId
            };
            puesto.CambiarDatos("GER-001", "Gerente General", "Responsable de la gerencia");
        }
        else
        {
            // Variantes para los tests de QueryAsync que necesitan
            // múltiples puestos con códigos distintos.
            puesto = new Puesto(UnidadId, CargoId, codigo, codigo)
            {
                Id = Guid.NewGuid()
            };
            puesto.CambiarDatos(codigo, codigo, "Responsable de la gerencia");
        }

        // Set navigation properties via reflection (EF Core sets these normally)
        SetNavigation(puesto, "UnidadOrganizativa", unidad);
        SetNavigation(puesto, "Cargo", cargo);

        return puesto;
    }

    private static void SetNavigation<TEntity, TNav>(TEntity entity, string propertyName, TNav value)
        where TEntity : class
    {
        var field = typeof(TEntity).GetField($"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(entity, value);
    }

    [Fact]
    public async Task ListAsync_CuandoExistenPuestos_RetornaListaDeDtoConResumenRelaciones()
    {
        var puesto = CrearPuestoConNavigations();
        var repo = new FakePuestoRepository { Datos = [puesto] };
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(PuestoId, dto.Id);
        Assert.Equal("GER-001", dto.Codigo);
        Assert.Equal("Gerente General", dto.Nombre);
        Assert.Equal("Responsable de la gerencia", dto.Descripcion);
        Assert.Equal(UnidadId, dto.UnidadOrganizativaId);
        Assert.Equal("Gerencia General", dto.UnidadOrganizativaNombre);
        Assert.Equal(CargoId, dto.CargoId);
        Assert.Equal("Director", dto.CargoNombre);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenPuestos_RetornaListaVacia()
    {
        var repo = new FakePuestoRepository { Datos = [] };
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPuestoExiste_RetornaDtoConResumenRelaciones()
    {
        var puesto = CrearPuestoConNavigations();
        var repo = new FakePuestoRepository { Datos = [puesto] };
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(PuestoId, default);

        Assert.NotNull(resultado);
        Assert.Equal(PuestoId, resultado!.Id);
        Assert.Equal("GER-001", resultado.Codigo);
        Assert.Equal("Gerencia General", resultado.UnidadOrganizativaNombre);
        Assert.Equal("Director", resultado.CargoNombre);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPuestoNoExiste_RetornaNull()
    {
        var repo = new FakePuestoRepository { Datos = [] };
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    // ---- REQ-PTO-001 / REQ-PTO-002: QueryAsync (server-side paginated) ----

    [Fact]
    public async Task QueryAsync_ConSegmentoActivas_RetornaSoloActivos()
    {
        var repo = new FakePuestoRepository { Datos = [CrearPuestoConNavigations()] };
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new PuestoListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Equal(1, resultado.Page);
        Assert.Equal(10, resultado.PageSize);
        Assert.Single(resultado.Items);
        Assert.Equal(PuestoId, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados()
    {
        var repo = new FakePuestoRepository { Datos = [CrearPuestoConNavigations()] };
        await repo.DeleteAsync(PuestoId, default);
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new PuestoListQuery(Page: 1, PageSize: 10, Search: null, Sort: null,
                Segmento: PuestoSegmentoListado.Eliminadas),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Single(resultado.Items);
        Assert.Equal(PuestoId, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_SegmentosNoSeMezclan()
    {
        var activo = CrearPuestoConNavigations();
        var eliminado = CrearPuestoConNavigations(codigo: "DEL-001");
        var repo = new FakePuestoRepository { Datos = [activo, eliminado] };
        await repo.DeleteAsync(eliminado.Id, default);
        var servicio = new PuestoServicioConsulta(repo);

        var resultadoActivas = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, null, null, PuestoSegmentoListado.Activas), default);
        var resultadoEliminadas = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, null, null, PuestoSegmentoListado.Eliminadas), default);

        Assert.Equal(1, resultadoActivas.TotalCount);
        Assert.Equal(1, resultadoEliminadas.TotalCount);
        Assert.Equal(activo.Id, Assert.Single(resultadoActivas.Items).Id);
        Assert.Equal(eliminado.Id, Assert.Single(resultadoEliminadas.Items).Id);
        Assert.DoesNotContain(resultadoActivas.Items, p => p.Id == eliminado.Id);
        Assert.DoesNotContain(resultadoEliminadas.Items, p => p.Id == activo.Id);
    }

    [Fact]
    public async Task QueryAsync_TotalCountProvieneDelRepositorio()
    {
        var puestos = Enumerable.Range(0, 25)
            .Select(i => CrearPuestoConNavigations(codigo: $"GER-{i:000}"))
            .ToArray();
        var repo = new FakePuestoRepository { Datos = puestos.ToList() };
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new PuestoListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(25, resultado.TotalCount);
        Assert.Equal(10, resultado.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar()
    {
        var repo = new FakePuestoRepository();
        // Sort por Nombre con códigos alfabéticamente ascendentes y nombres
        // deliberadamente en orden inverso. Si el sort server-side
        // funciona, debe traer Zulu, Yankee, Xray, Whisky. Si sólo
        // ordenara por Codigo en memoria, traería Zulu, Yankee, Xray,
        // Whisky (que en este caso coincide por diseño — verificar el
        // orden descendente por Nombre es la aserción clave).
        var p1 = CrearPuestoConNavigations(codigo: "A-001");
        var p2 = CrearPuestoConNavigations(codigo: "A-002");
        var p3 = CrearPuestoConNavigations(codigo: "A-003");
        var p4 = CrearPuestoConNavigations(codigo: "A-004");
        // Renombrar vía Actualizar para forzar orden por Nombre distinto
        // al orden por Codigo (Nombre se setea por CambiarDatos).
        p1.Actualizar("Zulu");
        p2.Actualizar("Yankee");
        p3.Actualizar("Xray");
        p4.Actualizar("Whisky");
        repo.Datos.AddRange(new[] { p1, p2, p3, p4 });
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, null, "nombre_desc"),
            default);

        Assert.Equal(new[] { "Zulu", "Yankee", "Xray", "Whisky" },
            resultado.Items.Select(i => i.Nombre).ToArray());
    }

    [Fact]
    public async Task QueryAsync_ConSortCodigoAsc_NoDesordena()
    {
        var repo = new FakePuestoRepository();
        var p1 = CrearPuestoConNavigations(codigo: "C-001");
        var p2 = CrearPuestoConNavigations(codigo: "A-002");
        var p3 = CrearPuestoConNavigations(codigo: "B-003");
        repo.Datos.AddRange(new[] { p1, p2, p3 });
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, null, "codigo_asc"),
            default);

        Assert.Equal(new[] { "A-002", "B-003", "C-001" },
            resultado.Items.Select(i => i.Codigo).ToArray());
    }

    [Fact]
    public async Task QueryAsync_ConSortDesconocido_CaeACodigoAsc()
    {
        // Si sort no es uno de los valores reconocidos, el repositorio
        // debe caer al orden por defecto (Codigo asc) para mantener el
        // contrato de paginación consistente.
        var repo = new FakePuestoRepository();
        var p1 = CrearPuestoConNavigations(codigo: "B-001");
        var p2 = CrearPuestoConNavigations(codigo: "A-002");
        repo.Datos.AddRange(new[] { p1, p2 });
        var servicio = new PuestoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, null, "foo_bar"),
            default);

        Assert.Equal(new[] { "A-002", "B-001" },
            resultado.Items.Select(i => i.Codigo).ToArray());
    }

    [Fact]
    public async Task QueryAsync_ConSearchFiltraPorCodigo_Nombre_O_Descripcion()
    {
        var repo = new FakePuestoRepository();
        // Cada puesto tiene Codigo, Nombre y Descripcion DISTINTOS para
        // que el filtro LIKE se evalúe contra el campo que el test
        // pretende cubrir sin colisiones por substring accidental.
        var pGer = new Puesto(UnidadId, CargoId, "GER-001", "Gerente General", null, "Coordina la gerencia");
        var pDev = new Puesto(UnidadId, CargoId, "DEV-002", "Programador Senior", null, "Desarrolla features");
        var pAna = new Puesto(UnidadId, CargoId, "ANA-003", "Analista Funcional", null, "Levanta requisitos");
        SetNavigation(pGer, "UnidadOrganizativa", new UnidadOrganizativa("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId) { Id = UnidadId });
        SetNavigation(pGer, "Cargo", new Cargo("DIRECTOR", "Director", Guid.Parse("70000000-0000-0000-0000-000000000001"), null) { Id = CargoId });
        SetNavigation(pDev, "UnidadOrganizativa", new UnidadOrganizativa("DEV", "Desarrollo", TipoUnidadOrganizativaConstantes.DireccionId) { Id = UnidadId });
        SetNavigation(pDev, "Cargo", new Cargo("DEV", "Desarrollador", Guid.Parse("70000000-0000-0000-0000-000000000002"), null) { Id = CargoId });
        SetNavigation(pAna, "UnidadOrganizativa", new UnidadOrganizativa("ANA", "Analisis", TipoUnidadOrganizativaConstantes.DireccionId) { Id = UnidadId });
        SetNavigation(pAna, "Cargo", new Cargo("ANA", "Analista", Guid.Parse("70000000-0000-0000-0000-000000000003"), null) { Id = CargoId });
        repo.Datos.AddRange(new[] { pGer, pDev, pAna });
        var servicio = new PuestoServicioConsulta(repo);

        // Match por Codigo (substring "GER")
        var porCodigo = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, "GER", null), default);
        Assert.Equal(1, porCodigo.TotalCount);
        Assert.Equal("GER-001", porCodigo.Items[0].Codigo);

        // Match por Codigo exacto (DEV-002 sólo aparece en pDev)
        var porCodigoExacto = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, "DEV-002", null), default);
        Assert.Equal(1, porCodigoExacto.TotalCount);
        Assert.Equal("DEV-002", porCodigoExacto.Items[0].Codigo);

        // Match por Nombre (sub-string "Programador")
        var porNombre = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, "Programador", null), default);
        Assert.Equal(1, porNombre.TotalCount);
        Assert.Equal("DEV-002", porNombre.Items[0].Codigo);

        // Match por Descripcion (sub-string "requisitos")
        var porDesc = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, "requisitos", null), default);
        Assert.Equal(1, porDesc.TotalCount);
        Assert.Equal("ANA-003", porDesc.Items[0].Codigo);

        // Match vacío (sin search) → todos los activos
        var sinSearch = await servicio.QueryAsync(
            new PuestoListQuery(1, 10, null, null), default);
        Assert.Equal(3, sinSearch.TotalCount);
    }
}

internal sealed class FakePuestoRepository : IPuestoRepository
{
    public List<Puesto> Datos { get; set; } = [];

    public Task<Puesto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<Puesto>> ListAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Puesto>>(Datos.ToList());

    public Task AddAsync(Puesto puesto, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Puesto?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Puesto?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateAsync(Puesto puesto, CancellationToken ct = default) => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = Datos.FirstOrDefault(e => e.Id == id);
        if (item is not null)
        {
            // Espejo del repo real: el flag `IsDeleted` lo setea la capa
            // de persistencia; en el fake hay que reflejarlo con
            // reflection para que el filtro de segmento Eliminadas lo
            // vea correctamente.
            typeof(Puesto).GetProperty("IsDeleted")!.SetValue(item, true);
            typeof(Puesto).GetProperty("IsActive")!.SetValue(item, false);
        }
        return Task.CompletedTask;
    }

    public Task ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        var item = Datos.FirstOrDefault(e => e.Id == id);
        if (item is not null)
        {
            typeof(Puesto).GetProperty("IsDeleted")!.SetValue(item, false);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<(IReadOnlyList<Puesto> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        PuestoSegmentoListado segmento = PuestoSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        var filtered = Datos.Where(p =>
        {
            var isDeleted = p.IsDeleted;
            var isActive = p.IsActive;

            return segmento == PuestoSegmentoListado.Activas
                ? (isActive && !isDeleted)
                : (!isActive && isDeleted);
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || p.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || (p.Descripcion?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = ApplySort(filtered, sort).ToList();
        var totalCount = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Puesto>, int)>((items, totalCount));
    }

    private static IOrderedEnumerable<Puesto> ApplySort(IEnumerable<Puesto> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => source.OrderByDescending(static p => p.Codigo, StringComparer.OrdinalIgnoreCase),
            "codigo_asc" => source.OrderBy(static p => p.Codigo, StringComparer.OrdinalIgnoreCase),
            "nombre_desc" => source.OrderByDescending(static p => p.Nombre, StringComparer.OrdinalIgnoreCase),
            "nombre_asc" => source.OrderBy(static p => p.Nombre, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderBy(static p => p.Codigo, StringComparer.OrdinalIgnoreCase)
        };
}
