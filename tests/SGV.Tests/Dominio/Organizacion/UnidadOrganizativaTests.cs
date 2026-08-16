using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Dominio.Organizacion;

public sealed class UnidadOrganizativaTests
{
    private static readonly Guid TipoUnidadValido = Guid.Parse("60000000-0000-0000-0000-000000000001");

    [Fact]
    public void Crear_ConTipoUnidadOrganizativaIdNoVacio_AsignaPropiedad()
    {
        var unidad = new UnidadOrganizativa("COD-01", "Unidad Test", TipoUnidadValido);

        Assert.Equal(TipoUnidadValido, unidad.TipoUnidadOrganizativaId);
        Assert.Equal("COD-01", unidad.Codigo);
        Assert.Equal("Unidad Test", unidad.Nombre);
        Assert.True(unidad.IsActive);
    }

    [Fact]
    public void Crear_ConTipoUnidadOrganizativaIdVacio_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new UnidadOrganizativa("COD-02", "Otra Unidad", Guid.Empty));
        Assert.Contains("TipoUnidadOrganizativaId", ex.Message);
    }

    // ── Codigo inmutable tras creación ──────────────────────────

    [Fact]
    public void Codigo_EsInmutableTrasCreacion()
    {
        var unidad = new UnidadOrganizativa("COD-01", "Unidad Test", TipoUnidadValido);

        var codigoProperty = typeof(UnidadOrganizativa).GetProperty(nameof(UnidadOrganizativa.Codigo));
        Assert.NotNull(codigoProperty);

        // Tras issue #124: las propiedades de UO pasaron de `init` a `private set`
        // para paridad con las otras 5 entidades. El invariante "Codigo solo se
        // asigna en el constructor / Reconstitute" se preserva verificando que
        // el setter existe pero NO es accesible públicamente.
        var publicSetter = codigoProperty!.GetSetMethod(nonPublic: false);
        Assert.Null(publicSetter);

        var nonPublicSetter = codigoProperty.GetSetMethod(nonPublic: true);
        Assert.NotNull(nonPublicSetter);
        Assert.False(nonPublicSetter!.IsPublic);
    }

    // ── Actualizar ──────────────────────────────────────────────

    [Fact]
    public void Actualizar_ModificaCamposEditables_PeroNoCodigo()
    {
        var unidad = new UnidadOrganizativa("COD-03", "Unidad Original", TipoUnidadValido);
        var nuevoTipoId = Guid.Parse("60000000-0000-0000-0000-000000000002");

        // Tras issue #124: Actualizar devuelve void y muta la misma instancia.
        unidad.Actualizar("Unidad Modificada", "Descripción", nuevoTipoId, null, null, null);

        Assert.Equal(nuevoTipoId, unidad.TipoUnidadOrganizativaId);
        Assert.Equal("Unidad Modificada", unidad.Nombre);
        Assert.Equal("Descripción", unidad.Descripcion);
        // Codigo preservado por el invariante: Actualizar no acepta codigo como parámetro.
        Assert.Equal("COD-03", unidad.Codigo);
    }

    [Fact]
    public void Actualizar_CodigoNoCambia()
    {
        var unidad = new UnidadOrganizativa("RECT", "Rectorado", TipoUnidadValido);

        unidad.Actualizar("Rectorado Actualizado", "Nueva descripción", TipoUnidadValido, null, null, null);

        Assert.Equal("RECT", unidad.Codigo);
    }

    [Fact]
    public void Actualizar_ConTipoUnidadOrganizativaIdVacio_ThrowsArgumentException()
    {
        var unidad = new UnidadOrganizativa("COD-04", "Unidad", TipoUnidadValido);

        var ex = Assert.Throws<ArgumentException>(
            () => unidad.Actualizar("Unidad", "Desc", Guid.Empty, null, null, null));
        Assert.Contains("TipoUnidadOrganizativaId", ex.Message);
    }

    [Fact]
    public void Actualizar_ConNombreVacio_ThrowsArgumentException()
    {
        var unidad = new UnidadOrganizativa("COD-04", "Unidad", TipoUnidadValido);

        Assert.Throws<ArgumentException>(
            () => unidad.Actualizar("", null, TipoUnidadValido, null, null, null));
    }

    [Fact]
    public void Actualizar_ConVigenciaInvalida_ThrowsInvalidOperationException()
    {
        var unidad = new UnidadOrganizativa("COD-04", "Unidad", TipoUnidadValido);

        Assert.Throws<InvalidOperationException>(
            () => unidad.Actualizar("Unidad", null, TipoUnidadValido, null,
                new DateOnly(2025, 6, 1), new DateOnly(2025, 5, 1)));
    }

    // ── Crear ───────────────────────────────────────────────────

    [Fact]
    public void Crear_ConTipoUnidadOrganizativaIdNoVacio_NoTienePropiedadTipoUnidad()
    {
        var unidad = new UnidadOrganizativa("COD-05", "Unidad", TipoUnidadValido);

        var tipoUnidadProp = typeof(UnidadOrganizativa).GetProperty("TipoUnidad");
        Assert.Null(tipoUnidadProp);
    }

    // ── EsVigente (issue #281) ───────────────────────────────────

    [Fact]
    public void EsVigente_SinVentanaDefinida_DevuelveTrue()
    {
        var unidad = new UnidadOrganizativa("COD-V1", "Sin ventana", TipoUnidadValido);

        Assert.True(unidad.EsVigente(new DateOnly(2025, 6, 15)));
    }

    [Fact]
    public void EsVigente_ConVigenteDesdeFuturo_DevuelveFalseAntesDelInicio()
    {
        var unidad = new UnidadOrganizativa("COD-V2", "Desde futuro", TipoUnidadValido);
        unidad.DefinirVigencia(new DateOnly(2030, 1, 1), null);

        Assert.False(unidad.EsVigente(new DateOnly(2025, 6, 15)));
        Assert.False(unidad.EsVigente(new DateOnly(2029, 12, 31)));
    }

    [Fact]
    public void EsVigente_ConVigenteDesdeFuturo_DevuelveTrueEnODespuesDelInicio()
    {
        var unidad = new UnidadOrganizativa("COD-V3", "Desde futuro activo", TipoUnidadValido);
        unidad.DefinirVigencia(new DateOnly(2025, 1, 1), null);

        Assert.True(unidad.EsVigente(new DateOnly(2025, 1, 1)));
        Assert.True(unidad.EsVigente(new DateOnly(2025, 6, 15)));
        Assert.True(unidad.EsVigente(new DateOnly(2099, 12, 31)));
    }

    [Fact]
    public void EsVigente_ConVigenteHastaPasado_DevuelveFalse()
    {
        var unidad = new UnidadOrganizativa("COD-V4", "Hasta pasado", TipoUnidadValido);
        unidad.DefinirVigencia(null, new DateOnly(2024, 12, 31));

        Assert.False(unidad.EsVigente(new DateOnly(2025, 1, 1)));
        Assert.False(unidad.EsVigente(new DateOnly(2030, 6, 15)));
    }

    [Fact]
    public void EsVigente_ConVigenteHastaFuturo_DevuelveTrueIncluyendoLimite()
    {
        var unidad = new UnidadOrganizativa("COD-V5", "Hasta futuro", TipoUnidadValido);
        unidad.DefinirVigencia(null, new DateOnly(2030, 12, 31));

        Assert.True(unidad.EsVigente(new DateOnly(2025, 6, 15)));
        Assert.True(unidad.EsVigente(new DateOnly(2030, 12, 31)));
    }

    [Fact]
    public void EsVigente_ConRangoCompleto_DevuelveTrueDentro_FalseFuera()
    {
        var unidad = new UnidadOrganizativa("COD-V6", "Rango completo", TipoUnidadValido);
        unidad.DefinirVigencia(new DateOnly(2025, 1, 1), new DateOnly(2030, 12, 31));

        // Antes del rango → false (aún no vigente)
        Assert.False(unidad.EsVigente(new DateOnly(2024, 12, 31)));

        // Dentro del rango (incluyendo los límites) → true
        Assert.True(unidad.EsVigente(new DateOnly(2025, 1, 1)));
        Assert.True(unidad.EsVigente(new DateOnly(2025, 6, 15)));
        Assert.True(unidad.EsVigente(new DateOnly(2030, 12, 31)));

        // Después del rango → false (fuera de vigencia)
        Assert.False(unidad.EsVigente(new DateOnly(2031, 1, 1)));
    }
}
