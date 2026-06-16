using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Dominio.Organizacion;

public sealed class UnidadOrganizativaTests
{
    private static readonly Guid TipoUnidadValido = Guid.Parse("60000000-0000-0000-0000-000000000001");

    [Fact]
    public void Crear_ConTipoUnidadOrganizativaIdNoVacio_AsignaPropiedad()
    {
        var unidad = new UnidadOrganizativa("COD-01", "Unidad Test", TipoUnidadValido);

        Assert.Equal(TipoUnidadValido, unidad.TipoUnidadOrganizativaId);
        Assert.Equal("COD-01", unidad.Codigo);
        Assert.Equal("Unidad Test", unidad.Nombre);
        Assert.True(unidad.IsActive);
    }

    [Fact]
    public void Crear_ConTipoUnidadOrganizativaIdVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new UnidadOrganizativa("COD-02", "Otra Unidad", Guid.Empty));
        Assert.Contains("TipoUnidadOrganizativaId", ex.Message);
    }

    [Fact]
    public void CambiarDatos_ConTipoUnidadOrganizativaIdNoVacio_AsignaPropiedad()
    {
        var unidad = new UnidadOrganizativa("COD-03", "Unidad Original", TipoUnidadValido);
        var nuevoTipoId = Guid.Parse("60000000-0000-0000-0000-000000000002");

        unidad.CambiarDatos("COD-03", "Unidad Modificada", nuevoTipoId, "Descripción");

        Assert.Equal(nuevoTipoId, unidad.TipoUnidadOrganizativaId);
        Assert.Equal("Unidad Modificada", unidad.Nombre);
        Assert.Equal("Descripción", unidad.Descripcion);
    }

    [Fact]
    public void CambiarDatos_ConTipoUnidadOrganizativaIdVacio_ThrowsArgumentException()
    {
        var unidad = new UnidadOrganizativa("COD-04", "Unidad", TipoUnidadValido);

        var ex = Assert.Throws<ArgumentException>(
            () => unidad.CambiarDatos("COD-04", "Unidad", Guid.Empty, "Desc"));
        Assert.Contains("TipoUnidadOrganizativaId", ex.Message);
    }

    [Fact]
    public void Crear_ConTipoUnidadOrganizativaIdNoVacio_NoTienePropiedadTipoUnidad()
    {
        var unidad = new UnidadOrganizativa("COD-05", "Unidad", TipoUnidadValido);

        var tipoUnidadProp = typeof(UnidadOrganizativa).GetProperty("TipoUnidad");
        Assert.Null(tipoUnidadProp);
    }
}
