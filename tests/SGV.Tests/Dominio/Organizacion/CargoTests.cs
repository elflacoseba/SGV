using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Dominio.Organizacion;

public sealed class CargoTests
{
    private static readonly Guid NivelIdValido = Guid.Parse("70000000-0000-0000-0000-000000000001");

    [Fact]
    public void Crear_ConValoresValidos_AsignaPropiedades()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        Assert.NotEqual(Guid.Empty, cargo.Id);
        Assert.Equal("DIR-01", cargo.Codigo);
        Assert.Equal("Director", cargo.Nombre);
        Assert.Equal(NivelIdValido, cargo.NivelId);
        Assert.Null(cargo.Descripcion);
        Assert.True(cargo.IsActive);
        Assert.False(cargo.IsDeleted);
    }

    [Fact]
    public void Crear_ConDescripcion_AsignaDescripcion()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido, "Descripción");

        Assert.Equal("Descripción", cargo.Descripcion);
    }

    [Fact]
    public void Crear_ConCodigoVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Cargo("", "Director", NivelIdValido));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoNull_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Cargo(null!, "Director", NivelIdValido));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var codigoLargo = new string('A', 51);
        var ex = Assert.Throws<ArgumentException>(
            () => new Cargo(codigoLargo, "Director", NivelIdValido));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Crear_ConNombreVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Cargo("DIR-01", "", NivelIdValido));
        Assert.Contains("Nombre", ex.Message);
    }

    [Fact]
    public void Crear_ConNivelIdVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Cargo("DIR-01", "Director", Guid.Empty));
        Assert.Contains("NivelId", ex.Message);
    }

    [Fact]
    public void Crear_DescripcionMayorA1000_ThrowsArgumentException()
    {
        var descripcionLarga = new string('A', 1001);
        var ex = Assert.Throws<ArgumentException>(
            () => new Cargo("DIR-01", "Director", NivelIdValido, descripcionLarga));
        Assert.Contains("Descripcion", ex.Message);
    }

    [Fact]
    public void Actualizar_ModificaCamposEditables()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);
        var nuevoNivelId = Guid.Parse("70000000-0000-0000-0000-000000000002");

        cargo.Actualizar("DIR-01", "Director General", nuevoNivelId, "Nueva descripción");

        Assert.Equal("DIR-01", cargo.Codigo);
        Assert.Equal("Director General", cargo.Nombre);
        Assert.Equal(nuevoNivelId, cargo.NivelId);
        Assert.Equal("Nueva descripción", cargo.Descripcion);
    }

    [Fact]
    public void Actualizar_CambiaCodigoSiNoDuplica()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        cargo.Actualizar("DIR-02", "Director", NivelIdValido);

        Assert.Equal("DIR-02", cargo.Codigo);
    }

    [Fact]
    public void Actualizar_ConCodigoNull_ThrowsArgumentException()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        var ex = Assert.Throws<ArgumentException>(
            () => cargo.Actualizar(null!, "Director", NivelIdValido));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Actualizar_ConCodigoVacio_ThrowsArgumentException()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        var ex = Assert.Throws<ArgumentException>(
            () => cargo.Actualizar("", "Director", NivelIdValido));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Actualizar_ConCodigoMayorA50_ThrowsArgumentException()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);
        var codigoLargo = new string('A', 51);

        var ex = Assert.Throws<ArgumentException>(
            () => cargo.Actualizar(codigoLargo, "Director", NivelIdValido));
        Assert.Contains("Codigo", ex.Message);
    }

    [Fact]
    public void Actualizar_ConNivelIdVacio_ThrowsArgumentException()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        var ex = Assert.Throws<ArgumentException>(
            () => cargo.Actualizar("DIR-01", "Director", Guid.Empty));
        Assert.Contains("NivelId", ex.Message);
    }

    [Fact]
    public void Desactivar_CargoActivo_SeteaIsActiveFalse()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        cargo.Desactivar();

        Assert.False(cargo.IsActive);
    }

    [Fact]
    public void Desactivar_ConPuestosActivos_ThrowsInvalidOperationException()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);
        // Simular puestos activos cargados en la colección
        var puesto = new Puesto(
            Guid.NewGuid(), cargo.Id, "PUESTO-01", "Puesto Test", null);
        typeof(Puesto).GetProperty("IsActive")!.SetValue(puesto, true);
        AddPuestoToCargo(cargo, puesto);

        var ex = Assert.Throws<InvalidOperationException>(
            () => cargo.Desactivar());
        Assert.Contains("Puestos activos", ex.Message);
    }

    [Fact]
    public void Desactivar_ConPuestosInactivos_NoLanzaExcepcion()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);
        var puesto = new Puesto(
            Guid.NewGuid(), cargo.Id, "PUESTO-01", "Puesto Test", null);
        typeof(Puesto).GetProperty("IsActive")!.SetValue(puesto, false);
        AddPuestoToCargo(cargo, puesto);

        cargo.Desactivar();

        Assert.False(cargo.IsActive);
    }

    [Fact]
    public void Desactivar_SinPuestosCargados_NoLanzaExcepcion()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        cargo.Desactivar();

        Assert.False(cargo.IsActive);
    }

    [Fact]
    public void Activar_CargoInactivo_SeteaIsActiveTrue()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);
        cargo.Desactivar();

        cargo.Activar();

        Assert.True(cargo.IsActive);
    }

    [Fact]
    public void Desactivar_NoCambiaCodigo()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);

        cargo.Desactivar();

        Assert.Equal("DIR-01", cargo.Codigo);
    }

    [Fact]
    public void Activar_NoCambiaCodigo()
    {
        var cargo = new Cargo("DIR-01", "Director", NivelIdValido);
        cargo.Desactivar();

        cargo.Activar();

        Assert.Equal("DIR-01", cargo.Codigo);
    }

    private static void AddPuestoToCargo(Cargo cargo, Puesto puesto)
    {
        var field = typeof(Cargo).GetField("_puestos",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var list = (List<Puesto>)field!.GetValue(cargo)!;
        list.Add(puesto);
    }
}
