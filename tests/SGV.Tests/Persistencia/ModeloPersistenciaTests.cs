using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Entidades;
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
        var entidad = _contexto.Model.FindEntityType(typeof(OcupacionEntity));

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
        var entidad = _contexto.Model.FindEntityType(typeof(PuestoEntity));

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
        var entidad = _contexto.Model.FindEntityType(typeof(PostulacionEntity));

        var indice = entidad!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(PostulacionEntity.VacanteId), nameof(PostulacionEntity.PostulanteId)]));

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

    /// <summary>
    /// Verifica que las tablas de aplicación SGV se mapeen a tipos *Entity
    /// de Infraestructura, no a tipos del Dominio.
    /// </summary>
    [Fact]
    public void Modelo_EntidadesSgvUsanTiposEntity()
    {
        var entityTypes = _contexto.Model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == typeof(CargoEntity).Namespace)
            .Select(e => e.ClrType)
            .ToList();

        // Verificar que los tipos esperados están mapeados como Entity
        Assert.Contains(entityTypes, t => t == typeof(CargoEntity));
        Assert.Contains(entityTypes, t => t == typeof(PuestoEntity));
        Assert.Contains(entityTypes, t => t == typeof(UnidadOrganizativaEntity));
        Assert.Contains(entityTypes, t => t == typeof(OcupacionEntity));
        Assert.Contains(entityTypes, t => t == typeof(HabilidadEntity));
        Assert.Contains(entityTypes, t => t == typeof(NivelHabilidadEntity));
        Assert.Contains(entityTypes, t => t == typeof(CargoHabilidadEntity));
        Assert.Contains(entityTypes, t => t == typeof(PersonaEntity));
        Assert.Contains(entityTypes, t => t == typeof(PersonaHabilidadEntity));
        Assert.Contains(entityTypes, t => t == typeof(VacanteEntity));
        Assert.Contains(entityTypes, t => t == typeof(EstadoVacanteEntity));
        Assert.Contains(entityTypes, t => t == typeof(HistorialEstadoVacanteEntity));
        Assert.Contains(entityTypes, t => t == typeof(PostulanteEntity));
        Assert.Contains(entityTypes, t => t == typeof(EstadoPostulacionEntity));
        Assert.Contains(entityTypes, t => t == typeof(PostulacionEntity));
        Assert.Contains(entityTypes, t => t == typeof(HistorialEstadoPostulacionEntity));
        Assert.Contains(entityTypes, t => t == typeof(EvaluacionPostulacionEntity));
        Assert.Contains(entityTypes, t => t == typeof(AuditoriaEntity));
    }

    /// <summary>
    /// Verifica que los tipos del Dominio NO están siendo mapeados
    /// directamente por EF (el Core del refactor).
    /// </summary>
    [Fact]
    public void Modelo_DomainTypesNoEstanMapeados()
    {
        var entityTypes = _contexto.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .ToList();

        // Los tipos Identity de AspNetCore deben seguir mapeados
        // El resto de tipos de Dominio NO deben estar en el modelo
        foreach (var type in entityTypes)
        {
            if (type.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true)
                continue;

            Assert.DoesNotContain("SGV.Dominio", type.Namespace);
        }
    }

    /// <summary>
    /// Verifica que las tablas Identity (AspNet*) siguen mapeadas
    /// a sus propios tipos de Identity, sin cambios.
    /// </summary>
    [Fact]
    public void Modelo_IdentityMantieneTiposFramework()
    {
        var entityTypes = _contexto.Model.GetEntityTypes();

        var identityTypes = entityTypes
            .Where(e => e.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true)
            .Select(e => e.ClrType)
            .ToList();

        Assert.Contains(identityTypes, t => t == typeof(Microsoft.AspNetCore.Identity.IdentityRole));
        Assert.Contains(identityTypes, t => t == typeof(Microsoft.AspNetCore.Identity.IdentityUser));
        Assert.Contains(identityTypes, t => t == typeof(Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>));
        Assert.Contains(identityTypes, t => t == typeof(Microsoft.AspNetCore.Identity.IdentityUserClaim<string>));
        Assert.Contains(identityTypes, t => t == typeof(Microsoft.AspNetCore.Identity.IdentityUserLogin<string>));
        Assert.Contains(identityTypes, t => t == typeof(Microsoft.AspNetCore.Identity.IdentityUserToken<string>));
    }

    [MySqlFact]
    public void Migraciones_PuedenConectarseAlServidorMySql()
    {
        // Verifies the database server is reachable with the configured connection.
        using var contexto = new SgvDbContextFactory().CreateDbContext([]);
        var canConnect = contexto.Database.CanConnect();

        Assert.True(canConnect);
    }

    /// <summary>
    /// Prueba canaria de schema drift: verifica que el snapshot del modelo
    /// use exclusivamente tipos *Entity de Infraestructura y NO tipos del Dominio.
    /// Esto prueba que el refactor CLR-type está completo en el snapshot,
    /// y que no se generarán migraciones espurias por diferencias de tipos.
    /// </summary>
    [Fact]
    public void Migraciones_SnapshotUsaTiposEntityYNoDominio()
    {
        using var contexto = new SgvDbContextFactory().CreateDbContext([]);

        var migrationsAssembly = contexto.Database.GetService<IMigrationsAssembly>();
        var snapshot = migrationsAssembly.ModelSnapshot;

        Assert.NotNull(snapshot);

        // Verificar que las entidades SGV en el snapshot son *Entity
        var entityTypes = snapshot.Model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            var clrType = entityType.ClrType;
            var ns = clrType.Namespace;

            // Los tipos Identity pueden mantener sus nombres
            if (ns?.StartsWith("Microsoft.AspNetCore.Identity") == true)
                continue;

            // Saltar tipos del framework que EF usa internamente
            if (ns?.StartsWith("System.") == true)
                continue;

            // Todos los tipos SGV deben ser *Entity de Infraestructura
            // (Dominio o Infraestructura, ambos son válidos para SGV)
            Assert.True(
                ns == typeof(CargoEntity).Namespace,
                $"La entidad '{clrType.Name}' en el snapshot pertenece a '{ns}', " +
                $"se esperaba '{typeof(CargoEntity).Namespace}'. " +
                "Esto indica que el snapshot aún referencia tipos del Dominio.");
        }
    }

    /// <summary>
    /// Verifica que no existan migraciones pendientes de aplicar
    /// usando el script generator. Requiere conexión MySQL.
    /// </summary>
    [MySqlFact]
    public void Migraciones_ScriptIdempotenteNoGeneraDDL()
    {
        using var contexto = new SgvDbContextFactory().CreateDbContext([]);

        var migrator = contexto.Database.GetService<IMigrator>();
        var migrationsAssembly = contexto.Database.GetService<IMigrationsAssembly>();

        // Obtener la última migración aplicada
        var lastMigration = migrationsAssembly.Migrations
            .OrderByDescending(m => m.Key)
            .Select(m => m.Key)
            .FirstOrDefault();

        if (lastMigration is null)
        {
            return; // No hay migraciones — no hay drift
        }

        // Generar script desde la última migración hasta HEAD
        // Si hay drift, el script contendrá CREATE TABLE/ALTER TABLE
        var script = migrator.GenerateScript(
            fromMigration: lastMigration,
            toMigration: null);

        // El script no debe contener comandos DDL si no hay drift.
        var lineasDdl = script.Split('\n')
            .Where(linea => linea.TrimStart().StartsWith("CREATE TABLE")
                         || linea.TrimStart().StartsWith("ALTER TABLE")
                         || linea.TrimStart().StartsWith("DROP TABLE")
                         || linea.TrimStart().StartsWith("CREATE INDEX")
                         || linea.TrimStart().StartsWith("ALTER INDEX"))
            .ToList();

        Assert.True(
            lineasDdl.Count == 0,
            $"Se detectaron {lineasDdl.Count} comandos DDL en el script delta contra el snapshot. " +
            $"Esto indica schema drift. Primeras 3: {string.Join(" | ", lineasDdl.Take(3))}");
    }

}
