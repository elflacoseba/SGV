using SGV.Aplicacion.Habilidades.Consultas;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

public sealed class HabilidadServicioConsultaTests
{
    private static readonly Habilidad HabilidadActiva = new("LIDERAZGO", "Liderazgo", "Conducción", "Capacidad de liderar equipos")
    {
        Id = Guid.Parse("50000000-0000-0000-0000-000000000001")
    };

    [Fact]
    public async Task ListAsync_CuandoExistenHabilidades_RetornaListaDeDto()
    {
        var repo = new FakeHabilidadRepository { Datos = [HabilidadActiva] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(HabilidadActiva.Id, dto.Id);
        Assert.Equal(HabilidadActiva.Codigo, dto.Codigo);
        Assert.Equal(HabilidadActiva.Nombre, dto.Nombre);
        Assert.Equal(HabilidadActiva.Categoria, dto.Categoria);
        Assert.Equal(HabilidadActiva.Descripcion, dto.Descripcion);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenHabilidades_RetornaListaVacia()
    {
        var repo = new FakeHabilidadRepository { Datos = [] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoHabilidadExiste_RetornaDto()
    {
        var repo = new FakeHabilidadRepository { Datos = [HabilidadActiva] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(HabilidadActiva.Id, default);

        Assert.NotNull(resultado);
        Assert.Equal(HabilidadActiva.Id, resultado!.Id);
        Assert.Equal(HabilidadActiva.Codigo, resultado.Codigo);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoHabilidadNoExiste_RetornaNull()
    {
        var repo = new FakeHabilidadRepository { Datos = [] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task QueryAsync_ConSegmentoActivas_RetornaSoloActivos()
    {
        var repo = new FakeHabilidadRepository { Datos = [HabilidadActiva] };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new HabilidadListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Equal(1, resultado.Page);
        Assert.Equal(10, resultado.PageSize);
        Assert.Single(resultado.Items);
        Assert.Equal(HabilidadActiva.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados()
    {
        var repo = new FakeHabilidadRepository { Datos = [HabilidadActiva] };
        await repo.DeleteAsync(HabilidadActiva.Id, default);
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new HabilidadListQuery(Page: 1, PageSize: 10, Search: null, Sort: null,
                Segmento: HabilidadSegmentoListado.Eliminadas),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Single(resultado.Items);
        Assert.Equal(HabilidadActiva.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_SegmentosNoSeMezclan()
    {
        var activa = new Habilidad("HAB-ACT", "Activa", "Cat", "Desc activa")
        {
            Id = Guid.Parse("51000000-0000-0000-0000-000000000001")
        };
        var eliminada = new Habilidad("HAB-ELIM", "Eliminada", "Cat", "Desc eliminada")
        {
            Id = Guid.Parse("51000000-0000-0000-0000-000000000002")
        };
        var repo = new FakeHabilidadRepository { Datos = [activa, eliminada] };
        await repo.DeleteAsync(eliminada.Id, default);
        var servicio = new HabilidadServicioConsulta(repo);

        var resultadoActivas = await servicio.QueryAsync(
            new HabilidadListQuery(1, 10, null, null, HabilidadSegmentoListado.Activas), default);
        var resultadoEliminadas = await servicio.QueryAsync(
            new HabilidadListQuery(1, 10, null, null, HabilidadSegmentoListado.Eliminadas), default);

        Assert.Equal(1, resultadoActivas.TotalCount);
        Assert.Equal(1, resultadoEliminadas.TotalCount);
        Assert.Equal(activa.Id, Assert.Single(resultadoActivas.Items).Id);
        Assert.Equal(eliminada.Id, Assert.Single(resultadoEliminadas.Items).Id);
        Assert.DoesNotContain(resultadoActivas.Items, h => h.Id == eliminada.Id);
        Assert.DoesNotContain(resultadoEliminadas.Items, h => h.Id == activa.Id);
    }

    [Fact]
    public async Task QueryAsync_TotalCountProvieneDelRepositorio()
    {
        var habilidades = Enumerable.Range(0, 25)
            .Select(i => new Habilidad($"HAB-{i:000}", $"Habilidad {i}", "Cat", $"Desc {i}")
            {
                Id = Guid.Parse($"52000000-0000-0000-0000-{i:D12}")
            })
            .ToArray();
        var repo = new FakeHabilidadRepository { Datos = habilidades.ToList() };
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new HabilidadListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(25, resultado.TotalCount);
        Assert.Equal(10, resultado.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar()
    {
        // Habilidades con códigos alfabéticamente crecientes pero nombres en
        // orden inverso. Si el sort server-side funciona, la página 1 debe
        // traer los nombres Z, Y, X, W; si solo ordena por Codigo en memoria
        // tras paginar, traería los nombres A, B, C, D que NO coincide con
        // sort=nombre_desc.
        var repo = new FakeHabilidadRepository();
        var h1 = new Habilidad("A-001", "Zeta", "Cat", null) { Id = Guid.NewGuid() };
        var h2 = new Habilidad("A-002", "Yankee", "Cat", null) { Id = Guid.NewGuid() };
        var h3 = new Habilidad("A-003", "Xray", "Cat", null) { Id = Guid.NewGuid() };
        var h4 = new Habilidad("A-004", "Whisky", "Cat", null) { Id = Guid.NewGuid() };
        repo.Datos.AddRange(new[] { h1, h2, h3, h4 });
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new HabilidadListQuery(1, 10, null, "nombre_desc"),
            default);

        Assert.Equal(new[] { "Zeta", "Yankee", "Xray", "Whisky" },
            resultado.Items.Select(i => i.Nombre).ToArray());
    }

    [Fact]
    public async Task QueryAsync_ConSortDesconocido_CaeACodigoAsc()
    {
        // Si sort no es uno de los valores reconocidos, el repositorio
        // debe caer al orden por defecto (Codigo asc) para mantener el
        // contrato de paginación consistente.
        var repo = new FakeHabilidadRepository();
        var h1 = new Habilidad("B-001", "Zeta", "Cat", null) { Id = Guid.NewGuid() };
        var h2 = new Habilidad("A-002", "Yankee", "Cat", null) { Id = Guid.NewGuid() };
        repo.Datos.AddRange(new[] { h1, h2 });
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new HabilidadListQuery(1, 10, null, "foo_bar"),
            default);

        Assert.Equal(new[] { "A-002", "B-001" },
            resultado.Items.Select(i => i.Codigo).ToArray());
    }

    [Fact]
    public async Task QueryAsync_PageSizeYPagePropagadosEnPagedResult()
    {
        // El servicio debe devolver los Page/PageSize del input, no del
        // repositorio (el repo solo entrega items + totalCount).
        var repo = new FakeHabilidadRepository();
        for (var i = 0; i < 5; i++)
        {
            repo.Datos.Add(new Habilidad($"HAB-{i:000}", $"Nombre {i}", "Cat", null)
            {
                Id = Guid.NewGuid()
            });
        }
        var servicio = new HabilidadServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new HabilidadListQuery(Page: 3, PageSize: 50, Search: null, Sort: null),
            default);

        Assert.Equal(3, resultado.Page);
        Assert.Equal(50, resultado.PageSize);
        Assert.Equal(5, resultado.TotalCount);
    }
}

internal sealed class FakeHabilidadRepository : IHabilidadRepository
{
    public List<Habilidad> Datos { get; set; } = [];

    public Task<Habilidad?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<IReadOnlyList<Habilidad>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Habilidad>>(Datos.ToList());
    }

    public Task AddAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Habilidad?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<Habilidad?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task UpdateAsync(Habilidad habilidad, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = Datos.FirstOrDefault(e => e.Id == id);
        if (item is not null)
        {
            typeof(Habilidad).GetProperty("IsDeleted")!.SetValue(item, true);
            typeof(Habilidad).GetProperty("IsActive")!.SetValue(item, false);
        }
        return Task.CompletedTask;
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Read-only fake does not support write operations.");

    public Task<(IReadOnlyList<Habilidad> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        string? sort = null,
        HabilidadSegmentoListado segmento = HabilidadSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        var filtered = Datos.Where(h =>
        {
            var isDeleted = (bool)(typeof(Habilidad).GetProperty("IsDeleted")!.GetValue(h) ?? false);
            var isActive = (bool)(typeof(Habilidad).GetProperty("IsActive")!.GetValue(h) ?? true);

            return segmento == HabilidadSegmentoListado.Activas
                ? (isActive && !isDeleted)
                : (!isActive && isDeleted);
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLowerInvariant();
            filtered = filtered.Where(h =>
                h.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || h.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || (h.Categoria?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false)
                || (h.Descripcion?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = ApplySort(filtered, sort).ToList();
        var totalCount = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Habilidad>, int)>((items, totalCount));
    }

    private static IOrderedEnumerable<Habilidad> ApplySort(IEnumerable<Habilidad> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => source.OrderByDescending(static h => h.Codigo, StringComparer.OrdinalIgnoreCase),
            "codigo_asc" => source.OrderBy(static h => h.Codigo, StringComparer.OrdinalIgnoreCase),
            "nombre_desc" => source.OrderByDescending(static h => h.Nombre, StringComparer.OrdinalIgnoreCase),
            "nombre_asc" => source.OrderBy(static h => h.Nombre, StringComparer.OrdinalIgnoreCase),
            "categoria_desc" => source.OrderByDescending(static h => h.Categoria ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            "categoria_asc" => source.OrderBy(static h => h.Categoria ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderBy(static h => h.Codigo, StringComparer.OrdinalIgnoreCase)
        };
}
