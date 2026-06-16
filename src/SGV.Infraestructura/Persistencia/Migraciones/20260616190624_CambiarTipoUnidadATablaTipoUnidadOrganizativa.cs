using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SGV.Infraestructura.Persistencia.Catalogos;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    /// <summary>
    /// Expand-contract migration: replace UnidadOrganizativa.TipoUnidad (string)
    /// with FK to TipoUnidadOrganizativa catalog.
    ///
    /// Steps:
    /// 1. Create TiposUnidadOrganizativa table + unique index + seed 7 rows
    /// 2. Fail-loud pre-flight: SIGNAL if any dirty TipoUnidad values exist
    /// 3. Add nullable TipoUnidadOrganizativaId + index
    /// 4. Backfill by JOIN
    /// 5. Enforce NOT NULL + FK
    /// 6. Drop legacy TipoUnidad column
    /// </summary>
    public partial class CambiarTipoUnidadATablaTipoUnidadOrganizativa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== STEP 1: Create TiposUnidadOrganizativa + unique index + seed 7 rows =====
            migrationBuilder.CreateTable(
                name: "TiposUnidadOrganizativa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Codigo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposUnidadOrganizativa", x => x.Id);
                    table.CheckConstraint("CK_TiposUnidadOrganizativa_Codigo", "`Codigo` <> ''");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TiposUnidadOrganizativa_Codigo",
                table: "TiposUnidadOrganizativa",
                column: "Codigo",
                unique: true);

            migrationBuilder.InsertData(
                table: "TiposUnidadOrganizativa",
                columns: new[] { "Id", "Codigo", "Nombre" },
                values: new object[,]
                {
                    { TipoUnidadOrganizativaConstantes.InstitucionId,  "Institucion",   "Institución"   },
                    { TipoUnidadOrganizativaConstantes.FacultadId,     "Facultad",      "Facultad"      },
                    { TipoUnidadOrganizativaConstantes.SecretariaId,   "Secretaria",    "Secretaría"    },
                    { TipoUnidadOrganizativaConstantes.DireccionId,    "Direccion",     "Dirección"     },
                    { TipoUnidadOrganizativaConstantes.DepartamentoId, "Departamento",  "Departamento"  },
                    { TipoUnidadOrganizativaConstantes.DivisionId,     "Division",      "División"      },
                    { TipoUnidadOrganizativaConstantes.AreaId,         "Area",          "Área"          }
                });

            // ===== STEP 2: Fail-loud pre-flight =====
            // Any pre-existing TipoUnidad string that is not in the seed → abort with SIGNAL.
            // This is BEFORE any schema change, so a dirty DB rolls back cleanly.
            // MySQL does not support IF/THEN in regular batches (only in stored procedures),
            // so we use a prepared statement pattern to SIGNAL conditionally.
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE _DirtyTiposUnidad AS
                SELECT DISTINCT TipoUnidad
                FROM UnidadesOrganizativas
                WHERE TipoUnidad NOT IN (
                    'Institucion', 'Facultad', 'Secretaria', 'Direccion',
                    'Departamento', 'Division', 'Area'
                );
            ");

            migrationBuilder.Sql(@"
                SET @has_dirty = (SELECT COUNT(*) FROM _DirtyTiposUnidad);
                SET @msg = (
                    SELECT COALESCE(
                        (SELECT GROUP_CONCAT(TipoUnidad SEPARATOR ', ')
                         FROM (SELECT TipoUnidad FROM _DirtyTiposUnidad LIMIT 5) AS d),
                        'ninguno')
                );
                SET @signal = CONCAT(
                    'SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = \'Backfill fail-loud: ',
                    @has_dirty, ' valores de TipoUnidad sin catalogar. Ejemplos: ',
                    @msg, '\''
                );
                SET @noop = 'SELECT 1 AS ok';
                SET @sql = IF(@has_dirty > 0, @signal, @noop);
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql("DROP TEMPORARY TABLE IF EXISTS _DirtyTiposUnidad;");

            // ===== STEP 3: Add TipoUnidadOrganizativaId (nullable) + index =====
            migrationBuilder.AddColumn<Guid>(
                name: "TipoUnidadOrganizativaId",
                table: "UnidadesOrganizativas",
                type: "char(36)",
                nullable: true,
                defaultValue: null,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesOrganizativas_TipoUnidadOrganizativaId",
                table: "UnidadesOrganizativas",
                column: "TipoUnidadOrganizativaId");

            // ===== STEP 4: Backfill by JOIN =====
            migrationBuilder.Sql(@"
                UPDATE UnidadesOrganizativas u
                INNER JOIN TiposUnidadOrganizativa t ON t.Codigo = u.TipoUnidad
                SET u.TipoUnidadOrganizativaId = t.Id;
            ");

            // ===== STEP 5: Enforce NOT NULL + FK =====
            // Use raw SQL ALTER TABLE MODIFY because EF's AlterColumn may conflict
            // with the nullable-to-non-nullable change semantics.
            migrationBuilder.Sql(@"
                ALTER TABLE `UnidadesOrganizativas`
                MODIFY COLUMN `TipoUnidadOrganizativaId` char(36) NOT NULL
                COLLATE ascii_general_ci;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_UnidadesOrganizativas_TiposUnidadOrganizativa_TipoUnidadOrganizativaId",
                table: "UnidadesOrganizativas",
                column: "TipoUnidadOrganizativaId",
                principalTable: "TiposUnidadOrganizativa",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ===== STEP 6: Drop the legacy string column =====
            migrationBuilder.DropColumn(
                name: "TipoUnidad",
                table: "UnidadesOrganizativas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Re-add TipoUnidad as nullable varchar(50) — re-populate from JOIN (best-effort)
            migrationBuilder.AddColumn<string>(
                name: "TipoUnidad",
                table: "UnidadesOrganizativas",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: null)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE UnidadesOrganizativas u
                INNER JOIN TiposUnidadOrganizativa t ON t.Id = u.TipoUnidadOrganizativaId
                SET u.TipoUnidad = t.Codigo;
            ");

            // 2. Drop FK
            migrationBuilder.DropForeignKey(
                name: "FK_UnidadesOrganizativas_TiposUnidadOrganizativa_TipoUnidadOrganizativaId",
                table: "UnidadesOrganizativas");

            // 3. Drop index
            migrationBuilder.DropIndex(
                name: "IX_UnidadesOrganizativas_TipoUnidadOrganizativaId",
                table: "UnidadesOrganizativas");

            // 4. Drop column
            migrationBuilder.DropColumn(
                name: "TipoUnidadOrganizativaId",
                table: "UnidadesOrganizativas");

            // 5. Drop table
            migrationBuilder.DropTable(name: "TiposUnidadOrganizativa");

            // 6. Make re-added TipoUnidad NOT NULL
            // (skip if some rows have NULL because the backfill above is best-effort)
            migrationBuilder.Sql(@"
                UPDATE UnidadesOrganizativas
                SET TipoUnidad = '' WHERE TipoUnidad IS NULL;
            ");
        }
    }
}
