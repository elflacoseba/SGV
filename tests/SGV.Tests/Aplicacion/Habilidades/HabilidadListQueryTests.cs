using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Aplicacion.Habilidades;

/// <summary>
/// Unit tests for <see cref="HabilidadSegmentoListado"/> and
/// <see cref="HabilidadListQuery"/> value/record shapes used by the
/// application layer to drive the segmented habilidad query
/// (activas / eliminadas).
/// </summary>
public sealed class HabilidadListQueryTests
{
    [Fact]
    public void HabilidadSegmentoListado_TieneValoresEsperados()
    {
        Assert.Equal(0, (int)HabilidadSegmentoListado.Activas);
        Assert.Equal(1, (int)HabilidadSegmentoListado.Eliminadas);
    }

    [Fact]
    public void Default_SegmentoEsActivas()
    {
        var query = new HabilidadListQuery(Page: 1, PageSize: 20, Search: "prog", Sort: "codigo_asc");

        Assert.Equal(HabilidadSegmentoListado.Activas, query.Segmento);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal("prog", query.Search);
        Assert.Equal("codigo_asc", query.Sort);
    }

    [Fact]
    public void PuedeConstruirQueryParaEliminadas()
    {
        var query = new HabilidadListQuery(
            Page: 2,
            PageSize: 50,
            Search: "lider",
            Sort: "nombre_desc",
            Segmento: HabilidadSegmentoListado.Eliminadas);

        Assert.Equal(HabilidadSegmentoListado.Eliminadas, query.Segmento);
        Assert.Equal(2, query.Page);
        Assert.Equal(50, query.PageSize);
        Assert.Equal("lider", query.Search);
        Assert.Equal("nombre_desc", query.Sort);
    }
}