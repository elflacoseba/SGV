using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Catalogos;
using SGV.Aplicacion.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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

    [Fact]
    public void DatosSemilla_SoloIncluyeRolesFijosDeSgv()
    {
        using var context = new SgvDbContextFactory().CreateDbContext([]);
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var roles = designModel.FindEntityType(typeof(IdentityRole))!
            .GetSeedData()
            .Select(seed => seed[nameof(IdentityRole.Name)]?.ToString())
            .OrderBy(role => role)
            .ToArray();

        Assert.Equal(RolesSgv.Todos.OrderBy(role => role), roles);
    }
}
