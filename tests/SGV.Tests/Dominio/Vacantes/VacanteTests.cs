using SGV.Dominio.Vacantes;
using Xunit;

namespace SGV.Tests.Dominio.Vacantes;

public sealed class VacanteTests
{
    // ── ActualizarObservaciones ─────────────────────────────────

    [Fact]
    public void ActualizarObservaciones_SetValido_Asigna()
    {
        var vacante = CrearVacanteValida();

        vacante.ActualizarObservaciones("Se requiere experiencia en C#.");

        Assert.Equal("Se requiere experiencia en C#.", vacante.Observaciones);
    }

    [Fact]
    public void ActualizarObservaciones_TextoConEspacios_Trimea()
    {
        var vacante = CrearVacanteValida();

        vacante.ActualizarObservaciones("   Observations con espacios   ");

        Assert.Equal("Observations con espacios", vacante.Observaciones);
    }

    [Fact]
    public void ActualizarObservaciones_TextoMayorA500Caracteres_LanzaArgumentException()
    {
        var vacante = CrearVacanteValida();
        var textoLargo = new string('a', 501);

        var ex = Assert.Throws<ArgumentException>(
            () => vacante.ActualizarObservaciones(textoLargo));

        Assert.Equal("Observaciones", ex.ParamName);
        Assert.Contains("500", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActualizarObservaciones_Nulo_Limpia()
    {
        var vacante = CrearVacanteValida();
        vacante.ActualizarObservaciones("Texto inicial");

        vacante.ActualizarObservaciones(null);

        Assert.Null(vacante.Observaciones);
    }

    [Fact]
    public void ActualizarObservaciones_SoloEspacios_Limpia()
    {
        var vacante = CrearVacanteValida();
        vacante.ActualizarObservaciones("Texto inicial");

        vacante.ActualizarObservaciones("   \t  ");

        Assert.Null(vacante.Observaciones);
    }

    [Fact]
    public void ActualizarObservaciones_Vacio_Limpia()
    {
        var vacante = CrearVacanteValida();
        vacante.ActualizarObservaciones("Texto inicial");

        vacante.ActualizarObservaciones(string.Empty);

        Assert.Null(vacante.Observaciones);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static Vacante CrearVacanteValida()
    {
        var fechaApertura = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        return new Vacante(
            Guid.NewGuid(),
            Guid.NewGuid(),
            fechaApertura,
            "Apertura por renuncia del titular anterior.");
    }
}
