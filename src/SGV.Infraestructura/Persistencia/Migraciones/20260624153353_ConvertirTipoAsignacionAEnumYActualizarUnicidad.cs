using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ConvertirTipoAsignacionAEnumYActualizarUnicidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop old uniqueness index and column for Persona global uniqueness
            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_ActivePersonaIdUnique",
                table: "Ocupaciones");

            migrationBuilder.DropColumn(
                name: "ActivePersonaIdUnique",
                table: "Ocupaciones");

            // Step 2: Convert known legacy string values to their numeric equivalents
            // Must happen BEFORE altering the column type to int.
            // Enum mapping: Permanente=0, Interina=1, Temporal=2
            migrationBuilder.Sql(
                "UPDATE `Ocupaciones` SET `TipoAsignacion` = '0' WHERE `TipoAsignacion` = 'Permanente'");

            migrationBuilder.Sql(
                "UPDATE `Ocupaciones` SET `TipoAsignacion` = '1' WHERE `TipoAsignacion` = 'Interina'");

            migrationBuilder.Sql(
                "UPDATE `Ocupaciones` SET `TipoAsignacion` = '2' WHERE `TipoAsignacion` = 'Temporal'");

            // Step 3: Convert column from varchar to int
            // If any unknown string values remain, MySQL will throw an error here
            // because it cannot cast unrecognized strings to int.
            migrationBuilder.AlterColumn<int>(
                name: "TipoAsignacion",
                table: "Ocupaciones",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Step 4: Add new computed column for Persona+Puesto active uniqueness
            migrationBuilder.AddColumn<string>(
                name: "ActivePersonaPuestoUnique",
                table: "Ocupaciones",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN CONCAT(`PersonaId`, ':', `PuestoId`) ELSE NULL END")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Step 5: Create unique index on the new computed column
            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePersonaPuestoUnique",
                table: "Ocupaciones",
                column: "ActivePersonaPuestoUnique",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: drop new index and column
            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_ActivePersonaPuestoUnique",
                table: "Ocupaciones");

            migrationBuilder.DropColumn(
                name: "ActivePersonaPuestoUnique",
                table: "Ocupaciones");

            // Convert numeric values back to textual equivalents
            migrationBuilder.Sql(
                "UPDATE `Ocupaciones` SET `TipoAsignacion` = 'Permanente' WHERE `TipoAsignacion` = 0");

            migrationBuilder.Sql(
                "UPDATE `Ocupaciones` SET `TipoAsignacion` = 'Interina' WHERE `TipoAsignacion` = 1");

            migrationBuilder.Sql(
                "UPDATE `Ocupaciones` SET `TipoAsignacion` = 'Temporal' WHERE `TipoAsignacion` = 2");

            // Revert column type from int to varchar(50)
            migrationBuilder.AlterColumn<string>(
                name: "TipoAsignacion",
                table: "Ocupaciones",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Restore old computed column and index
            migrationBuilder.AddColumn<int>(
                name: "ActivePersonaIdUnique",
                table: "Ocupaciones",
                type: "int",
                nullable: true,
                computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_ActivePersonaIdUnique",
                table: "Ocupaciones",
                column: "ActivePersonaIdUnique",
                unique: true);
        }
    }
}
