using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Cobertura de la configuración EF Core de <see cref="VacanteEntity"/>
/// tras el fix de la ventana TOCTOU (issue #238). El modelo debe
/// declarar una columna generada shadow
/// <c>ActivePuestoIdUnique</c> + índice único, siguiendo el patrón
/// vigente de <c>OcupacionConfiguracion</c>.
/// </summary>
public sealed class VacanteConfiguracionTests
{
    private readonly SgvDbContext _contexto = new TestSgvDbContextFactory().CreateDbContext([]);

    [Fact]
    public void Vacante_ConfiguraShadowActivePuestoIdUniqueConFormulaCorrecta()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(VacanteEntity));

        var shadowProperty = entidad!.FindProperty("ActivePuestoIdUnique");

        Assert.NotNull(shadowProperty);

        // Validar el modelo relacional (read-optimized model) que es donde
        // Pomelo expone Collation/ComputedColumnSql para shadow properties.
        var relationalModel = _contexto.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var table = relationalModel.Tables.Single(t => t.Name == "Vacantes");
        var columna = table.Columns.Single(c => c.Name == "ActivePuestoIdUnique");

        Assert.Equal(
            "CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END",
            columna.ComputedColumnSql);
        Assert.Equal("ascii_general_ci", columna.Collation);
        Assert.True(columna.IsStored);
    }

    [Fact]
    public void Vacante_ConfiguraUniqueIndexSobreActivePuestoIdUnique()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(VacanteEntity));

        var indice = entidad!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == "ActivePuestoIdUnique"));

        Assert.NotNull(indice);
        Assert.True(indice!.IsUnique);
        Assert.Equal("IX_Vacantes_ActivePuestoIdUnique", indice.GetDatabaseName());
    }

    [Fact]
    public void Vacante_ActivePuestoIdUniqueEsPropiedadShadow()
    {
        // La propiedad debe ser shadow (PropertyInfo == null) para no
        // ensuciar la entidad de dominio Vacante con state de infra.
        var entidad = _contexto.Model.FindEntityType(typeof(VacanteEntity));

        var shadowProperty = entidad!.FindProperty("ActivePuestoIdUnique");
        Assert.NotNull(shadowProperty);
        Assert.Null(shadowProperty!.PropertyInfo);
        Assert.Null(shadowProperty.FieldInfo);
    }
}
