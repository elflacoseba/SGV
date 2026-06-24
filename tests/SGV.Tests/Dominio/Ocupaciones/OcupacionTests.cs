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

    // ── Actualizar ───────────────────────────────────────────────

    [Fact]
    public void Actualizar_EnOcupacionActiva_ActualizaPropiedades()
    {
        var ocupacion = CrearOcupacionActiva();
        var nuevaPersonaId = Guid.NewGuid();
        var nuevoPuestoId = Guid.NewGuid();
        var nuevaFechaInicio = new DateOnly(2025, 3, 1);

        ocupacion.Actualizar(nuevaPersonaId, nuevoPuestoId, nuevaFechaInicio,
            TipoAsignacion.Temporal, "Asignación temporal actualizada");

        Assert.Equal(nuevaPersonaId, ocupacion.PersonaId);
        Assert.Equal(nuevoPuestoId, ocupacion.PuestoId);
        Assert.Equal(nuevaFechaInicio, ocupacion.FechaInicio);
        Assert.Equal(TipoAsignacion.Temporal, ocupacion.TipoAsignacion);
        Assert.Equal("Asignación temporal actualizada", ocupacion.Observaciones);
    }

    [Fact]
    public void Actualizar_EnOcupacionFinalizada_LanzaInvalidOperationException()
    {
        var ocupacion = CrearOcupacionFinalizada();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.Actualizar(
                Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 3, 1),
                TipoAsignacion.Permanente, null));

        Assert.Contains("no se puede", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Actualizar_EnOcupacionEliminada_LanzaInvalidOperationException()
    {
        var ocupacion = CrearOcupacionActiva();
        ocupacion.EliminarLogicamente();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.Actualizar(
                Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 3, 1),
                TipoAsignacion.Permanente, null));

        Assert.Contains("no se puede", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Finalizar (extended) ─────────────────────────────────────

    [Fact]
    public void Finalizar_EnOcupacionYaFinalizada_LanzaInvalidOperationException()
    {
        var ocupacion = CrearOcupacionFinalizada();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.Finalizar(new DateOnly(2026, 1, 1)));

        Assert.Contains("no se puede", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Finalizar_EnOcupacionEliminada_LanzaInvalidOperationException()
    {
        var ocupacion = CrearOcupacionActiva();
        ocupacion.EliminarLogicamente();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.Finalizar(new DateOnly(2026, 1, 1)));

        Assert.Contains("no se puede", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── EliminarLogicamente ──────────────────────────────────────

    [Fact]
    public void EliminarLogicamente_EnOcupacionActiva_MarcaEliminada()
    {
        var ocupacion = CrearOcupacionActiva();

        ocupacion.EliminarLogicamente();

        Assert.True(ocupacion.IsDeleted);
        Assert.NotNull(ocupacion.DeletedAt);
        Assert.False(ocupacion.EsVigente);
    }

    [Fact]
    public void EliminarLogicamente_EnOcupacionFinalizada_LanzaInvalidOperationException()
    {
        var ocupacion = CrearOcupacionFinalizada();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.EliminarLogicamente());

        Assert.Contains("no se puede", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EliminarLogicamente_EnOcupacionYaEliminada_LanzaInvalidOperationException()
    {
        var ocupacion = CrearOcupacionActiva();
        ocupacion.EliminarLogicamente();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.EliminarLogicamente());

        Assert.Contains("no se puede", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Reactivar ────────────────────────────────────────────────

    [Fact]
    public void Reactivar_EnOcupacionFinalizada_RestauraActiva()
    {
        var ocupacion = CrearOcupacionFinalizada();
        Assert.False(ocupacion.EsVigente);

        ocupacion.Reactivar();

        Assert.Null(ocupacion.FechaFin);
        Assert.False(ocupacion.IsDeleted);
        Assert.True(ocupacion.EsVigente);
    }

    [Fact]
    public void Reactivar_EnOcupacionEliminada_RestauraActiva()
    {
        var ocupacion = CrearOcupacionActiva();
        ocupacion.EliminarLogicamente();
        Assert.True(ocupacion.IsDeleted);

        ocupacion.Reactivar();

        Assert.Null(ocupacion.FechaFin);
        Assert.False(ocupacion.IsDeleted);
        Assert.True(ocupacion.EsVigente);
    }

    [Fact]
    public void Reactivar_EnOcupacionActiva_LanzaInvalidOperationException()
    {
        var ocupacion = CrearOcupacionActiva();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ocupacion.Reactivar());

        Assert.Contains("no se puede", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static Ocupacion CrearOcupacionActiva()
    {
        return new Ocupacion(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 1, 1),
            TipoAsignacion.Permanente);
    }

    private static Ocupacion CrearOcupacionFinalizada()
    {
        var ocupacion = CrearOcupacionActiva();
        ocupacion.Finalizar(new DateOnly(2025, 12, 31));
        return ocupacion;
    }
}
