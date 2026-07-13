using SGV.Dominio.Organizacion;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Behavior coverage for <see cref="UnidadOrganizativa.Reconstitute"/>.
/// The IL guard for the mapper is already in <c>UnidadOrganizativaRepositoryTests</c>;
/// this file focuses on the factory contract itself.
/// </summary>
public sealed class UnidadOrganizativaReconstituteTests
{
    private static readonly Guid Id = Guid.Parse("60000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TipoId = Guid.Parse("60000000-0000-0000-0000-0000000000a2");
    private static readonly Guid PadreId = Guid.Parse("60000000-0000-0000-0000-0000000000a3");

    [Fact]
    public void Reconstitute_MapsAllFields()
    {
        var unidad = UnidadOrganizativa.Reconstitute(
            Id, "UO-001", "Unidad Test", TipoId, "Desc", PadreId,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31),
            isActive: true,
            unidadPadre: null,
            tipoUnidadOrganizativa: null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.Equal(Id, unidad.Id);
        Assert.Equal("UO-001", unidad.Codigo);
        Assert.Equal("Unidad Test", unidad.Nombre);
        Assert.Equal(TipoId, unidad.TipoUnidadOrganizativaId);
        Assert.Equal("Desc", unidad.Descripcion);
        Assert.Equal(PadreId, unidad.UnidadPadreId);
        Assert.Equal(new DateOnly(2024, 1, 1), unidad.VigenteDesde);
        Assert.Equal(new DateOnly(2024, 12, 31), unidad.VigenteHasta);
        Assert.True(unidad.IsActive);
    }

    [Fact]
    public void Reconstitute_IsActiveFalsePreservaFlag()
    {
        var unidad = UnidadOrganizativa.Reconstitute(
            Id, "UO-001", "Unidad Test", TipoId, null, null,
            null, null, isActive: false,
            null, null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.False(unidad.IsActive);
    }

    [Fact]
    public void Reconstitute_UnidadPadreNull()
    {
        var unidad = UnidadOrganizativa.Reconstitute(
            Id, "UO-001", "Unidad Test", TipoId, null, unidadPadreId: null,
            null, null, true, null, null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.Null(unidad.UnidadPadreId);
        Assert.Null(unidad.UnidadPadre);
    }

    [Fact]
    public void Reconstitute_UnidadPadreHydrated()
    {
        var padre = new UnidadOrganizativa("PADRE", "Padre", TipoId);

        var unidad = UnidadOrganizativa.Reconstitute(
            Id, "UO-001", "Unidad Test", TipoId, null, unidadPadreId: PadreId,
            null, null, true, padre, null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.Same(padre, unidad.UnidadPadre);
    }

    [Fact]
    public void Reconstitute_VigenteDesdeHasta()
    {
        var unidad = UnidadOrganizativa.Reconstitute(
            Id, "UO-001", "Unidad Test", TipoId, null, null,
            new DateOnly(2024, 6, 1), new DateOnly(2024, 12, 31),
            true, null, null,
            DateTime.UtcNow, null, null, null, false, null, null);

        Assert.Equal(new DateOnly(2024, 6, 1), unidad.VigenteDesde);
        Assert.Equal(new DateOnly(2024, 12, 31), unidad.VigenteHasta);
    }

    [Fact]
    public void Reconstitute_VigenteHastaBeforeVigenteDesde_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() =>
            UnidadOrganizativa.Reconstitute(
                Id, "UO-001", "Unidad Test", TipoId, null, null,
                new DateOnly(2024, 12, 31), new DateOnly(2024, 1, 1),
                true, null, null,
                DateTime.UtcNow, null, null, null, false, null, null));
    }
}