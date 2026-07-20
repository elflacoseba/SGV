using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SGV.Infraestructura.Persistencia.Catalogos;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    /// <summary>
    /// Issue #147: introduce el catálogo <c>TipoDocumento</c> (4 filas seed
    /// en el bloque <c>71000000-…</c>) y reemplaza <c>Personas.TipoDocumento</c>
    /// (string) por <c>Personas.TipoDocumentoId</c> (FK <c>char(36) NULL</c>)
    /// con <c>OnDelete(Restrict)</c>. La columna generada
    /// <c>ActiveDocumentoUnique</c> se reconstruye con la fórmula
    /// <c>CONCAT(TipoDocumentoId, ':', NumeroDocumento)</c>.
    ///
    /// Backfill con la variante **opt-in relajada** de la condición #3 de
    /// <c>REQ-SPA-EVOLUTION-001</c> (ver
    /// <c>openspec/specs/sgv-persistence-architecture/spec.md</c>): los
    /// valores legacy de <c>TipoDocumento</c> que NO matcheen ningún
    /// <c>Codigo</c> del seed quedan con <c>TipoDocumentoId = NULL</c> y
    /// <c>NumeroDocumento</c> preservado (huérfano remediación post-deploy).
    ///
    /// Forward-only: <c>Down()</c> tira <c>NotSupportedException</c> como
    /// primera línea. Revertir requiere una migración correctiva explícita
    /// (precedente <c>FixActivePuestoIdUniqueType</c>).
    /// </summary>
    public partial class TipoDocumentoCatalogoYPersonaFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =================================================================
            // STEP 1: Crear la tabla TiposDocumento con su índice único en Codigo
            //         y sus 2 check constraints.
            // =================================================================
            migrationBuilder.CreateTable(
                name: "TiposDocumento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PatronValidacion = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LongitudMinima = table.Column<int>(type: "int", nullable: true),
                    LongitudMaxima = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposDocumento", x => x.Id);
                    table.CheckConstraint("CK_TiposDocumento_Codigo", "`Codigo` <> ''");
                    table.CheckConstraint(
                        "CK_TiposDocumento_Longitudes",
                        "`LongitudMinima` IS NULL OR `LongitudMaxima` IS NULL OR `LongitudMinima` <= `LongitudMaxima`");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // =================================================================
            // STEP 2: InsertData de los 4 seeds desde TipoDocumentoConstantes
            //         (misma source of truth que DatosSemilla.HasData).
            // =================================================================
            var semillas = TipoDocumentoConstantes.Semilla;
            var values = new object[semillas.Count, 6];
            for (var i = 0; i < semillas.Count; i++)
            {
                var s = semillas[i];
                values[i, 0] = s.Id;
                values[i, 1] = s.Codigo;
                values[i, 2] = s.Nombre;
                values[i, 3] = s.PatronValidacion;
                values[i, 4] = s.LongitudMinima;
                values[i, 5] = s.LongitudMaxima;
            }
            migrationBuilder.InsertData(
                table: "TiposDocumento",
                columns: new[] { "Id", "Codigo", "Nombre", "PatronValidacion", "LongitudMinima", "LongitudMaxima" },
                values: values);

            migrationBuilder.CreateIndex(
                name: "IX_TiposDocumento_Codigo",
                table: "TiposDocumento",
                column: "Codigo",
                unique: true);

            // =================================================================
            // STEP 3: Pre-flight NO fail-loud. Lista los valores legacy sucios
            //         (los que no matchean ningún Codigo del seed) para logging
            //         operativo. No aborta — la política es opt-in relajada.
            // =================================================================
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE IF NOT EXISTS _DirtyTipoDocumento AS
                SELECT DISTINCT p.TipoDocumento
                FROM Personas p
                WHERE p.TipoDocumento IS NOT NULL
                  AND p.TipoDocumento NOT IN ('DNI', 'LE', 'LC', 'Pasaporte');

                SET @dirtyCount = (SELECT COUNT(*) FROM _DirtyTipoDocumento);
                SET @dirtyExamples = (
                    SELECT COALESCE(GROUP_CONCAT(DISTINCT TipoDocumento SEPARATOR ', '), 'ninguno')
                    FROM (SELECT TipoDocumento FROM _DirtyTipoDocumento LIMIT 5) AS d
                );

                -- Log diagnóstico (no aborta). Se puede inspeccionar vía
                -- SHOW ENGINE INNODB STATUS o capturando el output del
                -- EF migration.
                SELECT CONCAT('Backfill opt-in relajado: ', @dirtyCount,
                              ' valores de TipoDocumento sin catalogar. Ejemplos: ',
                              @dirtyExamples) AS _backfill_diagnostics;

                DROP TEMPORARY TABLE IF EXISTS _DirtyTipoDocumento;
            ");

            // =================================================================
            // STEP 4: Agregar TipoDocumentoId como columna nullable en
            //         Personas + índice de soporte. Todavía no es FK — el
            //         backfill corre antes.
            // =================================================================
            migrationBuilder.AddColumn<Guid>(
                name: "TipoDocumentoId",
                table: "Personas",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_TipoDocumentoId",
                table: "Personas",
                column: "TipoDocumentoId");

            // =================================================================
            // STEP 5: Backfill parcial. Mapea Personas.TipoDocumento (string)
            //         → TiposDocumento.Id por match exacto de Codigo. Las
            //         filas con valores sucios quedan con TipoDocumentoId=NULL
            //         (política opt-in relajada) y NumeroDocumento preservado.
            // =================================================================
            migrationBuilder.Sql(@"
                UPDATE Personas p
                INNER JOIN TiposDocumento t ON t.Codigo = p.TipoDocumento
                SET p.TipoDocumentoId = t.Id
                WHERE p.TipoDocumento IS NOT NULL;
            ");

            // =================================================================
            // STEP 6: Recrear ActiveDocumentoUnique con la nueva fórmula.
            //         MySQL InnoDB rechaza ALTER COLUMN sobre columna indexada:
            //         drop index → alter → create index. MySQL también
            //         re-evalúa la expresión de la columna generada durante
            //         el ALTER, así que los valores viejos se recalculan
            //         automáticamente con la nueva fórmula (precedente
            //         FixActivePuestoIdUniqueType).
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Personas_ActiveDocumentoUnique",
                table: "Personas");

            migrationBuilder.AlterColumn<string>(
                name: "ActiveDocumentoUnique",
                table: "Personas",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci",
                computedColumnSql: "CASE WHEN `TipoDocumentoId` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(`TipoDocumentoId`, ':', `NumeroDocumento`) ELSE NULL END",
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true,
                oldComputedColumnSql: "CASE WHEN `TipoDocumento` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(`TipoDocumento`, ':', `NumeroDocumento`) ELSE NULL END",
                oldCollation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_ActiveDocumentoUnique",
                table: "Personas",
                column: "ActiveDocumentoUnique",
                unique: true);

            // =================================================================
            // STEP 7: FK Restrict entre Personas.TipoDocumentoId → TiposDocumento.Id
            // =================================================================
            migrationBuilder.AddForeignKey(
                name: "FK_Personas_TiposDocumento_TipoDocumentoId",
                table: "Personas",
                column: "TipoDocumentoId",
                principalTable: "TiposDocumento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // =================================================================
            // STEP 8: DROP de la columna legacy TipoDocumento. El backfill
            //         ya corrió (óptimo o NULL) y la FK está en place, así
            //         que la invariante #3 de REQ-SPA-EVOLUTION-001 (no
            //         DROP hasta tener backfill + FK) se cumple.
            // =================================================================
            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "Personas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only by design. Revertir reintroduce la columna
            // legacy y requiere restaurar TipoDocumentoId → Codigo desde
            // el join inverso. Una reversión debe ser una migración
            // correctiva explícita (precedente FixActivePuestoIdUniqueType).
            throw new NotSupportedException(
                "Migración forward-only. Para revertir, escribir una migración correctiva explícita.");
        }
    }
}
