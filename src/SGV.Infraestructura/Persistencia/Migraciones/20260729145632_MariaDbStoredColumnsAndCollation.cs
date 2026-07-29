using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    /// <summary>
    /// Compatibilidad MariaDB para el repositorio SGV. Tres problemas
    /// detectados al apuntar contra MariaDB (en lugar de MySQL 8):
    ///
    /// 1. <c>utf8mb4_0900_ai_ci</c> no existe en MariaDB (collation exclusiva
    ///    de MySQL 8). Se sustituye por <c>utf8mb4_unicode_ci</c> (default
    ///    MariaDB, UCA 5.2.0).
    ///
    /// 2. MariaDB NO indexa columnas generadas VIRTUALES; sólo STORED. El
    ///    patrón de unicidad "soft-delete" empleado por el repo
    ///    (<c>ActiveXxxUnique</c>) requiere STORED para que el UNIQUE INDEX
    ///    funcione. MySQL 8 también acepta STORED, así que el cambio no
    ///    rompe backward-compat con la suite <c>[MySqlFact]</c>.
    ///
    /// 3. MySQL 8 rechaza <c>ALTER COLUMN</c> cuando el único cambio es el
    ///    flag STORED/VIRTUAL (error "Changing the STORED status is not
    ///    supported for generated columns"). Por eso la transformación
    ///    VIRTUAL → STORED exige el patrón explícito: drop index → drop
    ///    column → add column (STORED) → create unique index. La columna
    ///    se recomputa determinísticamente desde sus columnas fuente, así
    ///    que NO se pierden datos: el índice se cae y se vuelve a crear,
    ///    pero las columnas <c>Codigo</c>, <c>Legajo</c>, <c>Email</c>,
    ///    <c>NumeroDocumento</c>, <c>TipoDocumentoId</c>, <c>PersonaId</c>,
    ///    <c>PuestoId</c> permanecen intactas.
    ///
    /// Columnas afectadas (10, todas las que antes eran VIRTUAL en el modelo):
    ///   - Cargos.ActiveCodigoUnique
    ///   - Habilidades.ActiveCodigoUnique
    ///   - UnidadesOrganizativas.ActiveCodigoUnique
    ///   - Puestos.ActiveCodigoUnique
    ///   - Personas.ActiveLegajoUnique
    ///   - Personas.ActiveEmailUnique
    ///   - Personas.ActiveDocumentoUnique (+ cambio de collation)
    ///   - Postulantes.ActivePersonaIdUnique
    ///   - Ocupaciones.ActivePuestoIdUnique
    ///   - Ocupaciones.ActivePersonaPuestoUnique
    ///
    /// NO se toca <c>AspNetUsers.ActiveUserNameUnique</c> — esa columna ya
    /// era STORED desde la migración archivada
    /// <c>2026-07-15-quita-soft-delete-usuario</c>.
    ///
    /// <c>Down()</c> revierte el patrón simétrico (drop index → drop column
    /// → add column VIRTUAL → create index), restaurando además
    /// <c>utf8mb4_0900_ai_ci</c> en <c>Personas.ActiveDocumentoUnique</c>.
    ///
    /// IMPORTANTE: este <c>Down()</c> asume un servidor MySQL 8.
    /// <c>utf8mb4_0900_ai_ci</c> es exclusiva de MySQL 8 y MariaDB la
    /// rechaza con <c>Unknown collation</c>, por lo que revertir
    /// <c>MariaDbStoredColumnsAndCollation</c> contra MariaDB fallará.
    /// Para MariaDB-definitivo, mantener esta migración aplicada y no
    /// revertir.
    /// </summary>
    public partial class MariaDbStoredColumnsAndCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =================================================================
            // UnidadesOrganizativas.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_UnidadesOrganizativas_ActiveCodigoUnique",
                table: "UnidadesOrganizativas");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "UnidadesOrganizativas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "UnidadesOrganizativas",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesOrganizativas_ActiveCodigoUnique",
                table: "UnidadesOrganizativas",
                column: "ActiveCodigoUnique",
                unique: true);

            // =================================================================
            // Puestos.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Puestos_ActiveCodigoUnique",
                table: "Puestos");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "Puestos");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "Puestos",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_ActiveCodigoUnique",
                table: "Puestos",
                column: "ActiveCodigoUnique",
                unique: true);

            // =================================================================
            // Postulantes.ActivePersonaIdUnique (Guid → char(36))
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Postulantes_ActivePersonaIdUnique",
                table: "Postulantes");

            migrationBuilder.DropColumn(
                name: "ActivePersonaIdUnique",
                table: "Postulantes");

            migrationBuilder.AddColumn<Guid>(
                name: "ActivePersonaIdUnique",
                table: "Postulantes",
                type: "char(36)",
                nullable: true,
                computedColumnSql: "CASE WHEN `PersonaId` IS NOT NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END",
                stored: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Postulantes_ActivePersonaIdUnique",
                table: "Postulantes",
                column: "ActivePersonaIdUnique",
                unique: true);

            // =================================================================
            // Personas.ActiveLegajoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Personas_ActiveLegajoUnique",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ActiveLegajoUnique",
                table: "Personas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveLegajoUnique",
                table: "Personas",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `Legajo` IS NOT NULL AND `IsDeleted` = 0 THEN `Legajo` ELSE NULL END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveLegajoUnique",
                table: "Personas",
                column: "ActiveLegajoUnique",
                unique: true);

            // =================================================================
            // Personas.ActiveEmailUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Personas_ActiveEmailUnique",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ActiveEmailUnique",
                table: "Personas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveEmailUnique",
                table: "Personas",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `Email` IS NOT NULL AND `IsDeleted` = 0 THEN `Email` ELSE NULL END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveEmailUnique",
                table: "Personas",
                column: "ActiveEmailUnique",
                unique: true);

            // =================================================================
            // Personas.ActiveDocumentoUnique (+ cambio de collation)
            // utf8mb4_0900_ai_ci → utf8mb4_unicode_ci (no existe en MariaDB)
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Personas_ActiveDocumentoUnique",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ActiveDocumentoUnique",
                table: "Personas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveDocumentoUnique",
                table: "Personas",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true,
                computedColumnSql: "CASE WHEN `TipoDocumentoId` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(`TipoDocumentoId`, ':', `NumeroDocumento`) ELSE NULL END",
                stored: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveDocumentoUnique",
                table: "Personas",
                column: "ActiveDocumentoUnique",
                unique: true);

            // =================================================================
            // Ocupaciones.ActivePuestoIdUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_ActivePuestoIdUnique",
                table: "Ocupaciones");

            migrationBuilder.DropColumn(
                name: "ActivePuestoIdUnique",
                table: "Ocupaciones");

            migrationBuilder.AddColumn<string>(
                name: "ActivePuestoIdUnique",
                table: "Ocupaciones",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true,
                computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END",
                stored: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePuestoIdUnique",
                table: "Ocupaciones",
                column: "ActivePuestoIdUnique",
                unique: true);

            // =================================================================
            // Ocupaciones.ActivePersonaPuestoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_ActivePersonaPuestoUnique",
                table: "Ocupaciones");

            migrationBuilder.DropColumn(
                name: "ActivePersonaPuestoUnique",
                table: "Ocupaciones");

            migrationBuilder.AddColumn<string>(
                name: "ActivePersonaPuestoUnique",
                table: "Ocupaciones",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN CONCAT(`PersonaId`, ':', `PuestoId`) ELSE NULL END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePersonaPuestoUnique",
                table: "Ocupaciones",
                column: "ActivePersonaPuestoUnique",
                unique: true);

            // =================================================================
            // Habilidades.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Habilidades_ActiveCodigoUnique",
                table: "Habilidades");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "Habilidades");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "Habilidades",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Habilidades_ActiveCodigoUnique",
                table: "Habilidades",
                column: "ActiveCodigoUnique",
                unique: true);

            // =================================================================
            // Cargos.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Cargos_ActiveCodigoUnique",
                table: "Cargos");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "Cargos");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "Cargos",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_ActiveCodigoUnique",
                table: "Cargos",
                column: "ActiveCodigoUnique",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // =================================================================
            // UnidadesOrganizativas.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_UnidadesOrganizativas_ActiveCodigoUnique",
                table: "UnidadesOrganizativas");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "UnidadesOrganizativas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "UnidadesOrganizativas",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesOrganizativas_ActiveCodigoUnique",
                table: "UnidadesOrganizativas",
                column: "ActiveCodigoUnique",
                unique: true);

            // =================================================================
            // Puestos.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Puestos_ActiveCodigoUnique",
                table: "Puestos");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "Puestos");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "Puestos",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Puestos_ActiveCodigoUnique",
                table: "Puestos",
                column: "ActiveCodigoUnique",
                unique: true);

            // =================================================================
            // Postulantes.ActivePersonaIdUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Postulantes_ActivePersonaIdUnique",
                table: "Postulantes");

            migrationBuilder.DropColumn(
                name: "ActivePersonaIdUnique",
                table: "Postulantes");

            migrationBuilder.AddColumn<Guid>(
                name: "ActivePersonaIdUnique",
                table: "Postulantes",
                type: "char(36)",
                nullable: true,
                computedColumnSql: "CASE WHEN `PersonaId` IS NOT NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END",
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Postulantes_ActivePersonaIdUnique",
                table: "Postulantes",
                column: "ActivePersonaIdUnique",
                unique: true);

            // =================================================================
            // Personas.ActiveLegajoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Personas_ActiveLegajoUnique",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ActiveLegajoUnique",
                table: "Personas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveLegajoUnique",
                table: "Personas",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `Legajo` IS NOT NULL AND `IsDeleted` = 0 THEN `Legajo` ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveLegajoUnique",
                table: "Personas",
                column: "ActiveLegajoUnique",
                unique: true);

            // =================================================================
            // Personas.ActiveEmailUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Personas_ActiveEmailUnique",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ActiveEmailUnique",
                table: "Personas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveEmailUnique",
                table: "Personas",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `Email` IS NOT NULL AND `IsDeleted` = 0 THEN `Email` ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveEmailUnique",
                table: "Personas",
                column: "ActiveEmailUnique",
                unique: true);

            // =================================================================
            // Personas.ActiveDocumentoUnique (collation revertida)
            // utf8mb4_unicode_ci → utf8mb4_0900_ai_ci
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Personas_ActiveDocumentoUnique",
                table: "Personas");

            migrationBuilder.DropColumn(
                name: "ActiveDocumentoUnique",
                table: "Personas");

            migrationBuilder.AddColumn<string>(
                name: "ActiveDocumentoUnique",
                table: "Personas",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true,
                computedColumnSql: "CASE WHEN `TipoDocumentoId` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(`TipoDocumentoId`, ':', `NumeroDocumento`) ELSE NULL END",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveDocumentoUnique",
                table: "Personas",
                column: "ActiveDocumentoUnique",
                unique: true);

            // =================================================================
            // Ocupaciones.ActivePuestoIdUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_ActivePuestoIdUnique",
                table: "Ocupaciones");

            migrationBuilder.DropColumn(
                name: "ActivePuestoIdUnique",
                table: "Ocupaciones");

            migrationBuilder.AddColumn<string>(
                name: "ActivePuestoIdUnique",
                table: "Ocupaciones",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true,
                computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END",
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePuestoIdUnique",
                table: "Ocupaciones",
                column: "ActivePuestoIdUnique",
                unique: true);

            // =================================================================
            // Ocupaciones.ActivePersonaPuestoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_ActivePersonaPuestoUnique",
                table: "Ocupaciones");

            migrationBuilder.DropColumn(
                name: "ActivePersonaPuestoUnique",
                table: "Ocupaciones");

            migrationBuilder.AddColumn<string>(
                name: "ActivePersonaPuestoUnique",
                table: "Ocupaciones",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN CONCAT(`PersonaId`, ':', `PuestoId`) ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePersonaPuestoUnique",
                table: "Ocupaciones",
                column: "ActivePersonaPuestoUnique",
                unique: true);

            // =================================================================
            // Habilidades.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Habilidades_ActiveCodigoUnique",
                table: "Habilidades");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "Habilidades");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "Habilidades",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Habilidades_ActiveCodigoUnique",
                table: "Habilidades",
                column: "ActiveCodigoUnique",
                unique: true);

            // =================================================================
            // Cargos.ActiveCodigoUnique
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Cargos_ActiveCodigoUnique",
                table: "Cargos");

            migrationBuilder.DropColumn(
                name: "ActiveCodigoUnique",
                table: "Cargos");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCodigoUnique",
                table: "Cargos",
                type: "varchar(255)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Cargos_ActiveCodigoUnique",
                table: "Cargos",
                column: "ActiveCodigoUnique",
                unique: true);
        }
    }
}