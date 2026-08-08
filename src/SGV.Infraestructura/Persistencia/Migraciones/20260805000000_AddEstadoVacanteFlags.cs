using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AddEstadoVacanteFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsCubierta",
                table: "EstadosVacante",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsCancelada",
                table: "EstadosVacante",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "EstadosVacante",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "EsCubierta" },
                values: new object[] { true });

            migrationBuilder.UpdateData(
                table: "EstadosVacante",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                columns: new[] { "EsCancelada" },
                values: new object[] { true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsCancelada",
                table: "EstadosVacante");

            migrationBuilder.DropColumn(
                name: "EsCubierta",
                table: "EstadosVacante");
        }
    }
}
