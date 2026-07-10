using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

/// <summary>
/// Unit tests for <see cref="CargoSegmentoListado"/> and
/// <see cref="CargoListQuery"/> value/record shapes used by the application
/// layer to drive the segmented cargo query (activas / eliminadas).
/// </summary>
public sealed class CargoListQueryTests
{
    [Fact]
    public void CargoSegmentoListado_TieneValoresEsperados()
    {
        Assert.Equal(0, (int)CargoSegmentoListado.Activas);
        Assert.Equal(1, (int)CargoSegmentoListado.Eliminadas);
    }

    [Fact]
    public void Default_SegmentoEsActivas()
    {
        var query = new CargoListQuery(Page: 1, PageSize: 20, Search: "ana", Sort: "codigo_asc");

        Assert.Equal(CargoSegmentoListado.Activas, query.Segmento);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal("ana", query.Search);
        Assert.Equal("codigo_asc", query.Sort);
    }

    [Fact]
    public void PuedeConstruirQueryParaEliminadas()
    {
        var query = new CargoListQuery(
            Page: 2,
            PageSize: 50,
            Search: "director",
            Sort: "nombre_desc",
            Segmento: CargoSegmentoListado.Eliminadas);

        Assert.Equal(CargoSegmentoListado.Eliminadas, query.Segmento);
        Assert.Equal(2, query.Page);
        Assert.Equal(50, query.PageSize);
        Assert.Equal("director", query.Search);
        Assert.Equal("nombre_desc", query.Sort);
    }
}