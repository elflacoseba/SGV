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
/// Verifica el comportamiento esperado de la migración D7:
///
/// (1) Preflight fail-loud si hay duplicados activos en PersonaId.
/// (2) Backfill IsDeleted=1 → LockoutEnd futuro en datetime(6) antes
///     de dropear la columna IsDeleted.
/// (3) Down() lanza NotSupportedException (forward-only).
///
/// Los tests inspeccionan las operaciones SQL generadas por la migración
/// sin necesidad de MySQL real: suficiente para validar el contrato.
/// </summary>
public sealed class DropSoftDeleteMigracionTests
{
    [Fact]
    public void DropSoftDeleteMigracion_PreflightDuplicadosPersonaId_IsPresent()
    {
        var sql = InvokeMigrationUpSql();

        Assert.Contains("PersonaId", sql, StringComparison.Ordinal);
        Assert.Contains("DUPLICATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("45000", sql, StringComparison.Ordinal);
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
        var sql = InvokeMigrationUpSql();

        // El orden importa: primero FK (metadata-only), después índices
        // generados, después la columna generada + IsDeleted, después
        // swap del índice PersonaId.
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
    public void DropSoftDeleteMigracion_RestoresUniqueIndexOnPersonaId()
    {
        var sql = InvokeMigrationUpSql();

        Assert.Contains("DROP INDEX `IX_AspNetUsers_PersonaId`", sql, StringComparison.Ordinal);
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
    public void DropSoftDeleteMigracion_IsIdempotent_GuardedByInformationSchema()
    {
        // RES-001 (4R review): un segundo run de la migración no debe
        // romper. La idempotencia se implementa con un stored procedure
        // gated por information_schema.COLUMNS (IsDeleted presente).
        var sql = InvokeMigrationUpSql();

        Assert.Contains("information_schema.COLUMNS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__sgvApplyD7", sql, StringComparison.Ordinal);
        Assert.Contains("DROP PROCEDURE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CALL __sgvApplyD7", sql, StringComparison.Ordinal);
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