using SGV.Dominio.Personas;
using Xunit;

namespace SGV.Tests.Dominio.Personas;

/// <summary>
/// Unit tests for the read-only <see cref="TipoDocumento"/> catalog entity.
/// Covers REQ-TD-002 (creation shape), REQ-TD-006 (pattern validation) and
/// the length range invariant declared in the design.
/// </summary>
public sealed class TipoDocumentoTests
{
    // ── Constructor & invariants ────────────────────────────────

    [Fact]
    public void Constructor_ConCodigoVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoDocumento("", "DNI"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Constructor_ConCodigoSoloEspacios_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoDocumento("   ", "DNI"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Constructor_ConNombreVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoDocumento("DNI", ""));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Constructor_ConCodigoValido_AsignaTrim()
    {
        var td = new TipoDocumento("  DNI  ", "Documento Nacional de Identidad");
        Assert.Equal("DNI", td.Codigo);
    }

    [Fact]
    public void Constructor_ConPatronLongitudes_AsignaValores()
    {
        var td = new TipoDocumento(
            "DNI",
            "Documento Nacional de Identidad",
            patronValidacion: @"^\d{7,8}$",
            longitudMinima: 7,
            longitudMaxima: 8);

        Assert.Equal(@"^\d{7,8}$", td.PatronValidacion);
        Assert.Equal(7, td.LongitudMinima);
        Assert.Equal(8, td.LongitudMaxima);
    }

    [Fact]
    public void Constructor_ConPatronVacioONulo_NormalizaANulo()
    {
        var conEspacios = new TipoDocumento("X", "X", "   ");
        Assert.Null(conEspacios.PatronValidacion);

        var conNulo = new TipoDocumento("X", "X", null);
        Assert.Null(conNulo.PatronValidacion);
    }

    [Fact]
    public void Constructor_ConLongitudMinimaNegativa_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TipoDocumento("DNI", "DNI", null, longitudMinima: -1, longitudMaxima: 8));
    }

    [Fact]
    public void Constructor_ConLongitudMaximaNegativa_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TipoDocumento("DNI", "DNI", null, longitudMinima: 0, longitudMaxima: -1));
    }

    [Fact]
    public void Constructor_ConMinimoMayorQueMaximo_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new TipoDocumento("DNI", "DNI", null, longitudMinima: 9, longitudMaxima: 8));
        Assert.Contains("longitud mínima", ex.Message);
    }

    // ── REQ-TD-006: pattern validation per tipo ────────────────

    [Theory]
    [InlineData("1234567")]   // 7 digits
    [InlineData("12345678")]  // 8 digits
    public void ValidarNumeroDocumento_Dni_7u8Digitos_RetornaTrue(string numero)
    {
        var td = new TipoDocumento("DNI", "DNI", @"^\d{7,8}$", 7, 8);

        Assert.True(td.ValidarNumeroDocumento(numero));
    }

    [Theory]
    [InlineData("123456")]   // 6 digits (too short)
    [InlineData("123456789")] // 9 digits (too long)
    [InlineData("12A45678")] // non-digit
    [InlineData("ABC12345")]
    public void ValidarNumeroDocumento_Dni_FueraDeRangoOInvalido_RetornaFalse(string numero)
    {
        var td = new TipoDocumento("DNI", "DNI", @"^\d{7,8}$", 7, 8);

        Assert.False(td.ValidarNumeroDocumento(numero));
    }

    [Theory]
    [InlineData("ABC123456")]
    [InlineData("abc123456")]
    [InlineData("XYZ000000")]
    public void ValidarNumeroDocumento_Pasaporte_3Letras6Digitos_RetornaTrue(string numero)
    {
        var td = new TipoDocumento("Pasaporte", "Pasaporte", @"^[A-Za-z]{3}\d{6}$", 9, 9);

        Assert.True(td.ValidarNumeroDocumento(numero));
    }

    [Theory]
    [InlineData("AB1234567")]   // 2 letras + 7 dígitos
    [InlineData("ABCD12345")]   // 4 letras + 5 dígitos
    [InlineData("123ABC456")]   // dígitos primero
    [InlineData("ABC12345")]    // 3 letras + 5 dígitos
    public void ValidarNumeroDocumento_Pasaporte_FueraDeRangoOInvalido_RetornaFalse(string numero)
    {
        var td = new TipoDocumento("Pasaporte", "Pasaporte", @"^[A-Za-z]{3}\d{6}$", 9, 9);

        Assert.False(td.ValidarNumeroDocumento(numero));
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12345678")]
    public void ValidarNumeroDocumento_Le_6a8Digitos_RetornaTrue(string numero)
    {
        var td = new TipoDocumento("LE", "LE", @"^\d{6,8}$", 6, 8);

        Assert.True(td.ValidarNumeroDocumento(numero));
    }

    [Fact]
    public void ValidarNumeroDocumento_NuloOVacio_RetornaTrue()
    {
        var td = new TipoDocumento("DNI", "DNI", @"^\d{7,8}$", 7, 8);

        Assert.True(td.ValidarNumeroDocumento(null));
        Assert.True(td.ValidarNumeroDocumento(""));
        Assert.True(td.ValidarNumeroDocumento("   "));
    }

    [Fact]
    public void ValidarNumeroDocumento_SinPatron_ValidaSoloLongitud()
    {
        var td = new TipoDocumento("X", "X", patronValidacion: null, longitudMinima: 1, longitudMaxima: 5);

        Assert.True(td.ValidarNumeroDocumento("abc"));
        Assert.True(td.ValidarNumeroDocumento("a"));
        Assert.False(td.ValidarNumeroDocumento("abcdef"));
    }

    [Fact]
    public void ValidarNumeroDocumento_SinLongitudes_ValidaSoloPatron()
    {
        var td = new TipoDocumento("X", "X", patronValidacion: @"^\d+$");

        Assert.True(td.ValidarNumeroDocumento("12345"));
        Assert.True(td.ValidarNumeroDocumento("1"));
        Assert.False(td.ValidarNumeroDocumento("abc"));
    }
}
