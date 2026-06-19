using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Dominio.Organizacion;

public sealed class PuestoTests
{
    private static readonly Guid UnidadIdValida = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid CargoIdValido = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    // ── Constructor ─────────────────────────────────────────────

    [Fact]
    public void Crear_ConValoresValidos_AsignaPropiedades()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente General");

        Assert.NotEqual(Guid.Empty, puesto.Id);
        Assert.Equal("GER-001", puesto.Codigo);
        Assert.Equal("Gerente General", puesto.Nombre);
        Assert.Equal(UnidadIdValida, puesto.UnidadOrganizativaId);
        Assert.Equal(CargoIdValido, puesto.CargoId);
        Assert.Null(puesto.PuestoSuperiorId);
        Assert.Null(puesto.Descripcion);
        Assert.True(puesto.IsActive);
        Assert.False(puesto.IsDeleted);
    }

    [Fact]
    public void Crear_ConSuperiorOpcional_AsignaPuestoSuperiorId()
    {
        var superiorId = Guid.NewGuid();

        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente General", superiorId);

        Assert.Equal(superiorId, puesto.PuestoSuperiorId);
    }

    [Fact]
    public void Crear_ConDescripcion_AsignaDescripcion()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente General", null, "Descripción ejecutiva");

        Assert.Equal("Descripción ejecutiva", puesto.Descripcion);
    }

    [Fact]
    public void Crear_ConCodigoVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, CargoIdValido, "", "Gerente"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, CargoIdValido, null!, "Gerente"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var codigoLargo = new string('A', 51);
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, CargoIdValido, codigoLargo, "Gerente"));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, CargoIdValido, "GER-001", ""));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, CargoIdValido, "GER-001", null!));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreMayorA200_ThrowsArgumentException()
    {
        var nombreLargo = new string('A', 201);
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, CargoIdValido, "GER-001", nombreLargo));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConUnidadOrganizativaIdVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(Guid.Empty, CargoIdValido, "GER-001", "Gerente"));
        Assert.Contains("UnidadOrganizativaId", ex.Message);
    }

    [Fact]
    public void Crear_ConCargoIdVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, Guid.Empty, "GER-001", "Gerente"));
        Assert.Contains("CargoId", ex.Message);
    }

    [Fact]
    public void Crear_DescripcionMayorA1000_ThrowsArgumentException()
    {
        var descripcionLarga = new string('A', 1001);
        var ex = Assert.Throws<ArgumentException>(
            () => new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente", null, descripcionLarga));
        Assert.Contains("Descripcion", ex.Message);
    }

    [Fact]
    public void Crear_AutorreferenciaComoSuperior_ThrowsInvalidOperationException()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        var ex = Assert.Throws<InvalidOperationException>(
            () => puesto.CambiarPuestoSuperior(puesto.Id));
        Assert.Contains("superior de sí mismo", ex.Message);
    }

    // ── Codigo inmutable tras creación ──────────────────────────

    [Fact]
    public void Codigo_EsInmutableTrasCreacion()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        var codigoProperty = typeof(Puesto).GetProperty(nameof(Puesto.Codigo));
        Assert.NotNull(codigoProperty);
        Assert.Null(codigoProperty!.GetSetMethod());
    }

    // ── Actualizar ──────────────────────────────────────────────

    [Fact]
    public void Actualizar_ModificaCamposEditables()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");
        var superiorId = Guid.NewGuid();

        puesto.Actualizar("Gerente General", "Nueva descripción", superiorId);

        Assert.Equal("GER-001", puesto.Codigo); // Inmutable
        Assert.Equal("Gerente General", puesto.Nombre);
        Assert.Equal("Nueva descripción", puesto.Descripcion);
        Assert.Equal(superiorId, puesto.PuestoSuperiorId);
    }

    [Fact]
    public void Actualizar_CodigoNoCambia()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        puesto.Actualizar("Gerente Actualizado", null, null);

        Assert.Equal("GER-001", puesto.Codigo);
    }

    [Fact]
    public void Actualizar_ConNombreVacio_ThrowsArgumentException()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        var ex = Assert.Throws<ArgumentException>(
            () => puesto.Actualizar("", null, null));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Actualizar_DescripcionMayorA1000_ThrowsArgumentException()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");
        var descripcionLarga = new string('A', 1001);

        var ex = Assert.Throws<ArgumentException>(
            () => puesto.Actualizar("Gerente", descripcionLarga, null));
        Assert.Contains("Descripcion", ex.Message);
    }

    [Fact]
    public void Actualizar_SuperiorVacio_LiberaPuestoSuperiorId()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente", Guid.NewGuid());

        puesto.Actualizar("Gerente", null, null);

        Assert.Null(puesto.PuestoSuperiorId);
    }

    // ── Desactivar ──────────────────────────────────────────────

    [Fact]
    public void Desactivar_SeteaIsActiveFalse()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        puesto.Desactivar();

        Assert.False(puesto.IsActive);
    }

    [Fact]
    public void Desactivar_NoCambiaCodigo()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        puesto.Desactivar();

        Assert.Equal("GER-001", puesto.Codigo);
    }

    [Fact]
    public void Desactivar_NoCambiaNombre()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        puesto.Desactivar();

        Assert.Equal("Gerente", puesto.Nombre);
    }

    [Fact]
    public void Desactivar_PuestoYaInactivo_MantieneIsActiveFalse()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");
        puesto.Desactivar();

        puesto.Desactivar();

        Assert.False(puesto.IsActive);
    }

    // ── Activar ─────────────────────────────────────────────────

    [Fact]
    public void Activar_SeteaIsActiveTrue()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");
        puesto.Desactivar();

        puesto.Activar();

        Assert.True(puesto.IsActive);
    }

    [Fact]
    public void Activar_NoCambiaCodigo()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");
        puesto.Desactivar();

        puesto.Activar();

        Assert.Equal("GER-001", puesto.Codigo);
    }

    [Fact]
    public void Activar_PuestoYaActivo_MantieneIsActiveTrue()
    {
        var puesto = new Puesto(UnidadIdValida, CargoIdValido, "GER-001", "Gerente");

        puesto.Activar();

        Assert.True(puesto.IsActive);
    }
}
