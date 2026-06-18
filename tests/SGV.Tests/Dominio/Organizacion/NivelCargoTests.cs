using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Dominio.Organizacion;

public sealed class NivelCargoTests
{
    [Fact]
    public void Crear_ConValoresValidos_AsignaPropiedades()
    {
        var nivel = new NivelCargo("Directivo", "Directivo", 1, 1);

        Assert.NotEqual(Guid.Empty, nivel.Id);
        Assert.Equal("Directivo", nivel.Codigo);
        Assert.Equal("Directivo", nivel.Nombre);
        Assert.Equal(1, nivel.ValorNumerico);
        Assert.Equal(1, nivel.Orden);
    }

    [Fact]
    public void Crear_ConCodigoVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new NivelCargo("", "Directivo", 1, 1));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new NivelCargo(null!, "Directivo", 1, 1));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var codigoLargo = new string('A', 51);
        var ex = Assert.Throws<ArgumentException>(
            () => new NivelCargo(codigoLargo, "Directivo", 1, 1));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new NivelCargo("Directivo", "", 1, 1));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new NivelCargo("Directivo", null!, 1, 1));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreMayorA100_ThrowsArgumentException()
    {
        var nombreLargo = new string('A', 101);
        var ex = Assert.Throws<ArgumentException>(
            () => new NivelCargo("Directivo", nombreLargo, 1, 1));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConValorNumericoCero_AsignaValor()
    {
        var nivel = new NivelCargo("Junior", "Junior", 0, 2);

        Assert.Equal(0, nivel.ValorNumerico);
    }

    [Fact]
    public void Crear_ConValorNumericoMayorA255_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new NivelCargo("Test", "Test", 256, 1));
        Assert.Contains("ValorNumerico", ex.Message);
    }

    [Fact]
    public void Crear_ConValorNumericoNegativo_ThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new NivelCargo("Test", "Test", -1, 1));
        Assert.Contains("ValorNumerico", ex.Message);
    }

    [Fact]
    public void NoExponeSetterPublico()
    {
        var nivel = new NivelCargo("Directivo", "Directivo", 1, 1);

        var codigoProperty = typeof(NivelCargo).GetProperty(nameof(NivelCargo.Codigo));
        var nombreProperty = typeof(NivelCargo).GetProperty(nameof(NivelCargo.Nombre));
        var valorNumericoProperty = typeof(NivelCargo).GetProperty(nameof(NivelCargo.ValorNumerico));
        var ordenProperty = typeof(NivelCargo).GetProperty(nameof(NivelCargo.Orden));

        Assert.NotNull(codigoProperty);
        Assert.NotNull(nombreProperty);
        Assert.NotNull(valorNumericoProperty);
        Assert.NotNull(ordenProperty);
        Assert.Null(codigoProperty!.GetSetMethod());
        Assert.Null(nombreProperty!.GetSetMethod());
        Assert.Null(valorNumericoProperty!.GetSetMethod());
        Assert.Null(ordenProperty!.GetSetMethod());
    }
}
