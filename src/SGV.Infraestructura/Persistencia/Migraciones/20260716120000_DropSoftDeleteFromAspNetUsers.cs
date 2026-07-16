using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGV.Infraestructura.Persistencia.Migraciones
{
    /// <summary>
    /// Forward-only migration que retira el soft-delete del módulo
    /// Usuarios. Reemplaza <c>IsDeleted</c> + columnas generadas
    /// STORED <c>ActiveUserNameUnique</c> / <c>ActivePersonaIdUnique</c>
    /// por la unicidad plana sobre <c>IX_AspNetUsers_PersonaId</c>
    /// (UNIQUE) y deja la separación activa/bloqueada en manos de
    /// <c>LockoutEnd</c> nativo de Identity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Orden crítico (ver design D7 y sección "Migración / rollout"
    /// del design.md). MySQL no permite <c>DROP INDEX</c> sobre un
    /// índice que sostiene la FK 1:1 <c>AspNetUsers.PersonaId</c> →
    /// <c>Personas.Id</c> ni <c>DROP COLUMN</c> con columna generada
    /// STORED referenciada por un unique index. La secuencia evita
    /// <c>ALGORITHM=COPY</c> extra y conserva la unicidad 1:1.
    /// </para>
    /// <para>
    /// El preflight fail-loud (paso 1) aborta con SQLSTATE 45000 si
    /// existen duplicados activos de PersonaId; el backfill (paso 2)
    /// traduce <c>IsDeleted=1</c> a un lockout administrativo
    /// (<c>LockoutEnabled=1, LockoutEnd='9999-12-31 23:59:59.999999'</c>)
    /// antes de dropear la columna.
    /// </para>
    /// </remarks>
    public partial class DropSoftDeleteFromAspNetUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ────────────────────────────────────────────────────────────
            // RES-001 (4R review): la migración es reentrante. Un segundo
            // run contra un schema post-D7 (IsDeleted ya no existe)
            // debe ser un no-op, no una excepción. Lo logramos con un
            // stored procedure efímero gated por INFORMATION_SCHEMA.
            //
            // El orden de los 8 pasos dentro del bloque IF sigue
            // siendo el del design D7:
            //  (1) preflight fail-loud duplicados PersonaId;
            //  (2) backfill IsDeleted=1 → LockoutEnd futuro;
            //  (3) DROP FK;
            //  (4) DROP INDEX ActiveUserNameUnique;
            //  (5) DROP INDEX ActivePersonaIdUnique;
            //  (6) DROP COLUMN ActiveUserNameUnique/ActivePersonaIdUnique/IsDeleted;
            //  (7) DROP INDEX PersonaId (no-único);
            //  (8) ADD UNIQUE INDEX PersonaId;
            //  (9) ADD CONSTRAINT FK PersonaId RESTRICT.
            // ────────────────────────────────────────────────────────────
            migrationBuilder.Sql(
                """
                DROP PROCEDURE IF EXISTS __sgvApplyD7;

                CREATE PROCEDURE __sgvApplyD7()
                BEGIN
                    DECLARE _needsD7 INT DEFAULT (
                        SELECT COUNT(*) FROM information_schema.COLUMNS
                        WHERE table_schema = DATABASE()
                          AND table_name = 'AspNetUsers'
                          AND column_name = 'IsDeleted'
                    );
                    IF _needsD7 > 0 THEN
                        -- Paso 1: preflight fail-loud
                        SET @duplicatePersonas = (
                            SELECT COUNT(*) FROM (
                                SELECT `PersonaId` FROM `AspNetUsers`
                                GROUP BY `PersonaId` HAVING COUNT(*) > 1
                            ) AS dupes
                        );
                        SET @preflightMsg = CONCAT(
                            'Backfill fail-loud: ', @duplicatePersonas,
                            ' PersonaId duplicados entre AspNetUsers activas. ',
                            'Resolver duplicados manualmente antes de aplicar esta migración.'
                        );
                        IF @duplicatePersonas > 0 THEN
                            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @preflightMsg;
                        END IF;

                        -- Paso 2: backfill
                        UPDATE `AspNetUsers`
                        SET `LockoutEnabled` = 1,
                            `LockoutEnd` = '9999-12-31 23:59:59.999999'
                        WHERE `IsDeleted` = 1;

                        -- Paso 3: DROP FK
                        ALTER TABLE `AspNetUsers`
                          DROP FOREIGN KEY `FK_AspNetUsers_Personas_PersonaId`,
                          ALGORITHM=INPLACE, LOCK=NONE;

                        -- Paso 4: DROP INDEX ActiveUserNameUnique
                        ALTER TABLE `AspNetUsers`
                          DROP INDEX `IX_AspNetUsers_ActiveUserNameUnique`,
                          ALGORITHM=INPLACE, LOCK=NONE;

                        -- Paso 5: DROP INDEX ActivePersonaIdUnique
                        ALTER TABLE `AspNetUsers`
                          DROP INDEX `IX_AspNetUsers_ActivePersonaIdUnique`,
                          ALGORITHM=INPLACE, LOCK=NONE;

                        -- Paso 6: DROP COLUMNs
                        ALTER TABLE `AspNetUsers`
                          DROP COLUMN `ActiveUserNameUnique`,
                          DROP COLUMN `ActivePersonaIdUnique`,
                          DROP COLUMN `IsDeleted`,
                          ALGORITHM=INPLACE, LOCK=NONE;

                        -- Paso 7: DROP INDEX PersonaId
                        ALTER TABLE `AspNetUsers`
                          DROP INDEX `IX_AspNetUsers_PersonaId`,
                          ALGORITHM=INPLACE, LOCK=NONE;

                        -- Paso 8: ADD UNIQUE INDEX PersonaId
                        ALTER TABLE `AspNetUsers`
                          ADD UNIQUE INDEX `IX_AspNetUsers_PersonaId` (`PersonaId`),
                          ALGORITHM=INPLACE, LOCK=NONE;

                        -- Paso 9: ADD CONSTRAINT FK
                        ALTER TABLE `AspNetUsers`
                          ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId`
                          FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`)
                          ON DELETE RESTRICT,
                          ALGORITHM=COPY;
                    END IF;
                END;

                CALL __sgvApplyD7();
                DROP PROCEDURE __sgvApplyD7;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only por diseño. Revertir reintroduce el
            // soft-delete y exige la migración AddSoftDeleteToAspNetUsers
            // original, que ya no aplica al schema post-D7 (la unicidad
            // vuelve a ser plana y las columnas generadas no se
            // restauran sin perder datos). Una reversión real requiere
            // una migración correctiva explícita.
            throw new NotSupportedException(
                "Migración forward-only. Para revertir, escribir una migración correctiva explícita.");
        }
    }
}