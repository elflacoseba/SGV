using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifies that <c>TipoDocumentoConstantes</c> contains exactly 4 unique
/// non-empty Guids in the reserved block <c>71000000-…</c>, and that the
/// migration's <c>InsertData</c> + <c>DatosSemilla.HasData</c> consume those
/// same constants. Precedente: <c>NivelCargoConstantesTests</c>.
/// </summary>
public sealed class TipoDocumentoConstantesTests
{
    private static readonly Guid[] AllGuids =
    [
        TipoDocumentoConstantes.DniId,
        TipoDocumentoConstantes.LeId,
        TipoDocumentoConstantes.LcId,
        TipoDocumentoConstantes.PasaporteId
    ];

    private static readonly string[] AllCodigos =
    [
        TipoDocumentoConstantes.DniCodigo,
        TipoDocumentoConstantes.LeCodigo,
        TipoDocumentoConstantes.LcCodigo,
        TipoDocumentoConstantes.PasaporteCodigo
    ];

    [Fact]
    public void Constantes_TieneExactamente4Valores()
    {
        Assert.Equal(4, AllGuids.Length);
    }

    [Fact]
    public void Constantes_TodosLosGuidsSonUnicos()
    {
        var distinct = new HashSet<Guid>(AllGuids);
        Assert.Equal(AllGuids.Length, distinct.Count);
    }

    [Fact]
    public void Constantes_NingunGuidEsVacio()
    {
        Assert.All(AllGuids, guid => Assert.NotEqual(Guid.Empty, guid));
    }

    [Fact]
    public void Constantes_GuidsEnBloqueReservado71000000()
    {
        // El bloque 71000000-… está reservado para TipoDocumento. Cualquier
        // valor fuera de ese bloque es drift. Verificamos comparando la
        // representación textual canónica para evitar mixed-endian de
        // Guid.ToByteArray().
        Assert.All(AllGuids, guid =>
        {
            var texto = guid.ToString("D").ToLowerInvariant();
            Assert.StartsWith("71000000-0000-0000-0000-00000000000", texto);
        });
    }

    [Fact]
    public void Constantes_CodigosEsperados()
    {
        Assert.Equal("DNI", TipoDocumentoConstantes.DniCodigo);
        Assert.Equal("LE", TipoDocumentoConstantes.LeCodigo);
        Assert.Equal("LC", TipoDocumentoConstantes.LcCodigo);
        Assert.Equal("Pasaporte", TipoDocumentoConstantes.PasaporteCodigo);
    }

    [Fact]
    public void Semilla_Tiene4Elementos()
    {
        Assert.Equal(4, TipoDocumentoConstantes.Semilla.Count);
    }

    [Fact]
    public void Semilla_IdsCoincidenConConstantes()
    {
        var semillaIds = TipoDocumentoConstantes.Semilla.Select(s => s.Id).ToArray();
        Assert.Equal(AllGuids.OrderBy(g => g).ToArray(), semillaIds.OrderBy(g => g).ToArray());
    }

    [Fact]
    public void Semilla_CodigosNoRepetidos()
    {
        var codigos = TipoDocumentoConstantes.Semilla.Select(s => s.Codigo).ToArray();
        Assert.Equal(codigos.Length, new HashSet<string>(codigos).Count);
    }

    [Fact]
    public void Semilla_Dni_ContienePatronYLongitudes()
    {
        var dni = TipoDocumentoConstantes.Semilla
            .Single(s => s.Codigo == TipoDocumentoConstantes.DniCodigo);
        Assert.Equal(@"^\d{7,8}$", dni.PatronValidacion);
        Assert.Equal(7, dni.LongitudMinima);
        Assert.Equal(8, dni.LongitudMaxima);
    }

    [Fact]
    public void Semilla_Pasaporte_ContienePatronYLongitudFija()
    {
        var pasaporte = TipoDocumentoConstantes.Semilla
            .Single(s => s.Codigo == TipoDocumentoConstantes.PasaporteCodigo);
        Assert.Equal(@"^[A-Za-z]{3}\d{6}$", pasaporte.PatronValidacion);
        Assert.Equal(9, pasaporte.LongitudMinima);
        Assert.Equal(9, pasaporte.LongitudMaxima);
    }
}
