using SGV.Dominio.Habilidades;
using Xunit;

namespace SGV.Tests.Dominio;

/// <summary>
/// Tests de dominio para la entidad <see cref="Habilidad"/>.
///
/// <b>Issue migrar-campo-categoria-habilidades-a-tabla:</b> la firma del
/// constructor y <see cref="Habilidad.Actualizar"/> reemplazaron el parámetro
/// legacy <c>string? Categoria</c> por <c>Guid? categoriaId</c> (FK opcional
/// al catálogo <see cref="CategoriaHabilidad"/>). Las invariantes de shape
/// (Codigo/Nombre requeridos, longitudes máximas) se preservan; el resto de
/// las semánticas de la entidad permanecen idénticas.
/// </summary>
public sealed class HabilidadTests
{
    private static readonly Guid ConduccionId = Guid.Parse("72000000-0000-0000-0000-000000000000");

    // ── Constructor ─────────────────────────────────────────────

    [Fact]
    public void Crear_AsignaCodigoNombreCategoriaIdYDescripcion()
    {
        var habilidad = new Habilidad("COM01", "Comunicación", ConduccionId, "Capacidad de comunicar");

        Assert.Equal("COM01", habilidad.Codigo);
        Assert.Equal("Comunicación", habilidad.Nombre);
        Assert.Equal(ConduccionId, habilidad.CategoriaId);
        Assert.Equal("Capacidad de comunicar", habilidad.Descripcion);
    }

    [Fact]
    public void Crear_SinCategoriaIdYPredeterminadoCategoriaIdEsNull()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        Assert.Null(habilidad.CategoriaId);
    }

    [Fact]
    public void Crear_ConCodigoVacio_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new Habilidad("", "Comunicación"));
    }

    [Fact]
    public void Crear_ConCodigoNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new Habilidad(null!, "Comunicación"));
    }

    [Fact]
    public void Crear_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var codigoLargo = new string('A', HabilidadRules.CodigoMaxLength + 1);

        Assert.Throws<ArgumentException>(
            () => new Habilidad(codigoLargo, "Comunicación"));
    }

    [Fact]
    public void Crear_ConNombreVacio_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", ""));
    }

    [Fact]
    public void Crear_ConNombreNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", null!));
    }

    [Fact]
    public void Crear_ConNombreMayorA200_ThrowsArgumentException()
    {
        var nombreLargo = new string('A', 201);

        Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", nombreLargo));
    }

    [Fact]
    public void Crear_ConDescripcionMayorA1000_ThrowsArgumentException()
    {
        var descripcionLarga = new string('A', 1001);

        var ex = Assert.Throws<ArgumentException>(
            () => new Habilidad("COM01", "Comunicación", null, descripcionLarga));

        Assert.Contains("Descripcion", ex.Message);
    }

    // ── Actualizar ──────────────────────────────────────────────

    [Fact]
    public void Actualizar_ModificaCamposEditables()
    {
        var habilidad = new Habilidad("COM01", "Comunicación", ConduccionId, "Original");

        habilidad.Actualizar("COM02", "Comunicación Efectiva",
            Guid.Parse("72000000-0000-0000-0000-000000000001"), "Nueva descripción");

        Assert.Equal("COM02", habilidad.Codigo);
        Assert.Equal("Comunicación Efectiva", habilidad.Nombre);
        Assert.Equal(Guid.Parse("72000000-0000-0000-0000-000000000001"), habilidad.CategoriaId);
        Assert.Equal("Nueva descripción", habilidad.Descripcion);
    }

    [Fact]
    public void Actualizar_CambiaCodigoSiNoDuplica()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        habilidad.Actualizar("COM02", "Comunicación");

        Assert.Equal("COM02", habilidad.Codigo);
    }

    [Fact]
    public void Actualizar_PermiteCategoriaNulaYLimpia()
    {
        var habilidad = new Habilidad("COM01", "Comunicación", ConduccionId, "Original");

        habilidad.Actualizar("COM01", "Comunicación", null, null);

        Assert.Null(habilidad.CategoriaId);
        Assert.Null(habilidad.Descripcion);
    }

    [Fact]
    public void Actualizar_ConCodigoVacio_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar("", "Comunicación", null, null));

        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Actualizar_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        var codigoLargo = new string('A', HabilidadRules.CodigoMaxLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar(codigoLargo, "Comunicación", null, null));

        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Actualizar_ConNombreVacio_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar("COM01", "", null, null));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Actualizar_ConNombreMayorA200_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        var nombreLargo = new string('A', 201);

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar("COM01", nombreLargo, null, null));

        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Actualizar_ConDescripcionMayorA1000_ThrowsArgumentException()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        var descripcionLarga = new string('A', 1001);

        var ex = Assert.Throws<ArgumentException>(
            () => habilidad.Actualizar("COM01", "Comunicación", null, descripcionLarga));

        Assert.Contains("Descripcion", ex.Message);
    }

    [Fact]
    public void Actualizar_NoExponeSettersPublicos()
    {
        var nombreSetter = typeof(Habilidad).GetProperty(nameof(Habilidad.Nombre))?.GetSetMethod();
        var descripcionSetter = typeof(Habilidad).GetProperty(nameof(Habilidad.Descripcion))?.GetSetMethod();

        Assert.Null(nombreSetter);
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

    // ── Activar ─────────────────────────────────────────────────

    [Fact]
    public void Activar_SeteaIsActiveTrue()
    {
        var habilidad = new Habilidad("COM01", "Comunicación");
        habilidad.Desactivar();

        habilidad.Activar();

        Assert.True(habilidad.IsActive);
    }

    // ── Defensa contra reintroducción de string Categoria ────────

    [Fact]
    public void Habilidad_NoExponePropiedadCategoriaString()
    {
        var tipo = typeof(Habilidad);
        var tieneCategoriaString = tipo.GetProperty("Categoria")?.PropertyType == typeof(string);

        Assert.False(tieneCategoriaString,
            "Habilidad NO debe exponer una propiedad 'Categoria' de tipo string.");
    }
}