using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests for the modified UnidadOrganizativaEntity with TipoUnidadOrganizativaId.
/// </summary>
public sealed class UnidadOrganizativaEntityModificationTests
{
    [Fact]
    public void Entity_TieneTipoUnidadOrganizativaId()
    {
        var entity = new UnidadOrganizativaEntity();

        // The property should exist and be a non-nullable Guid
        var prop = typeof(UnidadOrganizativaEntity).GetProperty(nameof(UnidadOrganizativaEntity.TipoUnidadOrganizativaId));
        Assert.NotNull(prop);
        Assert.Equal(typeof(Guid), prop!.PropertyType);
        Assert.False(prop.PropertyType.IsGenericType); // Not Nullable<Guid>
    }

    [Fact]
    public void Entity_TieneNavPropertyTipoUnidadOrganizativa()
    {
        var entity = new UnidadOrganizativaEntity();

        var navProp = typeof(UnidadOrganizativaEntity).GetProperty(nameof(UnidadOrganizativaEntity.TipoUnidadOrganizativa));
        Assert.NotNull(navProp);
        Assert.Equal(typeof(TipoUnidadOrganizativaEntity), navProp!.PropertyType);
        Assert.True(navProp.PropertyType.IsClass);
        Assert.True(navProp.CanWrite);
    }

    [Fact]
    public void Entity_NoTienePropiedadTipoUnidad()
    {
        var prop = typeof(UnidadOrganizativaEntity).GetProperty("TipoUnidad");
        Assert.Null(prop);
    }
}
