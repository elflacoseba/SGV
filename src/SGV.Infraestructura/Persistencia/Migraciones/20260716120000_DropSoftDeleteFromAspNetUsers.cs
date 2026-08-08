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
    /// <b>Reescrito en #263:</b> la versión previa
    /// usaba un stored procedure <c>__sgvApplyD7</c> anidado dentro
    /// del procedure <c>MigrationsScript</c> que EF Core genera para
    /// <c>dotnet ef migrations script --idempotent</c>. MySQL rechaza
    /// <c>DROP/CREATE PROCEDURE</c> dentro de otro stored routine con
    /// <c>ERROR 1357 (Can't drop or alter a PROCEDURE from within
    /// another stored routine)</c>. La solución ejecuta los diez
    /// pasos en SQL directo, gated por un <c>@needsD7</c> booleano
    /// derivado de <c>information_schema.COLUMNS</c> y ejecutado vía
    /// <c>PREPARE</c>/<c>EXECUTE</c>/<c>DEALLOCATE PREPARE</c>.
    /// </para>
    /// <para>
    /// <b>Preflight fail-loud:</b> el SIGNAL custom de la versión
    /// previa se reemplaza por un <c>ADD UNIQUE INDEX</c> temporal
    /// sobre <c>PersonaId</c> que actúa como preflight natural: si
    /// existen duplicados activos, MySQL devuelve <c>ERROR 1062</c>
    /// y aborta el script antes de cualquier operación destructiva.
    /// El índice temporal se dropea en el paso 8 y se recrea
    /// como canónico en el paso 9. La pérdida del mensaje custom
    /// se compensa por la barrera natural del UNIQUE INDEX, suficiente
    /// para el criterio end-to-end del script.
    /// </para>
    /// </remarks>
    public partial class DropSoftDeleteFromAspNetUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ────────────────────────────────────────────────────────────
            // Reentrancia (#263): un segundo run contra un schema post-D7
            // (IsDeleted ya no existe) debe ser un no-op. El EF Core wrapper
            // MigrationsScript ya gatea por __EFMigrationsHistory, pero
            // defendemos también contra filas huérfanas en el historial
            // (ej. edición manual) chequeando information_schema.COLUMNS
            // y usando PREPARE/EXECUTE con un SQL condicional.
            //
            // El orden de los 10 pasos respeta el design D7:
            //  (1) preflight ADD UNIQUE INDEX temporal sobre PersonaId
            //      (falla con 1062 si hay duplicados activos);
            //  (2) backfill IsDeleted=1 → LockoutEnd futuro;
            //  (3) DROP FK;
            //  (4) DROP INDEX ActiveUserNameUnique;
            //  (5) DROP INDEX ActivePersonaIdUnique;
            //  (6) DROP COLUMN ActiveUserNameUnique/ActivePersonaIdUnique/IsDeleted;
            //  (7) DROP INDEX PersonaId (no-único vigente desde
            //      AddSoftDeleteToAspNetUsers paso 3);
            //  (8) DROP INDEX temporal preflight;
            //  (9) ADD UNIQUE INDEX PersonaId (canónico, mismo nombre);
            // (10) ADD CONSTRAINT FK PersonaId RESTRICT.
            // ────────────────────────────────────────────────────────────
            migrationBuilder.Sql(
                """
                -- Bandera de reentrancia: 1 si la columna IsDeleted existe
                -- (estado pre-D7); 0 si ya fue dropeada (estado post-D7).
                SET @needsD7 := (
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE table_schema = DATABASE()
                      AND table_name = 'AspNetUsers'
                      AND column_name = 'IsDeleted'
                );

                -- Paso 1: preflight fail-loud vía ADD UNIQUE INDEX temporal
                -- sobre PersonaId. Si hay duplicados activos, MySQL devuelve
                -- ERROR 1062 y aborta el script antes de cualquier mutación
                -- destructiva. Reemplaza al SIGNAL SQLSTATE custom previo
                -- (no disponible fuera de un stored procedure).
                SET @step1 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        ADD UNIQUE INDEX `__sgvD7_PreflightUnique` (`PersonaId`),
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step1Stmt FROM @step1;
                EXECUTE step1Stmt;
                DEALLOCATE PREPARE step1Stmt;

                -- Paso 2: backfill IsDeleted=1 → LockoutEnd futuro.
                SET @step2 := IF(@needsD7 > 0,
                    'UPDATE `AspNetUsers`
                        SET `LockoutEnabled` = 1,
                            `LockoutEnd` = ''9999-12-31 23:59:59.999999''
                        WHERE `IsDeleted` = 1',
                    'DO 0');
                PREPARE step2Stmt FROM @step2;
                EXECUTE step2Stmt;
                DEALLOCATE PREPARE step2Stmt;

                -- Paso 3: DROP FK (metadata-only, INPLACE).
                SET @step3 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        DROP FOREIGN KEY `FK_AspNetUsers_Personas_PersonaId`,
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step3Stmt FROM @step3;
                EXECUTE step3Stmt;
                DEALLOCATE PREPARE step3Stmt;

                -- Paso 4: DROP INDEX ActiveUserNameUnique.
                SET @step4 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        DROP INDEX `IX_AspNetUsers_ActiveUserNameUnique`,
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step4Stmt FROM @step4;
                EXECUTE step4Stmt;
                DEALLOCATE PREPARE step4Stmt;

                -- Paso 5: DROP INDEX ActivePersonaIdUnique.
                SET @step5 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        DROP INDEX `IX_AspNetUsers_ActivePersonaIdUnique`,
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step5Stmt FROM @step5;
                EXECUTE step5Stmt;
                DEALLOCATE PREPARE step5Stmt;

                -- Paso 6: DROP COLUMNs (incluye IsDeleted, libera columnas
                -- generadas STORED referenciadas por los índices dropeados).
                SET @step6 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        DROP COLUMN `ActiveUserNameUnique`,
                        DROP COLUMN `ActivePersonaIdUnique`,
                        DROP COLUMN `IsDeleted`,
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step6Stmt FROM @step6;
                EXECUTE step6Stmt;
                DEALLOCATE PREPARE step6Stmt;

                -- Paso 7: DROP INDEX PersonaId no-único vigente desde la
                -- migración AddSoftDeleteToAspNetUsers paso 3. Este índice
                -- fue creado como no-único para liberar la unicidad que
                -- originalmente sostenía la FK; ahora lo reemplazamos por
                -- una versión UNIQUE.
                SET @step7 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        DROP INDEX `IX_AspNetUsers_PersonaId`,
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step7Stmt FROM @step7;
                EXECUTE step7Stmt;
                DEALLOCATE PREPARE step7Stmt;

                -- Paso 8: DROP INDEX temporal preflight.
                SET @step8 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        DROP INDEX `__sgvD7_PreflightUnique`,
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step8Stmt FROM @step8;
                EXECUTE step8Stmt;
                DEALLOCATE PREPARE step8Stmt;

                -- Paso 9: ADD UNIQUE INDEX PersonaId (canónico, mismo
                -- nombre que el índice temporal — el DROP/CREATE atómico
                -- podría optimizarse, pero rompería la barrera del
                -- preflight fail-loud).
                SET @step9 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        ADD UNIQUE INDEX `IX_AspNetUsers_PersonaId` (`PersonaId`),
                        ALGORITHM=INPLACE, LOCK=NONE',
                    'DO 0');
                PREPARE step9Stmt FROM @step9;
                EXECUTE step9Stmt;
                DEALLOCATE PREPARE step9Stmt;

                -- Paso 10: ADD CONSTRAINT FK PersonaId RESTRICT.
                -- ALGORITHM=COPY porque MySQL 8 no permite INPLACE para
                -- ADD CONSTRAINT sobre FKs en este contexto.
                SET @step10 := IF(@needsD7 > 0,
                    'ALTER TABLE `AspNetUsers`
                        ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId`
                        FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`)
                        ON DELETE RESTRICT,
                        ALGORITHM=COPY',
                    'DO 0');
                PREPARE step10Stmt FROM @step10;
                EXECUTE step10Stmt;
                DEALLOCATE PREPARE step10Stmt;
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