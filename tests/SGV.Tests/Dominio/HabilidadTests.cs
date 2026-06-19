using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Dominio;

public sealed class HabilidadTests
{
    // ── Constructor ─────────────────────────────────────────────

    [Fact]
    public void Crear_ConValoresValidos_AsignaPropiedades()
    {
        var habilidad = new Habilidad("COM01", "Comunicación", "Blandas", "Capacidad de comunicar");

        Assert.NotEqual(Guid.Empty, habilidad.Id);
        Assert.Equal("COM01", habilidad.Codigo);
        Assert.Equal("Comunicación", habilidad.Nombre);
        Assert.Equal("Blandas", habilidad.Categoria);
        Assert.Equal("Capacidad de comunicar", habilidad.Descripcion);
        Assert.True(habilidad.IsActive);
        Assert.False(habilidad.IsDeleted);
    }

    [Fact]
    public void Crear_SinCategoriaYDescripcion_AsignaNulos()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        Assert.Null(habilidad.Categoria);
        Assert.Null(habilidad.Descripcion);
        Assert.True(habilidad.IsActive);
    }

    [Fact]
    public void Crear_ConCodigoVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad("", "Comunicación"));

        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad(null!, "Comunicación"));

        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var codigoLargo = new string('A', 51);

        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad(codigoLargo, "Comunicación"));

        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", ""));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", null!));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreMayorA200_ThrowsArgumentException()
    {
        var nombreLargo = new string('A', 201);

        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", nombreLargo));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConCategoriaMayorA100_ThrowsArgumentException()
    {
        var categoriaLarga = new string('A', 101);

        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", "Comunicación", categoriaLarga));

        Assert.Contains("Categoria", ex.Message);
    }

    [Fact]
    public void Crear_ConDescripcionMayorA1000_ThrowsArgumentException()
    {
        var descripcionLarga = new string('A', 1001);

        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", "Comunicación", null, descripcionLarga));

        Assert.Contains("Descripcion", ex.Message);
    }

    // ── Codigo inmutable tras creación ──────────────────────────

    [Fact]
    public void Codigo_EsInmutableTrasCreacion()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        var codigoProperty = typeof(Habilidad).GetProperty(nameof(Habilidad.Codigo));
        Assert.NotNull(codigoProperty);
        Assert.Null(codigoProperty!.GetSetMethod());
    }

    // ── Actualizar ──────────────────────────────────────────────

    [Fact]
    public void Actualizar_ModificaCamposEditables()
    {
        var habilidad = new Habilidad("COM01", "Comunicación", "Blandas", "Original");

        habilidad.Actualizar("Comunicación Efectiva", "Blandas/Avanzadas", "Nueva descripción");

        Assert.Equal("COM01", habilidad.Codigo); // Inmutable
        Assert.Equal("Comunicación Efectiva", habilidad.Nombre);
        Assert.Equal("Blandas/Avanzadas", habilidad.Categoria);
        Assert.Equal("Nueva descripción", habilidad.Descripcion);
    }

    [Fact]
    public void Actualizar_PermiteCategoriaNulaYLimpia()
    {
        var habilidad = new Habilidad("COM01", "Comunicación", "Blandas", "Original");

        habilidad.Actualizar("Comunicación", null, null);

        Assert.Null(habilidad.Categoria);
        Assert.Null(habilidad.Descripcion);
    }

    [Fact]
    public void Actualizar_CodigoNoCambia()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        habilidad.Actualizar("Comunicación Actualizada", null, null);

        Assert.Equal("COM01", habilidad.Codigo);
    }

    [Fact]
    public void Actualizar_ConNombreVacio_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar("", null, null));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Actualizar_ConNombreMayorA200_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        var nombreLargo = new string('A', 201);

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar(nombreLargo, null, null));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Actualizar_ConCategoriaMayorA100_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        var categoriaLarga = new string('A', 101);

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar("Comunicación", categoriaLarga, null));

        Assert.Contains("Categoria", ex.Message);
    }

    [Fact]
    public void Actualizar_ConDescripcionMayorA1000_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        var descripcionLarga = new string('A', 1001);

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar("Comunicación", null, descripcionLarga));

        Assert.Contains("Descripcion", ex.Message);
    }

    [Fact]
    public void Actualizar_NoExponeSettersPublicos()
    {
        // Defensa: además de la verificación de setter en Codigo, garantizamos
        // que el método Actualizar es la única vía para mutar Nombre/Categoria/Descripcion.
        var nombreSetter = typeof(Habilidad).GetProperty(nameof(Habilidad.Nombre))?.GetSetMethod();
        var categoriaSetter = typeof(Habilidad).GetProperty(nameof(Habilidad.Categoria))?.GetSetMethod();
        var descripcionSetter = typeof(Habilidad).GetProperty(nameof(Habilidad.Descripcion))?.GetSetMethod();

        Assert.Null(nombreSetter);
        Assert.Null(categoriaSetter);
        Assert.Null(descripcionSetter);
    }

    // ── Desactivar ──────────────────────────────────────────────

    [Fact]
    public void Desactivar_SeteaIsActiveFalse()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        habilidad.Desactivar();

        Assert.False(habilidad.IsActive);
    }

    [Fact]
    public void Desactivar_NoCambiaCodigo()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        habilidad.Desactivar();

        Assert.Equal("COM01", habilidad.Codigo);
    }

    [Fact]
    public void Desactivar_NoCambiaNombre()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        habilidad.Desactivar();

        Assert.Equal("Comunicación", habilidad.Nombre);
    }

    [Fact]
    public void Desactivar_HabilidadYaInactiva_MantieneIsActiveFalse()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        habilidad.Desactivar();

        habilidad.Desactivar();

        Assert.False(habilidad.IsActive);
    }

    // ── Activar ─────────────────────────────────────────────────

    [Fact]
    public void Activar_HabilidadInactiva_SeteaIsActiveTrue()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        habilidad.Desactivar();

        habilidad.Activar();

        Assert.True(habilidad.IsActive);
    }

    [Fact]
    public void Activar_NoCambiaCodigo()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        habilidad.Desactivar();

        habilidad.Activar();

        Assert.Equal("COM01", habilidad.Codigo);
    }

    [Fact]
    public void Activar_HabilidadYaActiva_MantieneIsActiveTrue()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        habilidad.Activar();

        Assert.True(habilidad.IsActive);
    }

    [Fact]
    public void Activar_NoCambiaNombre()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        habilidad.Desactivar();

        habilidad.Activar();

        Assert.Equal("Comunicación", habilidad.Nombre);
    }
}
