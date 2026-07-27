using SGV.Contracts.Organizacion.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

/// <summary>
/// Unit tests for <see cref="PuestoSegmentoListado"/> and
/// <see cref="PuestoListQuery"/> value/record shapes used by the application
/// layer to drive the segmented puesto query (activas / eliminadas).
/// </summary>
public sealed class PuestoListQueryTests
{
    [Fact]
    public void PuestoSegmentoListado_TieneValoresEsperados()
    {
        Assert.Equal(0, (int)PuestoSegmentoListado.Activas);
        Assert.Equal(1, (int)PuestoSegmentoListado.Eliminadas);
    }

    [Fact]
    public void Default_SegmentoEsActivas()
    {
        var query = new PuestoListQuery(Page: 1, PageSize: 20, Search: "ger", Sort: "codigo_asc");

        Assert.Equal(PuestoSegmentoListado.Activas, query.Segmento);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal("ger", query.Search);
        Assert.Equal("codigo_asc", query.Sort);
    }

    [Fact]
    public void PuedeConstruirQueryParaEliminadas()
    {
        var query = new PuestoListQuery(
            Page: 2,
            PageSize: 50,
            Search: "director",
            Sort: "nombre_desc",
            Segmento: PuestoSegmentoListado.Eliminadas);

        Assert.Equal(PuestoSegmentoListado.Eliminadas, query.Segmento);
        Assert.Equal(2, query.Page);
        Assert.Equal(50, query.PageSize);
        Assert.Equal("director", query.Search);
        Assert.Equal("nombre_desc", query.Sort);
    }
}
