using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Dominio.Organizacion;

public sealed class NivelesCargoTests
{
    private static readonly Guid[] AllGuids =
    [
        NivelesCargo.DirectivoId,
        NivelesCargo.ConduccionMediaId,
        NivelesCargo.OperativoId,
        NivelesCargo.AcademicoId
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
}
