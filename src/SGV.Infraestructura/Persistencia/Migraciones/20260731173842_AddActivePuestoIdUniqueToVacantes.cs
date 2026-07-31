using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    /// <summary>
    /// Cierra la ventana TOCTOU del módulo vacantes (issue #238): unique
    /// constraint parcial sobre <c>PuestoId</c> filtrado por
    /// <c>FechaCierre IS NULL AND IsDeleted = 0</c> vía columna calculada
    /// STORED. Patrón de columna calculada vigente en Cargo, Puesto,
    /// Habilidad, UnidadOrganizativa, Ocupacion y Postulante. La columna
    /// evalúa a NULL para vacantes cerradas o soft-deleted — MySQL ignora
    /// NULL en el unique index, permitiendo múltiples filas con constraint
    /// satisfecha. El pre-check <c>ExistsAbiertaByPuestoAsync</c> se
    /// conserva como rechazo temprano; la BD es fuente de verdad ante
    /// carrera concurrente.
    /// </summary>
    public partial class AddActivePuestoIdUniqueToVacantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivePuestoIdUnique",
                table: "Vacantes",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true,
                computedColumnSql: "CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END",
                stored: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Vacantes_ActivePuestoIdUnique",
                table: "Vacantes",
                column: "ActivePuestoIdUnique",
                unique: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Migración forward-only (paridad con FixActivePuestoIdUniqueType y
        /// AddCategoriaHabilidadCatalog). Para revertir, escribir una
        /// migración correctiva explícita que dropee el índice y la columna
        /// en orden seguro. El catch de
        /// <c>VacanteServicioComandos.CrearAsync</c> mapea la constraint
        /// violation a <c>VacanteErrorCodigo.PuestoConVacanteAbierta</c> —
        /// si se revierte esta migración sin reescribir el catch, las
        /// carreras concurrentes vuelven a la ventana TOCTOU original.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Migración forward-only. Para revertir, escribir una migración correctiva explícita.");
        }
    }
}

