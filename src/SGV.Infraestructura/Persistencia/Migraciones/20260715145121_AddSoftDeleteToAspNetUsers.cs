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

            // PR #148 review: replicamos sobre PersonaId el mismo patrón
            // soft-delete-aware aplicado a UserName. El índice único plano
            // IX_AspNetUsers_PersonaId bloquea la recreación de un usuario
            // cuando el previo fue dado de baja lógica; lo reemplazamos
            // por una columna generada que devuelve NULL cuando
            // IsDeleted = 1, de modo que la unicidad sólo aplica a
            // usuarios activos.
            //
            // Secuencia crítica: MySQL no permite DROP INDEX sobre un
            // índice que sostiene la FK 1:1 AspNetUsers.PersonaId →
            // Personas.Id. Hacemos swap atómico: dropeamos la FK,
            // demoteamos el índice a NO-ÚNICO (preservando nombre y
            // soporte de JOINs), re-adherimos la FK y luego añadimos la
            // columna generada + su índice único soft-delete-aware.
            //
            // 1) DROP FK — metadata-only, INPLACE/NONE.
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  DROP FOREIGN KEY `FK_AspNetUsers_Personas_PersonaId`,
                  ALGORITHM=INPLACE,
                  LOCK=NONE;
                """);

            // 2) DROP unique index (ahora permitido porque la FK ya no
            //    depende de él). INPLACE/NONE.
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  DROP INDEX `IX_AspNetUsers_PersonaId`,
                  ALGORITHM=INPLACE,
                  LOCK=NONE;
                """);

            // 3) Re-crear el índice como NO-ÚNICO con el MISMO nombre,
            //    para preservar el nombre canónico heredado de la
            //    migración VincularIdentityUsuariosAPersonas. Este
            //    índice sigue acelerando el lookup del FK y los JOINs
            //    contra Personas.Id.
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  ADD INDEX `IX_AspNetUsers_PersonaId` (`PersonaId`),
                  ALGORITHM=INPLACE,
                  LOCK=NONE;
                """);

            // 4) Re-crear la FK (mismas reglas que la migración original
            //    VincularIdentityUsuariosAPersonas). MySQL requiere
            //    ALGORITHM=COPY para ADD CONSTRAINT (los FKs no son
            //    metadata-only como un CREATE INDEX), así que este
            //    statement es el segundo COPY de la migración.
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId`
                  FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`)
                  ON DELETE RESTRICT,
                  ALGORITHM=COPY;
                """);

            // 5) Columna generada STORED requiere ALGORITHM=COPY (MySQL 8
            //    no permite STORED + INPLACE). Mismo trade-off explícito
            //    que ya documentamos para ActiveUserNameUnique. CHAR(36)
            //    coincide con el tipo que Pomelo asigna a Guid en MySQL
            //    y permite que el proveedor relea la fila resultante del
            //    INSERT sin InvalidCastException (Guid ↔ string nativo).
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  ADD COLUMN `ActivePersonaIdUnique` CHAR(36)
                    COLLATE `ascii_general_ci`
                    GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END) STORED,
                  ALGORITHM=COPY;
                """);

            // 6) Nuevo índice único sobre la columna generada.
            migrationBuilder.Sql(
                """
                ALTER TABLE `AspNetUsers`
                  ADD UNIQUE INDEX `IX_AspNetUsers_ActivePersonaIdUnique` (`ActivePersonaIdUnique`),
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
