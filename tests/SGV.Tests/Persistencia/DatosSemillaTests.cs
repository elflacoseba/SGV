using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Verifies that DatosSemilla seed IDs match TipoUnidadOrganizativaConstantes.
/// </summary>
public sealed class DatosSemillaTests
{
    [Fact]
    public void DatosSemilla_SeedIdsMatchTipoUnidadOrganizativaConstantes()
    {
        // These are the Ids used in DatosSemilla.cs for TipoUnidadOrganizativaEntity.HasData
        var seedIds = new[]
        {
            TipoUnidadOrganizativaConstantes.InstitucionId,
            TipoUnidadOrganizativaConstantes.FacultadId,
            TipoUnidadOrganizativaConstantes.SecretariaId,
            TipoUnidadOrganizativaConstantes.DireccionId,
            TipoUnidadOrganizativaConstantes.DepartamentoId,
            TipoUnidadOrganizativaConstantes.DivisionId,
            TipoUnidadOrganizativaConstantes.AreaId
        };

        Assert.Equal(7, seedIds.Length);
        Assert.Equal(7, new HashSet<Guid>(seedIds).Count);
        Assert.All(seedIds, id => Assert.NotEqual(Guid.Empty, id));

        // Verify specific known values
        Assert.Contains(TipoUnidadOrganizativaConstantes.InstitucionId, seedIds);
        Assert.Contains(TipoUnidadOrganizativaConstantes.AreaId, seedIds);

        // Verify NivelCargo seed matches NivelCargoConstantes
        var nivelCargoSeedIds = new[]
        {
            NivelCargoConstantes.DirectivoId,
            NivelCargoConstantes.ConduccionMediaId,
            NivelCargoConstantes.OperativoId,
            NivelCargoConstantes.AcademicoId
        };

        Assert.Equal(4, nivelCargoSeedIds.Length);
        Assert.Equal(4, new HashSet<Guid>(nivelCargoSeedIds).Count);
        Assert.All(nivelCargoSeedIds, id => Assert.NotEqual(Guid.Empty, id));
    }
}
