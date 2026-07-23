using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SGV.Infraestructura.Persistencia.Catalogos;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <summary>
    /// Issue migrar-campo-categoria-habilidades-a-tabla: introduce el catálogo
    /// inmutable <c>CategoriasHabilidad</c> (4 filas seed en el bloque
    /// <c>72000000-…</c>) y reemplaza <c>Habilidades.Categoria</c> (string) por
    /// <c>Habilidades.CategoriaId</c> (FK <c>char(36) NULL</c>) con
    /// <c>OnDelete(Restrict)</c>. El índice legacy <c>IX_Habilidades_Categoria</c>
    /// se reemplaza por <c>IX_Habilidades_CategoriaId</c>.
    ///
    /// <b>Variante opt-in relajada del REQ-SPA-EVOLUTION-001 condición #3</b>
    /// (precedente <c>Personas.TipoDocumento</c> issue #147): los valores
    /// legacy de <c>Categoria</c> que NO matcheen ningún <c>Nombre</c> del
    /// seed quedan con <c>CategoriaId = NULL</c> y se registran en
    /// <c>Auditorias</c> con
    /// <c>Metadata = { Origen, CategoriaOriginal }</c> para remediación
    /// post-deploy.
    ///
    /// <b>Forward-only</b>: <c>Down()</c> lanza <c>NotSupportedException</c>
    /// como primera línea (precedente <c>FixActivePuestoIdUniqueType</c>).
    /// </summary>
    public partial class AddCategoriaHabilidadCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =================================================================
            // STEP 1: Crear la tabla CategoriasHabilidad con su índice único
            //         en Codigo y su check constraint.
            // =================================================================
            migrationBuilder.CreateTable(
                name: "CategoriasHabilidad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasHabilidad", x => x.Id);
                    table.CheckConstraint("CK_CategoriasHabilidad_Codigo", "`Codigo` <> ''");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // =================================================================
            // STEP 2: InsertData de los 4 seeds desde CategoriaHabilidadConstantes
            //         (misma source of truth que DatosSemilla.HasData).
            // =================================================================
            var semillas = CategoriaHabilidadConstantes.Semilla;
            var values = new object[semillas.Count, 3];
            for (var i = 0; i < semillas.Count; i++)
            {
                var s = semillas[i];
                values[i, 0] = s.Id;
                values[i, 1] = s.Codigo;
                values[i, 2] = s.Nombre;
            }
            migrationBuilder.InsertData(
                table: "CategoriasHabilidad",
                columns: new[] { "Id", "Codigo", "Nombre" },
                values: values);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasHabilidad_Codigo",
                table: "CategoriasHabilidad",
                column: "Codigo",
                unique: true);

            // =================================================================
            // STEP 3: Pre-flight NO fail-loud. Lista los valores legacy sucios
            //         (los que no matchean ningún Nombre del seed) para logging
            //         operativo. No aborta — la política es opt-in relajada.
            // =================================================================
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE IF NOT EXISTS _DirtyCategoriaHabilidad AS
                SELECT DISTINCT h.Categoria
                FROM Habilidades h
                WHERE h.Categoria IS NOT NULL
                  AND LOWER(h.Categoria) NOT IN (
                      SELECT LOWER(Nombre) FROM CategoriasHabilidad
                  );

                SET @dirtyCount = (SELECT COUNT(*) FROM _DirtyCategoriaHabilidad);
                SET @dirtyExamples = (
                    SELECT COALESCE(GROUP_CONCAT(DISTINCT Categoria SEPARATOR ', '), 'ninguno')
                    FROM (SELECT Categoria FROM _DirtyCategoriaHabilidad LIMIT 5) AS d
                );

                -- Log diagnóstico (no aborta). Se puede inspeccionar vía
                -- SHOW ENGINE INNODB STATUS o capturando el output del
                -- EF migration.
                SELECT CONCAT('Backfill opt-in relajado: ', @dirtyCount,
                              ' valores de Categoria sin catalogar. Ejemplos: ',
                              @dirtyExamples) AS _backfill_diagnostics;

                DROP TEMPORARY TABLE IF EXISTS _DirtyCategoriaHabilidad;
            ");

            // =================================================================
            // STEP 4: Agregar CategoriaId como columna nullable en Habilidades
            //         + índice de soporte. Todavía no es FK — el backfill corre
            //         antes.
            // =================================================================
            migrationBuilder.AddColumn<Guid>(
                name: "CategoriaId",
                table: "Habilidades",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Habilidades_CategoriaId",
                table: "Habilidades",
                column: "CategoriaId");

            // =================================================================
            // STEP 5: Backfill case-insensitive. Mapea Habilidades.Categoria
            //         (string legacy) → CategoriasHabilidad.Id por match de
            //         Nombre. Las filas con valores sucios quedan con
            //         CategoriaId=NULL (política opt-in relajada).
            // =================================================================
            migrationBuilder.Sql(@"
                UPDATE Habilidades h
                INNER JOIN CategoriasHabilidad c ON LOWER(c.Nombre) = LOWER(h.Categoria)
                SET h.CategoriaId = c.Id
                WHERE h.Categoria IS NOT NULL;
            ");

            // =================================================================
            // STEP 6: Auditoría de filas legacy sin match (fuera del
            //         interceptor de EF Core; el interceptor NO captura
            //         cambios que pasan por migrationBuilder.Sql).
            //         Firma reproducible para queries de remediación:
            //           Origen = "Migracion.AddCategoriaHabilidadCatalog"
            //           CategoriaOriginal = <valor legacy>
            // =================================================================
            migrationBuilder.Sql(@"
                INSERT INTO Auditorias (Id, UserId, OccurredAt, EntityName, EntityId, Operation, NewValuesJson)
                SELECT
                    UUID(),
                    NULL,
                    UTC_TIMESTAMP(6),
                    'Habilidad',
                    h.Id,
                    'BackfillLegacyCategoriaToNull',
                    JSON_OBJECT(
                        'Origen', 'Migracion.AddCategoriaHabilidadCatalog',
                        'CategoriaOriginal', h.Categoria
                    )
                FROM Habilidades h
                WHERE h.Categoria IS NOT NULL AND h.CategoriaId IS NULL;
            ");

            // =================================================================
            // STEP 6b: UpdateData de las 7 habilidades semilla (HasData de
            //         DatosSemilla) para fijar CategoriaId explícitamente.
            //         Mantiene Snapshot consistente: EF requiere que el
            //         modelo snapshot y el historial de migraciones estén
            //         alineados sobre los valores seed.
            // =================================================================
            migrationBuilder.UpdateData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"),
                column: "CategoriaId",
                value: CategoriaHabilidadConstantes.ConduccionId);

            migrationBuilder.UpdateData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"),
                column: "CategoriaId",
                value: CategoriaHabilidadConstantes.ConduccionId);

            migrationBuilder.UpdateData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"),
                column: "CategoriaId",
                value: CategoriaHabilidadConstantes.TecnicaId);

            migrationBuilder.UpdateData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"),
                column: "CategoriaId",
                value: CategoriaHabilidadConstantes.TecnicaId);

            migrationBuilder.UpdateData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000005"),
                column: "CategoriaId",
                value: CategoriaHabilidadConstantes.TecnicaId);

            migrationBuilder.UpdateData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000006"),
                column: "CategoriaId",
                value: CategoriaHabilidadConstantes.DominioId);

            migrationBuilder.UpdateData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000007"),
                column: "CategoriaId",
                value: CategoriaHabilidadConstantes.AcademicaId);

            // =================================================================
            // STEP 7: FK Restrict entre Habilidades.CategoriaId → CategoriasHabilidad.Id
            // =================================================================
            migrationBuilder.AddForeignKey(
                name: "FK_Habilidades_CategoriasHabilidad_CategoriaId",
                table: "Habilidades",
                column: "CategoriaId",
                principalTable: "CategoriasHabilidad",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // =================================================================
            // STEP 8: Drop del índice y columna legacy Categoria. El backfill
            //         ya corrió (óptimo o NULL) y la FK está en place, así
            //         que la invariante #3 de REQ-SPA-EVOLUTION-001 (no DROP
            //         hasta tener backfill + FK) se cumple.
            // =================================================================
            migrationBuilder.DropIndex(
                name: "IX_Habilidades_Categoria",
                table: "Habilidades");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Habilidades");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only by design. Revertir reintroduce la columna
            // legacy y requiere restaurar CategoriaId → Nombre desde el
            // join inverso. Una reversión debe ser una migración
            // correctiva explícita (precedente FixActivePuestoIdUniqueType).
            throw new NotSupportedException(
                "Migración forward-only. Para revertir, escribir una migración correctiva explícita.");
        }
    }
}