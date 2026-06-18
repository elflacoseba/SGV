using Microsoft.EntityFrameworkCore;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Model-level tests for NivelCargoEntity mapping.
/// These tests use EF metadata only, not a live database.
/// </summary>
public sealed class NivelCargoEntityTests
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
        var entityType = CreateEmptyContext().Model.FindEntityType(typeof(NivelCargoEntity));

        Assert.NotNull(entityType);

        var idProp = entityType!.FindProperty(nameof(NivelCargoEntity.Id));
        Assert.NotNull(idProp);
        Assert.True(idProp!.IsPrimaryKey());

        var codigoProp = entityType.FindProperty(nameof(NivelCargoEntity.Codigo));
        Assert.NotNull(codigoProp);
        Assert.False(codigoProp!.IsNullable);
        Assert.Equal(50, codigoProp.GetMaxLength());

        var nombreProp = entityType.FindProperty(nameof(NivelCargoEntity.Nombre));
        Assert.NotNull(nombreProp);
        Assert.False(nombreProp!.IsNullable);
        Assert.Equal(100, nombreProp.GetMaxLength());

        var valorNumericoProp = entityType.FindProperty(nameof(NivelCargoEntity.ValorNumerico));
        Assert.NotNull(valorNumericoProp);
        Assert.False(valorNumericoProp!.IsNullable);

        var ordenProp = entityType.FindProperty(nameof(NivelCargoEntity.Orden));
        Assert.NotNull(ordenProp);
        Assert.False(ordenProp!.IsNullable);
    }

    [Fact]
    public void Entity_TablaMapeadaCorrectamente()
    {
        var entityType = CreateEmptyContext().Model.FindEntityType(typeof(NivelCargoEntity));

        Assert.NotNull(entityType);
        Assert.Equal("NivelesCargo", entityType!.GetTableName());
    }

    [Fact]
    public void Entity_NoTienePropiedadesDeAuditoria()
    {
        var entityType = CreateEmptyContext().Model.FindEntityType(typeof(NivelCargoEntity));

        Assert.NotNull(entityType);

        // Should NOT have audit properties
        Assert.Null(entityType!.FindProperty("IsDeleted"));
        Assert.Null(entityType!.FindProperty("IsActive"));
        Assert.Null(entityType!.FindProperty("CreatedAt"));
        Assert.Null(entityType!.FindProperty("UpdatedAt"));
    }
}
