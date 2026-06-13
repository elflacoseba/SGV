using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarDatosSemillaBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "Administrador", "rol-Administrador", "Administrador", "ADMINISTRADOR" },
                    { "EvaluadorSeleccion", "rol-EvaluadorSeleccion", "EvaluadorSeleccion", "EVALUADORSELECCION" },
                    { "GestorOrganizacional", "rol-GestorOrganizacional", "GestorOrganizacional", "GESTORORGANIZACIONAL" },
                    { "Lector", "rol-Lector", "Lector", "LECTOR" },
                    { "RecursosHumanos", "rol-RecursosHumanos", "RecursosHumanos", "RECURSOSHUMANOS" }
                });

            migrationBuilder.InsertData(
                table: "Cargos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "Descripcion", "IsActive", "IsDeleted", "Nivel", "Nombre", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "DECANO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Directivo", "Decano", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "SECRETARIO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Directivo", "Secretario", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "DIRECTOR", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Conducción media", "Director", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "JEFE_DEPARTAMENTO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Conducción media", "Jefe de Departamento", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "ADMINISTRATIVO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Operativo", "Administrativo", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000006"), "PROFESOR", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Académico", "Profesor", null, null }
                });

            migrationBuilder.InsertData(
                table: "Habilidades",
                columns: new[] { "Id", "Categoria", "Codigo", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "Descripcion", "IsActive", "IsDeleted", "Nombre", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), "Conducción", "LIDERAZGO", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Liderazgo", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000002"), "Conducción", "GESTION_PERSONAL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Gestión de Personal", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000003"), "Técnica", "SQL_SERVER", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "SQL Server", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000004"), "Técnica", "EF_CORE", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Entity Framework Core", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000005"), "Técnica", "DOTNET", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Programación .NET", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000006"), "Dominio", "ADMINISTRACION_PUBLICA", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Administración Pública", null, null },
                    { new Guid("50000000-0000-0000-0000-000000000007"), "Académica", "DOCENCIA_UNIVERSITARIA", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, null, null, true, false, "Docencia Universitaria", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Administrador");

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

            migrationBuilder.DeleteData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Cargos",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Habilidades",
                keyColumn: "Id",
                keyValue: new Guid("50000000-0000-0000-0000-000000000007"));
        }
    }
}
