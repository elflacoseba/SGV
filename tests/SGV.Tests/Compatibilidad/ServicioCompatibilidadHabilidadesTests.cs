using SGV.Aplicacion.Compatibilidad;
using Xunit;

namespace SGV.Tests.Compatibilidad;

public sealed class ServicioCompatibilidadHabilidadesTests
{
    private readonly ServicioCompatibilidadHabilidades _servicio = new();

    [Fact]
    public void Calcular_DevuelveTotal_CuandoCumpleTodasLasHabilidadesObligatorias()
    {
        var habilidad = Guid.NewGuid();

        var resultado = _servicio.Calcular(
            [new RequisitoHabilidad(habilidad, 3, 2, true)],
            [new HabilidadPersona(habilidad, 4)]);

        Assert.Equal(100, resultado.Puntaje);
        Assert.Equal("Total", resultado.Categoria);
        Assert.True(resultado.CumplePerfilCompleto);
    }

    [Fact]
    public void Calcular_DevuelveInsuficiente_CuandoNivelEsMuyInferior()
    {
        var habilidad = Guid.NewGuid();

        var resultado = _servicio.Calcular(
            [new RequisitoHabilidad(habilidad, 4, 1, true)],
            [new HabilidadPersona(habilidad, 2)]);

        Assert.Equal(50, resultado.Puntaje);
        Assert.Equal("Insuficiente", resultado.Categoria);
        Assert.False(resultado.CumplePerfilCompleto);
    }

    [Fact]
    public void Calcular_AplicaPonderacion_CuandoHayMultiplesHabilidades()
    {
        var critica = Guid.NewGuid();
        var deseable = Guid.NewGuid();

        var resultado = _servicio.Calcular(
            [
                new RequisitoHabilidad(critica, 4, 3, true),
                new RequisitoHabilidad(deseable, 2, 1, false)
            ],
            [
                new HabilidadPersona(critica, 4),
                new HabilidadPersona(deseable, 1)
            ]);

        Assert.Equal(87.50m, resultado.Puntaje);
        Assert.Equal("Parcial", resultado.Categoria);
    }
}
