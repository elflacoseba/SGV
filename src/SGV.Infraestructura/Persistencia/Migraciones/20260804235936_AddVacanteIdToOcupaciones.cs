using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AddVacanteIdToOcupaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VacanteId",
                table: "Ocupaciones",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Ocupaciones_VacanteId",
                table: "Ocupaciones",
                column: "VacanteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ocupaciones_Vacantes_VacanteId",
                table: "Ocupaciones",
                column: "VacanteId",
                principalTable: "Vacantes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ocupaciones_Vacantes_VacanteId",
                table: "Ocupaciones");

            migrationBuilder.DropIndex(
                name: "IX_Ocupaciones_VacanteId",
                table: "Ocupaciones");

            migrationBuilder.DropColumn(
                name: "VacanteId",
                table: "Ocupaciones");
        }
    }
}
