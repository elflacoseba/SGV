using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifies that TipoUnidadOrganizativaConstantes contains exactly 7 unique, non-empty Guids.
/// </summary>
public sealed class TipoUnidadOrganizativaConstantesTests
{
    private static readonly Guid[] AllGuids =
    [
        TipoUnidadOrganizativaConstantes.InstitucionId,
        TipoUnidadOrganizativaConstantes.FacultadId,
        TipoUnidadOrganizativaConstantes.SecretariaId,
        TipoUnidadOrganizativaConstantes.DireccionId,
        TipoUnidadOrganizativaConstantes.DepartamentoId,
        TipoUnidadOrganizativaConstantes.DivisionId,
        TipoUnidadOrganizativaConstantes.AreaId
    ];

    [Fact]
    public void Constantes_TieneExactamente7Valores()
    {
        Assert.Equal(7, AllGuids.Length);
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
