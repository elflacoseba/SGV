using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class CargoServicioConsultaTests
{
    private static readonly Guid NivelId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Cargo CargoActivo = new("DIRECTOR", "Director", NivelId, "Dirige equipos")
    {
        Id = Guid.Parse("20000000-0000-0000-0000-000000000001")
    };

    [Fact]
    public async Task ListAsync_CuandoExistenCargos_RetornaListaDeDto()
    {
        var repo = new FakeCargoRepository { Datos = [CargoActivo] };
        var servicio = new CargoServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(CargoActivo.Id, dto.Id);
        Assert.Equal(CargoActivo.Codigo, dto.Codigo);
        Assert.Equal(CargoActivo.Nombre, dto.Nombre);
        Assert.Equal(CargoActivo.NivelId, dto.NivelId);
        Assert.Equal(CargoActivo.Descripcion, dto.Descripcion);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenCargos_RetornaListaVacia()
    {
        var repo = new FakeCargoRepository { Datos = [] };
        var servicio = new CargoServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoCargoExiste_RetornaDto()
    {
        var repo = new FakeCargoRepository { Datos = [CargoActivo] };
        var servicio = new CargoServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(CargoActivo.Id, default);

        Assert.NotNull(resultado);
        Assert.Equal(CargoActivo.Id, resultado!.Id);
        Assert.Equal(CargoActivo.Codigo, resultado.Codigo);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoCargoNoExiste_RetornaNull()
    {
        var repo = new FakeCargoRepository { Datos = [] };
        var servicio = new CargoServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task QueryAsync_ConSegmentoActivas_RetornaSoloActivos()
    {
        var repo = new FakeCargoRepository { Datos = [CargoActivo] };
        var servicio = new CargoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new CargoListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Equal(1, resultado.Page);
        Assert.Equal(10, resultado.PageSize);
        Assert.Single(resultado.Items);
        Assert.Equal(CargoActivo.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados()
    {
        var repo = new FakeCargoRepository { Datos = [CargoActivo] };
        await repo.DeleteAsync(CargoActivo.Id, default);
        var servicio = new CargoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new CargoListQuery(Page: 1, PageSize: 10, Search: null, Sort: null,
                Segmento: CargoSegmentoListado.Eliminadas),
            default);

        Assert.Equal(1, resultado.TotalCount);
        Assert.Single(resultado.Items);
        Assert.Equal(CargoActivo.Id, resultado.Items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_SegmentosNoSeMezclan()
    {
        var activo = new Cargo("ACT-001", "Activo", NivelId)
        {
            Id = Guid.Parse("21000000-0000-0000-0000-000000000001")
        };
        var eliminado = new Cargo("DEL-001", "Eliminado", NivelId)
        {
            Id = Guid.Parse("21000000-0000-0000-0000-000000000002")
        };
        var repo = new FakeCargoRepository { Datos = [activo, eliminado] };
        await repo.DeleteAsync(eliminado.Id, default);
        var servicio = new CargoServicioConsulta(repo);

        var resultadoActivas = await servicio.QueryAsync(
            new CargoListQuery(1, 10, null, null, CargoSegmentoListado.Activas), default);
        var resultadoEliminadas = await servicio.QueryAsync(
            new CargoListQuery(1, 10, null, null, CargoSegmentoListado.Eliminadas), default);

        Assert.Equal(1, resultadoActivas.TotalCount);
        Assert.Equal(1, resultadoEliminadas.TotalCount);
        Assert.Equal(activo.Id, Assert.Single(resultadoActivas.Items).Id);
        Assert.Equal(eliminado.Id, Assert.Single(resultadoEliminadas.Items).Id);
        Assert.DoesNotContain(resultadoActivas.Items, c => c.Id == eliminado.Id);
        Assert.DoesNotContain(resultadoEliminadas.Items, c => c.Id == activo.Id);
    }

    [Fact]
    public async Task QueryAsync_TotalCountProvieneDelRepositorio()
    {
        var cargos = Enumerable.Range(0, 25)
            .Select(i => new Cargo($"CRG-{i:000}", $"Cargo {i}", NivelId)
            {
                Id = Guid.Parse($"22000000-0000-0000-0000-{i:D12}")
            })
            .ToArray();
        var repo = new FakeCargoRepository { Datos = cargos.ToList() };
        var servicio = new CargoServicioConsulta(repo);

        var resultado = await servicio.QueryAsync(
            new CargoListQuery(Page: 1, PageSize: 10, Search: null, Sort: null),
            default);

        Assert.Equal(25, resultado.TotalCount);
        Assert.Equal(10, resultado.Items.Count);
    }
}

internal sealed class FakeCargoRepository : ICargoRepository
{
    public List<Cargo> Datos { get; set; } = [];

    public Task<Cargo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<IReadOnlyList<Cargo>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Cargo>>(Datos.ToList());
    }

    public Task AddAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        Datos.Add(cargo);
        return Task.CompletedTask;
    }

    public Task<Cargo?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<Cargo?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task UpdateAsync(Cargo cargo, CancellationToken cancellationToken = default)
    {
        var index = Datos.FindIndex(e => e.Id == cargo.Id);
        if (index >= 0)
            Datos[index] = cargo;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = Datos.FirstOrDefault(e => e.Id == id);
        if (item is not null)
        {
            typeof(Cargo).GetProperty("IsDeleted")!.SetValue(item, true);
            typeof(Cargo).GetProperty("IsActive")!.SetValue(item, false);
        }
        return Task.CompletedTask;
    }

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = Datos.FirstOrDefault(e => e.Id == id);
        if (item is not null)
        {
            typeof(Cargo).GetProperty("IsDeleted")!.SetValue(item, false);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.Any(e => e.Codigo == codigo && e.Id != (excludingId ?? Guid.Empty)));
    }

    public Task<bool> HasActivePuestosAsync(Guid cargoId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<(IReadOnlyList<Cargo> Items, int TotalCount)> QueryAsync(
        string? search,
        int page,
        int pageSize,
        CargoSegmentoListado segmento = CargoSegmentoListado.Activas,
        CancellationToken cancellationToken = default)
    {
        // The fake mirrors the production predicate: activas = IsActive && !IsDeleted;
        // eliminadas = !IsActive && IsDeleted. To keep existing unit tests green,
        // items start with IsActive=true/IsDeleted=false until DeleteAsync flips them.
        var filtered = Datos.Where(c =>
        {
            var isDeleted = (bool)(typeof(Cargo).GetProperty("IsDeleted")!.GetValue(c) ?? false);
            var isActive = (bool)(typeof(Cargo).GetProperty("IsActive")!.GetValue(c) ?? true);

            return segmento == CargoSegmentoListado.Activas
                ? (isActive && !isDeleted)
                : (!isActive && isDeleted);
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLowerInvariant();
            filtered = filtered.Where(c =>
                c.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || c.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                || (c.Descripcion?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = filtered.OrderBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase).ToList();
        var totalCount = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Cargo>, int)>((items, totalCount));
    }
}
