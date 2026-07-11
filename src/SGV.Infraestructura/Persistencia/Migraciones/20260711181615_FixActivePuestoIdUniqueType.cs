using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FixActivePuestoIdUniqueType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL rejects ALTER COLUMN on an indexed column. Drop the
            // unique index, alter the column, recreate the index. The
            // computed expression is re-evaluated by MySQL during ALTER,
            // so any pre-existing `'0'` values (from a permissive sql_mode
            // truncating char(36) → int) are replaced with the correct
            // PuestoId string or NULL automatically.
            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_ActivePuestoIdUnique",
                table: "Ocupaciones");

            migrationBuilder.AlterColumn<string>(
                name: "ActivePuestoIdUnique",
                table: "Ocupaciones",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true,
                collation: "ascii_general_ci",
                computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePuestoIdUnique",
                table: "Ocupaciones",
                column: "ActivePuestoIdUnique",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only by design. Reverting would reintroduce the bug;
            // a corrective migration must be authored explicitly.
            throw new NotSupportedException("Migración forward-only. Para revertir, escribir una migración correctiva explícita.");
        }
    }
}
