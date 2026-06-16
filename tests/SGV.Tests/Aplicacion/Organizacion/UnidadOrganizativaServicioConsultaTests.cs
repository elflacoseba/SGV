using System.Reflection;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class UnidadOrganizativaServicioConsultaTests
{
    private static readonly Guid UnidadId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static UnidadOrganizativa CrearUnidadActiva()
    {
        var unidad = new UnidadOrganizativa("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId)
        {
            Id = UnidadId
        };
        unidad.CambiarDatos("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId, "Máxima autoridad ejecutiva");

        // Simulate eager-loaded nav property (EF Core sets this via Include)
        var tipo = new TipoUnidadOrganizativa("Direccion", "Dirección")
        {
            Id = TipoUnidadOrganizativaConstantes.DireccionId
        };
        SetNavigation(unidad, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), tipo);

        return unidad;
    }

    [Fact]
    public async Task ListAsync_CuandoExistenUnidades_RetornaListaDeDto()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Single(resultado);
        var dto = resultado[0];
        Assert.Equal(unidad.Id, dto.Id);
        Assert.Equal(unidad.Codigo, dto.Codigo);
        Assert.Equal(unidad.Nombre, dto.Nombre);
        Assert.Equal(unidad.TipoUnidadOrganizativaId, dto.TipoUnidadOrganizativaId);
        Assert.Equal("Dirección", dto.TipoUnidadNombre);
        Assert.Equal(unidad.Descripcion, dto.Descripcion);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenUnidades_RetornaListaVacia()
    {
        var repo = new FakeUnidadOrganizativaRepository { Datos = [] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoUnidadExiste_RetornaDto()
    {
        var unidad = CrearUnidadActiva();
        var repo = new FakeUnidadOrganizativaRepository { Datos = [unidad] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(UnidadId, default);

        Assert.NotNull(resultado);
        Assert.Equal(unidad.Id, resultado!.Id);
        Assert.Equal(unidad.Codigo, resultado.Codigo);
        Assert.Equal("Dirección", resultado.TipoUnidadNombre);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoUnidadNoExiste_RetornaNull()
    {
        var repo = new FakeUnidadOrganizativaRepository { Datos = [] };
        var servicio = new UnidadOrganizativaServicioConsulta(repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }

    private static void SetNavigation<TEntity, TNav>(TEntity entity, string propertyName, TNav value)
        where TEntity : class
    {
        var field = typeof(TEntity).GetField($"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(entity, value);
    }
}

internal sealed class FakeUnidadOrganizativaRepository : IUnidadOrganizativaRepository
{
    public List<UnidadOrganizativa> Datos { get; set; } = [];

    public Task AddAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<bool> ExistsActiveCodeAsync(string codigo, Guid? excludingId = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<UnidadOrganizativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<UnidadOrganizativa?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(e => e.Id == id && e.IsActive && !e.IsDeleted));

    public Task<UnidadOrganizativa?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));

    public Task<bool> IsDescendantAsync(Guid candidateDescendantId, Guid ancestorId, CancellationToken cancellationToken = default)
    {
        var current = Datos.FirstOrDefault(d => d.Id == candidateDescendantId);
        while (current is not null && current.UnidadPadreId.HasValue)
        {
            if (current.UnidadPadreId == ancestorId)
            {
                return Task.FromResult(true);
            }

            current = Datos.FirstOrDefault(d => d.Id == current.UnidadPadreId.Value);
        }

        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<UnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<UnidadOrganizativa>>(Datos.ToList());
    }

    public Task UpdateAsync(UnidadOrganizativa unidad, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unidad = Datos.FirstOrDefault(d => d.Id == id);
        if (unidad is not null)
        {
            unidad.Activar();
        }

        return Task.CompletedTask;
    }
}
