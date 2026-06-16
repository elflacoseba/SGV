using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class TipoUnidadOrganizativaServicioConsultaTests
{
    private static readonly FakeTipoUnidadOrganizativaRepository Repo = new()
    {
        Datos =
        [
            new("Institucion", "Institución") { Id = TipoUnidadOrganizativaConstantes.InstitucionId },
            new("Facultad", "Facultad") { Id = TipoUnidadOrganizativaConstantes.FacultadId },
            new("Secretaria", "Secretaría") { Id = TipoUnidadOrganizativaConstantes.SecretariaId },
            new("Direccion", "Dirección") { Id = TipoUnidadOrganizativaConstantes.DireccionId },
            new("Departamento", "Departamento") { Id = TipoUnidadOrganizativaConstantes.DepartamentoId },
            new("Division", "División") { Id = TipoUnidadOrganizativaConstantes.DivisionId },
            new("Area", "Área") { Id = TipoUnidadOrganizativaConstantes.AreaId },
        ]
    };

    [Fact]
    public async Task ListAsync_CuandoExistenRegistros_RetornaListaCompleta()
    {
        var servicio = new TipoUnidadOrganizativaServicioConsulta(Repo);

        var resultado = await servicio.ListAsync(default);

        Assert.Equal(7, resultado.Count);
    }

    [Fact]
    public async Task ListAsync_CuandoNoExistenRegistros_RetornaListaVacia()
    {
        var repoVacio = new FakeTipoUnidadOrganizativaRepository { Datos = [] };
        var servicio = new TipoUnidadOrganizativaServicioConsulta(repoVacio);

        var resultado = await servicio.ListAsync(default);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoRegistroExiste_RetornaDto()
    {
        var servicio = new TipoUnidadOrganizativaServicioConsulta(Repo);

        var resultado = await servicio.GetByIdAsync(TipoUnidadOrganizativaConstantes.DireccionId, default);

        Assert.NotNull(resultado);
        Assert.Equal(TipoUnidadOrganizativaConstantes.DireccionId, resultado!.Id);
        Assert.Equal("Direccion", resultado.Codigo);
        Assert.Equal("Dirección", resultado.Nombre);
    }

    [Fact]
    public async Task GetByIdAsync_CuandoRegistroNoExiste_RetornaNull()
    {
        var servicio = new TipoUnidadOrganizativaServicioConsulta(Repo);

        var resultado = await servicio.GetByIdAsync(Guid.NewGuid(), default);

        Assert.Null(resultado);
    }
}

internal sealed class FakeTipoUnidadOrganizativaRepository : ITipoUnidadOrganizativaRepository
{
    public List<TipoUnidadOrganizativa> Datos { get; set; } = [];

    public Task<TipoUnidadOrganizativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Datos.FirstOrDefault(e => e.Id == id));
    }

    public Task<IReadOnlyList<TipoUnidadOrganizativa>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TipoUnidadOrganizativa>>(Datos.ToList());
    }
}
