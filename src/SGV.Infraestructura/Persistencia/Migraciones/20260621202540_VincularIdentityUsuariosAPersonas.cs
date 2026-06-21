using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VincularIdentityUsuariosAPersonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @existingUsers = (SELECT COUNT(*) FROM AspNetUsers);
                SET @userMsg = CONCAT(
                    'Backfill fail-loud: AspNetUsers contains ', @existingUsers,
                    ' existing users. Populate PersonaId explicitly before applying this migration.'
                );
                SET @userSql = IF(@existingUsers > 0,
                    CONCAT('SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = ''', @userMsg, ''''),
                    'SELECT 1');
                PREPARE stmt FROM @userSql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                SET @legacyAssignments = (
                    SELECT COUNT(*)
                    FROM AspNetUserRoles ur
                    INNER JOIN AspNetRoles r ON r.Id = ur.RoleId
                    WHERE r.Id IN ('RecursosHumanos', 'GestorOrganizacional', 'EvaluadorSeleccion', 'Lector')
                );
                SET @roleMsg = CONCAT(
                    'Backfill fail-loud: ', @legacyAssignments,
                    ' assignments use legacy roles. Reassign users to Administrador, GestorVacantes, or Consultor before applying this migration.'
                );
                SET @roleSql = IF(@legacyAssignments > 0,
                    CONCAT('SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = ''', @roleMsg, ''''),
                    'SELECT 1');
                PREPARE stmt FROM @roleSql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "EvaluadorSeleccion");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "GestorOrganizacional");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Lector");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "RecursosHumanos");

            migrationBuilder.AddColumn<Guid>(
                name: "PersonaId",
                table: "AspNetUsers",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "Consultor", "rol-Consultor", "Consultor", "CONSULTOR" },
                    { "GestorVacantes", "rol-GestorVacantes", "GestorVacantes", "GESTORVACANTES" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PersonaId",
                table: "AspNetUsers",
                column: "PersonaId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Personas_PersonaId",
                table: "AspNetUsers",
                column: "PersonaId",
                principalTable: "Personas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Personas_PersonaId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PersonaId",
                table: "AspNetUsers");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Consultor");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "GestorVacantes");

            migrationBuilder.DropColumn(
                name: "PersonaId",
                table: "AspNetUsers");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "EvaluadorSeleccion", "rol-EvaluadorSeleccion", "EvaluadorSeleccion", "EVALUADORSELECCION" },
                    { "GestorOrganizacional", "rol-GestorOrganizacional", "GestorOrganizacional", "GESTORORGANIZACIONAL" },
                    { "Lector", "rol-Lector", "Lector", "LECTOR" },
                    { "RecursosHumanos", "rol-RecursosHumanos", "RecursosHumanos", "RECURSOSHUMANOS" }
                });
        }
    }
}
