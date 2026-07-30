using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifies that TipoUnidadOrganizativaConstantes contains exactly 20 unique, non-empty Guids.
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
        TipoUnidadOrganizativaConstantes.AreaId,
        TipoUnidadOrganizativaConstantes.SedeId,
        TipoUnidadOrganizativaConstantes.RegionId,
        TipoUnidadOrganizativaConstantes.GerenciaId,
        TipoUnidadOrganizativaConstantes.VicepresidenciaId,
        TipoUnidadOrganizativaConstantes.SubgerenciaId,
        TipoUnidadOrganizativaConstantes.CoordinacionId,
        TipoUnidadOrganizativaConstantes.SeccionId,
        TipoUnidadOrganizativaConstantes.OficinaId,
        TipoUnidadOrganizativaConstantes.EquipoId,
        TipoUnidadOrganizativaConstantes.CelulaId,
        TipoUnidadOrganizativaConstantes.PlantaId,
        TipoUnidadOrganizativaConstantes.SucursalId,
        TipoUnidadOrganizativaConstantes.EscuelaId
    ];

    [Fact]
    public void Constantes_TieneExactamente20Valores()
    {
        Assert.Equal(20, AllGuids.Length);
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
