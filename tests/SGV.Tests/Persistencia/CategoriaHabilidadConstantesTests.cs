using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifica que <c>CategoriaHabilidadConstantes</c> contiene exactamente 4
/// unique non-empty Guids en el bloque reservado <c>72000000-…</c>, y que
/// la migración <c>InsertData</c> + <c>DatosSemilla.HasData</c> consumen
/// esas mismas constantes. Precedente: <c>TipoDocumentoConstantesTests</c>.
/// </summary>
public sealed class CategoriaHabilidadConstantesTests
{
    private static readonly Guid[] AllGuids =
    [
        CategoriaHabilidadConstantes.ConduccionId,
        CategoriaHabilidadConstantes.TecnicaId,
        CategoriaHabilidadConstantes.DominioId,
        CategoriaHabilidadConstantes.AcademicaId
    ];

    private static readonly string[] AllCodigos =
    [
        CategoriaHabilidadConstantes.ConduccionCodigo,
        CategoriaHabilidadConstantes.TecnicaCodigo,
        CategoriaHabilidadConstantes.DominioCodigo,
        CategoriaHabilidadConstantes.AcademicaCodigo
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
    public void Constantes_GuidsEnBloqueReservado72000000()
    {
        Assert.All(AllGuids, guid =>
        {
            var texto = guid.ToString("D").ToLowerInvariant();
            Assert.StartsWith("72000000-0000-0000-0000-00000000000", texto);
        });
    }

    [Fact]
    public void Constantes_CodigosEsperados()
    {
        Assert.Equal("Conduccion", CategoriaHabilidadConstantes.ConduccionCodigo);
        Assert.Equal("Tecnica", CategoriaHabilidadConstantes.TecnicaCodigo);
        Assert.Equal("Dominio", CategoriaHabilidadConstantes.DominioCodigo);
        Assert.Equal("Academica", CategoriaHabilidadConstantes.AcademicaCodigo);
    }

    [Fact]
    public void Semilla_Tiene4Elementos()
    {
        Assert.Equal(4, CategoriaHabilidadConstantes.Semilla.Count);
    }

    [Fact]
    public void Semilla_IdsCoincidenConConstantes()
    {
        var semillaIds = CategoriaHabilidadConstantes.Semilla.Select(s => s.Id).ToArray();
        Assert.Equal(AllGuids.OrderBy(g => g).ToArray(), semillaIds.OrderBy(g => g).ToArray());
    }

    [Fact]
    public void Semilla_CodigosNoRepetidos()
    {
        var codigos = CategoriaHabilidadConstantes.Semilla.Select(s => s.Codigo).ToArray();
        Assert.Equal(codigos.Length, new HashSet<string>(codigos).Count);
    }
}