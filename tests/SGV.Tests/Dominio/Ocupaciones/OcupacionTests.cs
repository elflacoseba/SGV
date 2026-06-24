using SGV.Dominio.Ocupaciones;
using Xunit;

namespace SGV.Tests.Dominio.Ocupaciones;

public sealed class OcupacionTests
{
    // ── Constructor ─────────────────────────────────────────────

    [Fact]
    public void Crear_ConValoresValidos_AsignaPropiedades()
    {
        var personaId = Guid.NewGuid();
        var puestoId = Guid.NewGuid();
        var fechaInicio = new DateOnly(2025, 1, 1);

        var ocupacion = new Ocupacion(personaId, puestoId, fechaInicio, TipoAsignacion.Permanente);

        Assert.NotEqual(Guid.Empty, ocupacion.Id);
        Assert.Equal(personaId, ocupacion.PersonaId);
        Assert.Equal(puestoId, ocupacion.PuestoId);
        Assert.Equal(fechaInicio, ocupacion.FechaInicio);
        Assert.Equal(TipoAsignacion.Permanente, ocupacion.TipoAsignacion);
        Assert.Null(ocupacion.FechaFin);
        Assert.True(ocupacion.EsVigente);
    }

    [Fact]
    public void Crear_ConFechaFin_AsignaFechaFin()
    {
        var fechaInicio = new DateOnly(2025, 1, 1);
        var fechaFin = new DateOnly(2025, 12, 31);

        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), fechaInicio,
            TipoAsignacion.Interina, fechaFin);

        Assert.Equal(fechaFin, ocupacion.FechaFin);
        Assert.False(ocupacion.EsVigente);
    }

    [Theory]
    [InlineData(TipoAsignacion.Permanente)]
    [InlineData(TipoAsignacion.Interina)]
    [InlineData(TipoAsignacion.Temporal)]
    public void Crear_ConCualquierTipoAsignacionValido_AsignaCorrectamente(TipoAsignacion tipo)
    {
        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1), tipo);

        Assert.Equal(tipo, ocupacion.TipoAsignacion);
    }

    [Fact]
    public void Crear_ConFechaFinAnteriorAFechaInicio_ThrowsInvalidOperationException()
    {
        var fechaInicio = new DateOnly(2025, 6, 1);
        var fechaFinAnterior = new DateOnly(2025, 1, 1);

        var ex = Assert.Throws<InvalidOperationException>(
            () => new Ocupacion(
                Guid.NewGuid(), Guid.NewGuid(), fechaInicio,
                TipoAsignacion.Temporal, fechaFinAnterior));

        Assert.Contains("fecha de fin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Crear_ConFechaFinIgualAFechaInicio_EsValido()
    {
        var mismaFecha = new DateOnly(2025, 6, 1);

        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), mismaFecha,
            TipoAsignacion.Temporal, mismaFecha);

        Assert.Equal(mismaFecha, ocupacion.FechaFin);
        Assert.False(ocupacion.EsVigente);
    }

    [Fact]
    public void Crear_ConFechaFinNula_EsVigente()
    {
        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente, null);

        Assert.Null(ocupacion.FechaFin);
        Assert.True(ocupacion.EsVigente);
    }

    // ── Finalizar ───────────────────────────────────────────────

    [Fact]
    public void Finalizar_ConFechaValida_ActualizaFechaFinYVigencia()
    {
        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente);

        Assert.True(ocupacion.EsVigente);

        ocupacion.Finalizar(new DateOnly(2025, 12, 31));

        Assert.Equal(new DateOnly(2025, 12, 31), ocupacion.FechaFin);
        Assert.False(ocupacion.EsVigente);
    }

    [Fact]
    public void Finalizar_ConFechaAnteriorAFechaInicio_ThrowsInvalidOperationException()
    {
        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 6, 1),
            TipoAsignacion.Interina);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.Finalizar(new DateOnly(2025, 1, 1)));

        Assert.Contains("fecha de fin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Finalizar_ConFechaFinIgualAFechaInicio_EsValido()
    {
        var fecha = new DateOnly(2025, 6, 1);
        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), fecha,
            TipoAsignacion.Temporal);

        ocupacion.Finalizar(fecha);

        Assert.Equal(fecha, ocupacion.FechaFin);
        Assert.False(ocupacion.EsVigente);
    }

    [Fact]
    public void Finalizar_ConObservaciones_ActualizaObservaciones()
    {
        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente);

        ocupacion.Finalizar(new DateOnly(2025, 12, 31), "Renuncia voluntaria");

        Assert.Equal("Renuncia voluntaria", ocupacion.Observaciones);
    }

    [Fact]
    public void Finalizar_SinObservaciones_NoCambiaObservaciones()
    {
        var ocupacion = new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente);

        ocupacion.Finalizar(new DateOnly(2025, 12, 31));

        Assert.Null(ocupacion.Observaciones);
    }

    // ── Inmutabilidad de propiedades clave ──────────────────────

    [Fact]
    public void PropiedadesClave_NoTienenSettersPublicos()
    {
        var tipoAsignacionSetter = typeof(Ocupacion)
            .GetProperty(nameof(Ocupacion.TipoAsignacion))?.GetSetMethod();

        Assert.Null(tipoAsignacionSetter);
    }
}
