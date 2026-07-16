using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToAspNetUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
                  ALGORITHM=INPLACE,
                  LOCK=NONE;
                """);

            // MySQL 8 cannot add a STORED generated column with ALGORITHM=INPLACE.
            // Preserve the specified final schema and make the unavoidable table-copy
            // operation explicit so deployment planning can budget the lock window.
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  ADD COLUMN `ActiveUserNameUnique` VARCHAR(256)
                    COLLATE `utf8mb4_0900_ai_ci`
                    GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN LOWER(`UserName`) ELSE NULL END) STORED,
                  ALGORITHM=COPY;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  ADD UNIQUE INDEX `IX_AspNetUsers_ActiveUserNameUnique` (`ActiveUserNameUnique`),
                  ALGORITHM=INPLACE,
                  LOCK=NONE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Migración forward-only. Para revertir, escribir una migración correctiva explícita.");
        }
    }
}
