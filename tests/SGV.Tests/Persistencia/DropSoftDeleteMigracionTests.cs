using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;
using SGV.Infraestructura.Persistencia;
using SGV.Infraestructura.Persistencia.Migraciones;
using SGV.Infraestructura.Seguridad;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Tests para el preflight fail-loud y backfill de la migración que
/// quita el soft-delete de usuarios (drop IsDeleted + columnas generadas).
/// Verifica el comportamiento esperado de la migración D7 (#263):
///
/// (1) Preflight fail-loud si hay duplicados activos en PersonaId — ahora
///     modelado como <c>ADD UNIQUE INDEX</c> temporal en lugar del
///     <c>SIGNAL SQLSTATE</c> custom (MySQL rechaza stored procedures
///     anidados con ERROR 1357 en el wrapper --idempotent).
/// (2) Backfill IsDeleted=1 → LockoutEnd futuro en datetime(6) antes
///     de dropear la columna IsDeleted.
/// (3) Down() lanza NotSupportedException (forward-only).
/// (4) Reentrancia: <c>@needsD7</c> booleano por information_schema
///     + PREPARE/EXECUTE evita que un segundo run contra un schema
///     post-D7 produzca errores.
///
/// Los tests inspeccionan las operaciones SQL generadas por la migración
/// sin necesidad de MySQL real: suficiente para validar el contrato.
/// </summary>
public sealed class DropSoftDeleteMigracionTests
{
    [Fact]
    public void DropSoftDeleteMigracion_PreflightUniqueIndexTemporal_PresenteAntesDeOperacionesDestructivas()
    {
        // (#263) El preflight fail-loud ahora es un ADD UNIQUE INDEX
        // temporal (__sgvD7_PreflightUnique) sobre PersonaId. Si hay
        // duplicados activos, MySQL aborta con ERROR 1062 antes de
        // cualquier mutación destructiva. Esta guarda fija la
        // posición: el ADD UNIQUE INDEX preflight debe aparecer
        // textual antes de cada mutación destructiva. Si alguien
        // mueve la barrera hacia abajo del UPDATE/DROP/CREATE
        // canónico, este test falla con el offset del primer
        // marcador violado.
        var sql = InvokeMigrationUpSql();

        var preflightIdx = sql.IndexOf("ADD UNIQUE INDEX `__sgvD7_PreflightUnique`", StringComparison.Ordinal);
        Assert.True(preflightIdx >= 0, "Debe existir ADD UNIQUE INDEX __sgvD7_PreflightUnique");

        var markersAfter = new (string Description, string Token)[]
        {
            ("UPDATE AspNetUsers", "UPDATE `AspNetUsers`"),
            ("DROP FOREIGN KEY", "DROP FOREIGN KEY"),
            ("DROP INDEX ActiveUserNameUnique", "DROP INDEX `IX_AspNetUsers_ActiveUserNameUnique`"),
            ("DROP INDEX ActivePersonaIdUnique", "DROP INDEX `IX_AspNetUsers_ActivePersonaIdUnique`"),
            ("DROP COLUMN", "DROP COLUMN `IsDeleted`"),
            ("DROP INDEX PersonaId no-único", "DROP INDEX `IX_AspNetUsers_PersonaId`"),
            ("ADD UNIQUE INDEX PersonaId canónico", "ADD UNIQUE INDEX `IX_AspNetUsers_PersonaId`"),
            ("ADD CONSTRAINT FK", "ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId`"),
        };

        foreach (var (description, token) in markersAfter)
        {
            var idx = sql.IndexOf(token, StringComparison.Ordinal);
            Assert.True(
                idx >= 0,
                $"Debe existir {description} ({token}).");
            Assert.True(
                preflightIdx < idx,
                $"Preflight __sgvD7_PreflightUnique (offset {preflightIdx}) debe preceder "
              + $"a {description} (offset {idx}). Si la barrera se movió después de la mutación, "
              + $"el fail-loud natural deja de proteger el script.");
        }

        // El DROP del temporal también debe ocurrir antes del ADD canónico
        // (paso 8 antes de paso 9) y antes de la FK final.
        var preflightDropIdx = sql.IndexOf("DROP INDEX `__sgvD7_PreflightUnique`", StringComparison.Ordinal);
        var canonicalAddIdx = sql.IndexOf("ADD UNIQUE INDEX `IX_AspNetUsers_PersonaId`", StringComparison.Ordinal);
        Assert.True(preflightDropIdx >= 0, "Debe existir DROP INDEX __sgvD7_PreflightUnique");
        Assert.True(canonicalAddIdx > preflightDropIdx,
            "DROP del temporal debe preceder al ADD canónico (paso 8 antes de paso 9).");
    }

