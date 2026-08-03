using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class IndiceAuditoriaCorrelationIdOccurredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El índice compuesto (CorrelationId, OccurredAt) cubre
            // las queries que filtran solo por CorrelationId
            // (leading column), por lo que el índice simple anterior
            // es redundante. Se elimina para reducir el costo de
            // escritura sobre Auditorias.
            migrationBuilder.DropIndex(
                name: "IX_Auditorias_CorrelationId",
                table: "Auditorias");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_CorrelationId_OccurredAt",
                table: "Auditorias",
                columns: new[] { "CorrelationId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auditorias_CorrelationId_OccurredAt",
                table: "Auditorias");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_CorrelationId",
                table: "Auditorias",
                column: "CorrelationId");
        }
    }
}
