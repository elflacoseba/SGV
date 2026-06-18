using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SGV.Infraestructura.Persistencia.Catalogos;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CambiarNivelStringANivelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ====================================================================
            // STEP 1: Pre-flight — detectar valores sucios en Cargos.Nivel
            //         (fail-loud antes de cualquier ALTER)
            //         Checks against the known old string values from the existing seed.
            // ====================================================================
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE IF NOT EXISTS _DirtyNivelesCargo AS
                SELECT DISTINCT Nivel
                FROM Cargos
                WHERE Nivel IS NOT NULL
                  AND Nivel NOT IN ('Directivo', 'Conducción media', 'Operativo', 'Académico');

                SET @dirtyCount = (SELECT COUNT(*) FROM _DirtyNivelesCargo);
                SET @dirtyExamples = (
                    SELECT COALESCE(GROUP_CONCAT(Nivel SEPARATOR ', '), 'ninguno')
                    FROM (SELECT Nivel FROM _DirtyNivelesCargo LIMIT 5) AS d
                );

                SET @msg = CONCAT(
                    'Backfill fail-loud: ', @dirtyCount,
                    ' valores de Nivel sin catalogar. Ejemplos: ', @dirtyExamples
                );

                SET @sql = IF(@dirtyCount > 0,
                    CONCAT('SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = ''', @msg, ''''),
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                DROP TEMPORARY TABLE IF EXISTS _DirtyNivelesCargo;
            ");

            // ====================================================================
            // STEP 2: Crear tabla NivelesCargo
            // ====================================================================
            migrationBuilder.CreateTable(
                name: "NivelesCargo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValorNumerico = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NivelesCargo", x => x.Id);
                    table.CheckConstraint("CK_NivelesCargo_ValorNumerico", "`ValorNumerico` >= 0 AND `ValorNumerico` <= 255");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ====================================================================
            // STEP 3: InsertData seed (4 niveles)
            //         Built from NivelCargoConstantes.Semilla so the migration
            //         cannot drift from the single source of truth.
            // ====================================================================
            var semillas = NivelCargoConstantes.Semilla;
            var values = new object[semillas.Count, 5];
            for (var i = 0; i < semillas.Count; i++)
            {
                var s = semillas[i];
                values[i, 0] = s.Id;
                values[i, 1] = s.Codigo;
                values[i, 2] = s.Nombre;
                values[i, 3] = s.Orden;
                values[i, 4] = s.ValorNumerico;
            }
            migrationBuilder.InsertData(
                table: "NivelesCargo",
                columns: new[] { "Id", "Codigo", "Nombre", "Orden", "ValorNumerico" },
                values: values);

            // ====================================================================
            // STEP 4: Agregar NivelId nullable
            // ====================================================================
            migrationBuilder.AddColumn<Guid>(
                name: "NivelId",
                table: "Cargos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            // ====================================================================
            // STEP 5: Backfill — mapea Cargos.Nivel (old string) → NivelesCargo.Id
            //         Uses inline mapping of old string values to seed Codigos
            // ====================================================================
            migrationBuilder.Sql(@"
                UPDATE Cargos c
                INNER JOIN NivelesCargo nc ON
                    (c.Nivel = 'Directivo'        AND nc.Codigo = 'Directivo')
                    OR (c.Nivel = 'Conducción media' AND nc.Codigo = 'ConduccionMedia')
                    OR (c.Nivel = 'Operativo'        AND nc.Codigo = 'Operativo')
                    OR (c.Nivel = 'Académico'        AND nc.Codigo = 'Academico')
                SET c.NivelId = nc.Id
                WHERE c.Nivel IS NOT NULL;
            ");

            // Seed data UpdateData for Cargos (set NivelId for seed rows).
            // NivelId values come from NivelCargoConstantes — the migration must
            // never embed a literal NivelCargo Guid.
            migrationBuilder.UpdateData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                column: "NivelId",
                value: NivelCargoConstantes.DirectivoId);

            migrationBuilder.UpdateData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                column: "NivelId",
                value: NivelCargoConstantes.DirectivoId);

            migrationBuilder.UpdateData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "NivelId",
                value: NivelCargoConstantes.ConduccionMediaId);

            migrationBuilder.UpdateData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                column: "NivelId",
                value: NivelCargoConstantes.ConduccionMediaId);

            migrationBuilder.UpdateData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                column: "NivelId",
                value: NivelCargoConstantes.OperativoId);

            migrationBuilder.UpdateData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"),
                column: "NivelId",
                value: NivelCargoConstantes.AcademicoId);

            // ====================================================================
            // STEP 6: Hacer NivelId NOT NULL
            // ====================================================================
            migrationBuilder.AlterColumn<Guid>(
                name: "NivelId",
                table: "Cargos",
                type: "char(36)",
                nullable: false,
                collation: "ascii_general_ci");

            // ====================================================================
            // STEP 7: FK + índice
            // ====================================================================
            migrationBuilder.CreateIndex(
                name: "IX_Cargos_NivelId",
                table: "Cargos",
                column: "NivelId");

            migrationBuilder.CreateIndex(
                name: "IX_NivelesCargo_Codigo",
                table: "NivelesCargo",
                column: "Codigo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cargos_NivelesCargo_NivelId",
                table: "Cargos",
                column: "NivelId",
                principalTable: "NivelesCargo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ====================================================================
            // STEP 8: Eliminar columna string Nivel
            // ====================================================================
            migrationBuilder.DropColumn(
                name: "Nivel",
                table: "Cargos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore Nivel string column
            migrationBuilder.AddColumn<string>(
                name: "Nivel",
                table: "Cargos",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Restore seed Nivel values from NivelId
            migrationBuilder.Sql(@"
                UPDATE Cargos c
                LEFT JOIN NivelesCargo nc ON nc.Id = c.NivelId
                SET c.Nivel = nc.Nombre
                WHERE nc.Id IS NOT NULL;
            ");

            // Drop FK and indexes
            migrationBuilder.DropForeignKey(
                name: "FK_Cargos_NivelesCargo_NivelId",
                table: "Cargos");

            migrationBuilder.DropIndex(
                name: "IX_Cargos_NivelId",
                table: "Cargos");

            // Drop NivelId column
            migrationBuilder.DropColumn(
                name: "NivelId",
                table: "Cargos");

            // Drop NivelesCargo table
            migrationBuilder.DropTable(
                name: "NivelesCargo");
        }
    }
}