    [Fact]
    public void DropSoftDeleteMigracion_BackfillIsDeletedToLockoutEnd_IsPresent()
    {
        var sql = InvokeMigrationUpSql();

        // El backfill debe poblar LockoutEnd a un valor futuro y activar
        // LockoutEnabled antes de dropear IsDeleted.
        Assert.Contains("`IsDeleted` = 1", sql, StringComparison.Ordinal);
        Assert.Contains("`LockoutEnd`", sql, StringComparison.Ordinal);
        Assert.Contains("`LockoutEnabled`", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void DropSoftDeleteMigracion_DropsGeneratedColumnsInCorrectOrder()
    {
        // El orden importa: primero FK (metadata-only), después índices
        // generados, después la columna generada + IsDeleted, después
        // swap del índice PersonaId.
        var sql = InvokeMigrationUpSql();

        var fkIdx = sql.IndexOf("DROP FOREIGN KEY", StringComparison.Ordinal);
        var activePersonaIdIdxIdx = sql.IndexOf(
            "DROP INDEX `IX_AspNetUsers_ActivePersonaIdUnique`",
            StringComparison.Ordinal);
        var activeUserNameIdxIdx = sql.IndexOf(
            "DROP INDEX `IX_AspNetUsers_ActiveUserNameUnique`",
            StringComparison.Ordinal);
        var isDeletedDropIdx = sql.IndexOf("DROP COLUMN `IsDeleted`", StringComparison.Ordinal);
        var personaIdReaddIdx = sql.IndexOf(
            "ADD UNIQUE INDEX `IX_AspNetUsers_PersonaId`",
            StringComparison.Ordinal);

        Assert.True(fkIdx > 0, "Debe existir DROP FOREIGN KEY");
        Assert.True(activePersonaIdIdxIdx > 0, "Debe existir DROP INDEX ActivePersonaIdUnique");
        Assert.True(activeUserNameIdxIdx > 0, "Debe existir DROP INDEX ActiveUserNameUnique");
        Assert.True(isDeletedDropIdx > 0, "Debe existir DROP COLUMN IsDeleted");
        Assert.True(personaIdReaddIdx > 0, "Debe existir ADD UNIQUE INDEX PersonaId");

        Assert.True(fkIdx < activePersonaIdIdxIdx,
            "FK drop debe preceder al drop del índice generado que la reemplazaba");
        Assert.True(activePersonaIdIdxIdx < isDeletedDropIdx,
            "Drop índice generado debe preceder al drop de columna");
        Assert.True(isDeletedDropIdx < personaIdReaddIdx,
            "Re-add índice PersonaId UNIQUE debe ir después del drop de IsDeleted");
    }

    [Fact]
    public void DropSoftDeleteMigracion_DropsThenAddsUniqueIndexOnPersonaId()
    {
        // (#263) La migración ahora añade un índice UNIQUE temporal
        // (preflight) y luego dropea tanto el índice PersonaId no-único
        // preexistente como el preflight temporal antes de añadir el
        // canónico UNIQUE.
        var sql = InvokeMigrationUpSql();

        Assert.Contains("DROP INDEX `IX_AspNetUsers_PersonaId`", sql, StringComparison.Ordinal);
        Assert.Contains("DROP INDEX `__sgvD7_PreflightUnique`", sql, StringComparison.Ordinal);
        Assert.Contains("ADD UNIQUE INDEX `IX_AspNetUsers_PersonaId`", sql, StringComparison.Ordinal);
        Assert.Contains("ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId`", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void DropSoftDeleteMigracion_DownIsForwardOnly()
    {
        var migration = CreateMigration();
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        var exception = Assert.Throws<TargetInvocationException>(
            () => InvokeMigrationMethod(migration, "Down", builder));

        Assert.IsType<NotSupportedException>(exception.InnerException);
    }

    [Fact]
    public void DropSoftDeleteMigracion_Reentrancia_GatedPorInformationSchemaConPrepare()
    {
        // (#263) Reentrancia sin stored procedure: variable @needsD7 seteada
        // desde information_schema.COLUMNS (IsDeleted presente) y cada
        // paso gated por IF(@needsD7 > 0, '...', 'DO 0') vía PREPARE/EXECUTE.
        var sql = InvokeMigrationUpSql();

        Assert.Contains("information_schema.COLUMNS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@needsD7", sql, StringComparison.Ordinal);
        Assert.Contains("PREPARE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEALLOCATE PREPARE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DropSoftDeleteMigracion_NoContieneProceduresAnidados()
    {
        // (#263) El script --idempotent de EF envuelve cada migración en
        // un procedure MigrationsScript; CREATE/DROP PROCEDURE anidado
        // produce ERROR 1357. La migración NO debe contener un procedure
        // interno propio: usamos SQL directo con PREPARE/EXECUTE.
        var sql = InvokeMigrationUpSql();

        Assert.DoesNotContain("CREATE PROCEDURE __sgvApplyD7", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP PROCEDURE IF EXISTS __sgvApplyD7", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CALL __sgvApplyD7", sql, StringComparison.Ordinal);
    }

    private static string InvokeMigrationUpSql()
    {
        var migration = CreateMigration();
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        InvokeMigrationMethod(migration, "Up", builder);
        return string.Join(
            "\n",
            builder.Operations
                .OfType<SqlOperation>()
                .Select(op => op.Sql));
    }

    private static Migration CreateMigration()
    {
        // Resolución por convención: la migración forward-only
        // implementada en 1.10 debe llamarse DropSoftDeleteFromAspNetUsers
        // (timestamp: cualquier prefijo 20260715xxx). Usamos reflexión
        // para no atar el test al nombre exacto del archivo.
        var assembly = typeof(SgvDbContext).Assembly;
        var migrationType = assembly
            .GetTypes()
            .FirstOrDefault(t => typeof(Migration).IsAssignableFrom(t)
                && t.Name.StartsWith("DropSoftDelete", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "No se encontró la migración DropSoftDeleteFromAspNetUsers en " +
                assembly.GetName().Name);
        return (Migration)Activator.CreateInstance(migrationType)!;
    }

    private static void InvokeMigrationMethod(
        Migration migration,
        string methodName,
        MigrationBuilder builder)
    {
        var method = migration.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(migration, [builder]);
    }
}