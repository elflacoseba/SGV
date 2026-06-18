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
}
