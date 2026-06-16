using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Model-level tests for TipoUnidadOrganizativaEntity mapping.
/// These tests use EF metadata only, not a live database.
/// </summary>
public sealed class TipoUnidadOrganizativaEntityTests
{
    private static SgvDbContext CreateEmptyContext()
    {
        var options = new DbContextOptionsBuilder<SgvDbContext>()
            .UseMySql(
                "Server=localhost;Database=sgv_test_entity;User=root;",
                ServerVersion.AutoDetect("Server=localhost;Database=sgv_test_entity;User=root;"),
                mysqlOptions => mysqlOptions.SchemaBehavior(Pomelo.EntityFrameworkCore.MySql.Infrastructure.MySqlSchemaBehavior.Ignore))
            .Options;
        return new SgvDbContext(options);
    }

    [Fact]
    public void Entity_TienePropiedadesCorrectas()
    {
        var entityType = CreateEmptyContext().Model.FindEntityType(typeof(TipoUnidadOrganizativaEntity));

        Assert.NotNull(entityType);

        var idProp = entityType!.FindProperty(nameof(TipoUnidadOrganizativaEntity.Id));
        Assert.NotNull(idProp);
        Assert.True(idProp!.IsPrimaryKey());

        var codigoProp = entityType.FindProperty(nameof(TipoUnidadOrganizativaEntity.Codigo));
        Assert.NotNull(codigoProp);
        Assert.False(codigoProp!.IsNullable);
        Assert.Equal(50, codigoProp.GetMaxLength());

        var nombreProp = entityType.FindProperty(nameof(TipoUnidadOrganizativaEntity.Nombre));
        Assert.NotNull(nombreProp);
        Assert.False(nombreProp!.IsNullable);
        Assert.Equal(100, nombreProp.GetMaxLength());
    }

    [Fact]
    public void Entity_TablaMapeadaCorrectamente()
    {
        var entityType = CreateEmptyContext().Model.FindEntityType(typeof(TipoUnidadOrganizativaEntity));

        Assert.NotNull(entityType);
        Assert.Equal("TiposUnidadOrganizativa", entityType!.GetTableName());
    }

    [Fact]
    public void Entity_NoTienePropiedadesDeAuditoria()
    {
        var entityType = CreateEmptyContext().Model.FindEntityType(typeof(TipoUnidadOrganizativaEntity));

        Assert.NotNull(entityType);

        // Should NOT have audit properties
        Assert.Null(entityType!.FindProperty("IsDeleted"));
        Assert.Null(entityType!.FindProperty("IsActive"));
        Assert.Null(entityType!.FindProperty("CreatedAt"));
        Assert.Null(entityType!.FindProperty("UpdatedAt"));
    }
}
