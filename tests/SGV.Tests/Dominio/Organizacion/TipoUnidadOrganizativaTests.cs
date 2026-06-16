using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Dominio.Organizacion;

public sealed class TipoUnidadOrganizativaTests
{
    [Fact]
    public void Crear_ConCodigoYNombreValidos_AsignaPropiedades()
    {
        var tipo = new TipoUnidadOrganizativa("Facultad", "Facultad");

        Assert.NotEqual(Guid.Empty, tipo.Id);
        Assert.Equal("Facultad", tipo.Codigo);
        Assert.Equal("Facultad", tipo.Nombre);
    }

    [Fact]
    public void Crear_ConCodigoVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoUnidadOrganizativa("", "Facultad"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoUnidadOrganizativa("Facultad", ""));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var codigoLargo = new string('A', 51);
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoUnidadOrganizativa(codigoLargo, "Facultad"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreMayorA100_ThrowsArgumentException()
    {
        var nombreLargo = new string('A', 101);
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoUnidadOrganizativa("Facultad", nombreLargo));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void TipoUnidadOrganizativa_NoExponeSetterPublico()
    {
        var tipo = new TipoUnidadOrganizativa("Facultad", "Facultad");

        // Verificar que las propiedades no tienen setter público
        var codigoProperty = typeof(TipoUnidadOrganizativa).GetProperty(nameof(TipoUnidadOrganizativa.Codigo));
        var nombreProperty = typeof(TipoUnidadOrganizativa).GetProperty(nameof(TipoUnidadOrganizativa.Nombre));

        Assert.NotNull(codigoProperty);
        Assert.NotNull(nombreProperty);
        Assert.Null(codigoProperty!.GetSetMethod());
        Assert.Null(nombreProperty!.GetSetMethod());
    }

    [Fact]
    public void Crear_ConCodigoNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoUnidadOrganizativa(null!, "Facultad"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoUnidadOrganizativa("Facultad", null!));
        Assert.Contains("Nombre", ex.Message);
    }
}
