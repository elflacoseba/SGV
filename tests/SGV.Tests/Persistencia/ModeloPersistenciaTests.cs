using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Seleccion;
using SGV.Infraestructura.Persistencia;
using Xunit;

namespace SGV.Tests.Persistencia;

public sealed class ModeloPersistenciaTests
{
    private readonly SgvDbContext _contexto = new SgvDbContextFactory().CreateDbContext([]);

    [Fact]
    public void Proveedor_UsaPomeloMySqlYNoSqlServer()
    {
        var providerName = _contexto.Database.ProviderName;

        Assert.NotNull(providerName);
        Assert.Contains("Pomelo", providerName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlServer", providerName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Modelo_SinIndicesFiltradosEspecificosDeSqlServer()
    {
        var entityTypes = _contexto.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            foreach (var index in entityType.GetIndexes())
            {
                var filter = index.GetFilter();
                Assert.True(
                    string.IsNullOrEmpty(filter),
                    $"Entity '{entityType.ClrType.Name}' has index with SQL Server filter: '{filter}'. MySQL does not support filtered indexes.");
            }
        }
    }

    [Fact]
    public void Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(Ocupacion));

        var generatedProperty = entidad!.FindProperty("ActivePuestoIdUnique");
        Assert.NotNull(generatedProperty);

        var computedSql = generatedProperty!.GetComputedColumnSql();
        Assert.NotNull(computedSql);
        Assert.Contains("FechaFin", computedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsDeleted", computedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PuestoId", computedSql, StringComparison.OrdinalIgnoreCase);

        var index = entidad.GetIndexes()
            .Single(i => i.Properties.Any(p => p.Name == "ActivePuestoIdUnique"));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Modelo_ConfiguraColumnaGeneradaUnicaParaCodigoPuestoActivo()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(Puesto));

        var generatedProperty = entidad!.FindProperty("ActiveCodigoUnique");
        Assert.NotNull(generatedProperty);

        var computedSql = generatedProperty!.GetComputedColumnSql();
        Assert.NotNull(computedSql);
        Assert.Contains("IsDeleted", computedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Codigo", computedSql, StringComparison.OrdinalIgnoreCase);

        var index = entidad.GetIndexes()
            .Single(i => i.Properties.Any(p => p.Name == "ActiveCodigoUnique"));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Modelo_ConfiguraPostulacionUnicaPorVacanteYPostulante()
    {
        var entidad = _contexto.Model.FindEntityType(typeof(Postulacion));

        var indice = entidad!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(Postulacion.VacanteId), nameof(Postulacion.PostulanteId)]));

        Assert.True(indice.IsUnique);
    }

    [Fact]
    public void Modelo_CheckConstraintsUsanSintaxisMySql()
    {
        var designModel = _contexto.GetService<IDesignTimeModel>().Model;
        var entityTypes = designModel.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            var tableChecks = entityType.GetCheckConstraints();
            foreach (var check in tableChecks)
            {
                var sql = check.Sql;
                Assert.DoesNotContain("[", sql);
                Assert.DoesNotContain("]", sql);
            }
        }
    }

    [Fact]
    public void Modelo_GeneraScriptCompatibleConMySql()
    {
        var relationalModel = _contexto.Model.GetRelationalModel();

        Assert.NotNull(relationalModel);

        var tables = relationalModel.Tables.ToList();
        Assert.Contains(tables, t => t.Name == "Cargos");
        Assert.Contains(tables, t => t.Name == "Puestos");
        Assert.Contains(tables, t => t.Name == "Ocupaciones");
        Assert.Contains(tables, t => t.Name == "Postulantes");
    }

    [Fact]
    public void Modelo_ProductVersionEsEfCore9x()
    {
        var productVersion = _contexto.Model.GetProductVersion();

        Assert.StartsWith("9.", productVersion);
    }

    [Fact]
    public void Modelo_EntidadesDeAplicacionUsanClavesGuid()
    {
        var entityTypes = _contexto.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            // Skip Identity tables (AspNet*) which use string keys
            if (entityType.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true)
                continue;

            var pk = entityType.FindPrimaryKey();
            if (pk is null) continue;

            foreach (var property in pk.Properties)
            {
                Assert.True(
                    property.ClrType == typeof(Guid),
                    $"Entity '{entityType.ClrType.Name}' PK property '{property.Name}' " +
                    $"is {property.ClrType.Name}, expected Guid.");
            }
        }
    }

    [Fact]
    public void Migraciones_ContienenClasesDeMigracionValidas()
    {
        var infraAssembly = typeof(SgvDbContext).Assembly;

        var migrationTypes = infraAssembly.GetTypes()
            .Where(t => t.IsPublic
                && !t.IsAbstract
                && t.BaseType == typeof(Migration))
            .ToList();

        Assert.NotEmpty(migrationTypes);

        foreach (var migrationType in migrationTypes)
        {
            var instance = (Migration)Activator.CreateInstance(migrationType)!;
            Assert.NotNull(instance.UpOperations);
            Assert.NotNull(instance.DownOperations);
        }
    }

    [MySqlFact]
    public void Migraciones_PuedenConectarseAlServidorMySql()
    {
        // Verifies the database server is reachable with the configured connection.
        using var contexto = new SgvDbContextFactory().CreateDbContext([]);
        var canConnect = contexto.Database.CanConnect();

        Assert.True(canConnect);
    }
}
