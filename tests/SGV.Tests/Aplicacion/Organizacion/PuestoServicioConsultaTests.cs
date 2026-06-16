using System.Reflection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class PuestoServicioConsultaTests
{
    private static readonly Guid UnidadId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CargoId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid PuestoId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    private static Puesto CrearPuestoConNavigations()
    {
        var unidad = new UnidadOrganizativa("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId)
        {
            Id = UnidadId
        };
        var cargo = new Cargo("DIRECTOR", "Director", "Conducción media")
        {
            Id = CargoId
        };
        var puesto = new Puesto(UnidadId, CargoId, "GER-001", "Gerente General")
        {
            Id = PuestoId
        };
        puesto.CambiarDatos("GER-001", "Gerente General", "Responsable de la gerencia");

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
}

internal sealed class FakePuestoRepository : IPuestoRepository
{
    public List<Puesto> Datos { get; set; } = [];

    public Task<Puesto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<IReadOnlyList<Puesto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Puesto>>(Datos.ToList());
    }
}
