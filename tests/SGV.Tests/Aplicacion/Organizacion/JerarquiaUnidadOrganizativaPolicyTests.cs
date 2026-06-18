using SGV.Aplicacion.Organizacion.Comandos;
using Xunit;

namespace SGV.Tests.Aplicacion.Organizacion;

public sealed class JerarquiaUnidadOrganizativaPolicyTests
{
    private static readonly JerarquiaUnidadOrganizativaPolicy Policy = new();

    // ===== Task 1.3: Seed type matrix validation =====

    [Theory]
    [InlineData("Institucion", null, true)]        // root — no parent allowed
    [InlineData("Institucion", "Facultad", false)]  // root cannot have a parent
    [InlineData("Facultad", "Institucion", true)]   // OK
    [InlineData("Facultad", "Direccion", false)]    // NOT allowed
    [InlineData("Secretaria", "Institucion", true)] // OK
    [InlineData("Secretaria", "Facultad", true)]    // OK
    [InlineData("Secretaria", "Direccion", false)]  // NOT allowed
    [InlineData("Direccion", "Institucion", true)]  // OK
    [InlineData("Direccion", "Facultad", true)]     // OK
    [InlineData("Direccion", "Secretaria", true)]   // OK
    [InlineData("Direccion", "Departamento", false)]// NOT allowed
    [InlineData("Departamento", "Facultad", true)]  // OK
    [InlineData("Departamento", "Direccion", true)] // OK
    [InlineData("Departamento", "Institucion", false)]// NOT allowed
    [InlineData("Division", "Direccion", true)]     // OK
    [InlineData("Division", "Departamento", true)]  // OK
    [InlineData("Division", "Facultad", false)]     // NOT allowed
    [InlineData("Area", "Secretaria", true)]        // OK
    [InlineData("Area", "Direccion", true)]         // OK
    [InlineData("Area", "Departamento", true)]      // OK
    [InlineData("Area", "Division", true)]          // OK
    [InlineData("Area", "Facultad", false)]         // NOT allowed
    [InlineData("Area", "Institucion", false)]      // NOT allowed
    public void EsRelacionPermitida_RetornaResultadoEsperado(
        string tipoCodigoHija, string? tipoCodigoPadre, bool esperado)
    {
        var resultado = Policy.EsRelacionPermitida(tipoCodigoHija, tipoCodigoPadre);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void EsRelacionPermitida_CodigoDesconocido_RetornaFalse()
    {
        Assert.False(Policy.EsRelacionPermitida("Inexistente", null));
        Assert.False(Policy.EsRelacionPermitida("Inexistente", "Institucion"));
    }

    [Fact]
    public void EsRelacionPermitida_TodosLosTiposSemillaTienenEntrada()
    {
        var tiposSemilla = new[]
        {
            "Institucion", "Facultad", "Secretaria", "Direccion",
            "Departamento", "Division", "Area"
        };

        foreach (var tipo in tiposSemilla)
        {
            // Every seed type must have an entry (calling with null/empty should not throw)
            var ex = Record.Exception(() => Policy.EsRelacionPermitida(tipo, null));
            Assert.Null(ex);
        }
    }

    // ===== Vigencia containment tests =====

    [Theory]
    [InlineData(null, null, null, null, true)]                          // both null
    [InlineData(null, null, "2025-01-01", "2025-12-31", true)]          // hija sin fechas
    [InlineData("2025-03-01", "2025-06-30", "2025-01-01", "2025-12-31", true)]   // inside
    [InlineData("2025-01-01", "2025-12-31", "2025-01-01", "2025-12-31", true)]   // exact match
    [InlineData("2025-01-01", null, "2025-01-01", "2025-12-31", true)]           // hija sin fin (open-ended)
    [InlineData("2024-01-01", "2025-12-31", "2025-01-01", "2025-12-31", false)]  // hija empieza antes
    [InlineData("2025-01-01", "2026-01-01", "2025-01-01", "2025-12-31", false)]  // hija termina después
    [InlineData("2024-01-01", "2024-12-31", "2025-01-01", "2025-12-31", false)]  // completamente fuera
    [InlineData("2025-01-01", "2025-06-30", null, null, true)]                   // padre sin fechas
    public void EsVigenciaContenida_RetornaResultadoEsperado(
        string? hijaDesdeStr, string? hijaHastaStr,
        string? padreDesdeStr, string? padreHastaStr,
        bool esperado)
    {
        DateOnly? hijaDesde = hijaDesdeStr is not null ? DateOnly.Parse(hijaDesdeStr) : null;
        DateOnly? hijaHasta = hijaHastaStr is not null ? DateOnly.Parse(hijaHastaStr) : null;
        DateOnly? padreDesde = padreDesdeStr is not null ? DateOnly.Parse(padreDesdeStr) : null;
        DateOnly? padreHasta = padreHastaStr is not null ? DateOnly.Parse(padreHastaStr) : null;

        var resultado = Policy.EsVigenciaContenida(hijaDesde, hijaHasta, padreDesde, padreHasta);
        Assert.Equal(esperado, resultado);
    }
}
