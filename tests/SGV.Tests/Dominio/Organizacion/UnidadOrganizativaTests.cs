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

    // ── Codigo inmutable tras creación ──────────────────────────

    [Fact]
    public void Codigo_EsInmutableTrasCreacion()
    {
        var unidad = new UnidadOrganizativa("COD-01", "Unidad Test", TipoUnidadValido);

        var codigoProperty = typeof(UnidadOrganizativa).GetProperty(nameof(UnidadOrganizativa.Codigo));
        Assert.NotNull(codigoProperty);

        // Para records con `init`, el setter existe a nivel IL pero está decorado
        // con System.Runtime.CompilerServices.IsExternalInit, lo que lo hace
        // inaccesible fuera del object initializer / `with`. Verificamos el modifier.
        var setter = codigoProperty!.GetSetMethod(nonPublic: false);
        Assert.NotNull(setter);
        var hasInitModifier = setter!.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
        Assert.True(hasInitModifier, "Codigo debe ser init-only, no public set.");
    }

    // ── Actualizar ──────────────────────────────────────────────

    [Fact]
    public void Actualizar_ModificaCamposEditables_PeroNoCodigo()
    {
        var unidad = new UnidadOrganizativa("COD-03", "Unidad Original", TipoUnidadValido);
        var nuevoTipoId = Guid.Parse("60000000-0000-0000-0000-000000000002");

        var actualizada = unidad.Actualizar("Unidad Modificada", "Descripción", nuevoTipoId, null, null, null);

        Assert.Equal(nuevoTipoId, actualizada.TipoUnidadOrganizativaId);
        Assert.Equal("Unidad Modificada", actualizada.Nombre);
        Assert.Equal("Descripción", actualizada.Descripcion);
        // Codigo preservado por el invariante: Actualizar no acepta codigo como parámetro.
        Assert.Equal("COD-03", actualizada.Codigo);
    }

    [Fact]
    public void Actualizar_CodigoNoCambia()
    {
        var unidad = new UnidadOrganizativa("RECT", "Rectorado", TipoUnidadValido);

        var actualizada = unidad.Actualizar("Rectorado Actualizado", "Nueva descripción", TipoUnidadValido, null, null, null);

        Assert.Equal("RECT", actualizada.Codigo);
    }

    [Fact]
    public void Actualizar_ConTipoUnidadOrganizativaIdVacio_ThrowsArgumentException()
    {
        var unidad = new UnidadOrganizativa("COD-04", "Unidad", TipoUnidadValido);

        var ex = Assert.Throws<ArgumentException>(
            () => unidad.Actualizar("Unidad", "Desc", Guid.Empty, null, null, null));
        Assert.Contains("TipoUnidadOrganizativaId", ex.Message);
    }

    [Fact]
    public void Actualizar_ConNombreVacio_ThrowsArgumentException()
    {
        var unidad = new UnidadOrganizativa("COD-04", "Unidad", TipoUnidadValido);

        Assert.Throws<ArgumentException>(
            () => unidad.Actualizar("", null, TipoUnidadValido, null, null, null));
    }

    [Fact]
    public void Actualizar_ConVigenciaInvalida_ThrowsInvalidOperationException()
    {
        var unidad = new UnidadOrganizativa("COD-04", "Unidad", TipoUnidadValido);

        Assert.Throws<InvalidOperationException>(
            () => unidad.Actualizar("Unidad", null, TipoUnidadValido, null,
                new DateOnly(2025, 6, 1), new DateOnly(2025, 5, 1)));
    }

    // ── Crear ───────────────────────────────────────────────────

    [Fact]
    public void Crear_ConTipoUnidadOrganizativaIdNoVacio_NoTienePropiedadTipoUnidad()
    {
        var unidad = new UnidadOrganizativa("COD-05", "Unidad", TipoUnidadValido);

        var tipoUnidadProp = typeof(UnidadOrganizativa).GetProperty("TipoUnidad");
        Assert.Null(tipoUnidadProp);
    }
}
