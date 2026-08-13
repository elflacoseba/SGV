# Capability: mysql-migration-compat

## Propósito

Definir el contrato de compatibilidad entre las migraciones de EF Core de `SGV.Infraestructura` y MySQL 8.4 LTS, sin reescribir migraciones ya aplicadas en producción. Cubre la transición de columnas `GENERATED VIRTUAL` a `GENERATED STORED` cuando deben soportar `UNIQUE INDEX`, la idempotencia de la migración compensatoria contra MySQL 8.0, la coherencia del script `--idempotent` regenerado y las restricciones sobre `migrationBuilder.Sql()` cuando sus statements entran al script idempotente.

## Requisitos AGREGADOS

### Requirement: Columnas GENERATED que reciben UNIQUE INDEX DEBEN ser STORED en MySQL 8.4 LTS

El sistema DEBE garantizar que toda columna `GENERATED` sobre la que se defina un `UNIQUE INDEX` quede como `STORED` antes de crear el índice cuando el proveedor target sea MySQL 8.4 LTS. La transición de estado inicial `VIRTUAL → STORED` DEBE realizarse mediante una migración compensatoria idempotente cronológicamente posterior a la migración que creó la columna como `VIRTUAL` y a la migración que asumió la existencia del `UNIQUE INDEX` basado en ella.

- **GIVEN** la migración `InicialSgvo` creó `Personas.ActiveEmailUnique` como `GENERATED VIRTUAL`
- **WHEN** una base limpia ejecuta la cadena against MySQL 8.4 LTS
- **THEN** MySQL 8.4 DEBE rechazar la creación del `UNIQUE INDEX` sobre la columna `VIRTUAL`
- **AND** las migraciones subsiguientes que asumen `IX_Personas_ActiveEmailUnique` existente DEBEN fallar hasta que la compensatoria convierta la columna a `STORED`.

- **GIVEN** la migración compensatoria `20260728XXXXXX_MySql84ActiveEmailUniqueFix` corre contra `Personas.ActiveEmailUnique` aún `VIRTUAL` sin `UNIQUE INDEX`
- **WHEN** aplica el `ALTER ... GENERATED ALWAYS AS (...) STORED` y luego `CREATE UNIQUE INDEX`
- **THEN** la columna DEBE quedar `STORED GENERATED`
- **AND** el `UNIQUE INDEX IX_Personas_ActiveEmailUnique` DEBE existir tras la migración
- **AND** las columnas `ActiveLegajoUnique` y `ActiveDocumentoUnique` DEBEN terminar también `STORED` + `UNIQUE INDEX`.

### Requirement: Compatibilidad con MySQL 8.0 preservada

La migración compensatoria DEBE ser idempotente y no destructiva contra bases MySQL 8.0 donde la columna ya sea `STORED` o el `UNIQUE INDEX` ya exista. Esta compensatoria NO DEBE introducir regresiones en flujos ya migrados.

- **GIVEN** una DB MySQL 8.0 donde `InicialSgvo` ya creó `ActiveEmailUnique` como `VIRTUAL` con `UNIQUE INDEX` (comportamiento permitido en 8.0)
- **WHEN** la compensatoria corre
- **THEN** los `ALTER`/`CREATE INDEX` DEBEN ser no-ops o tolerantes (`IF EXISTS`/`IF NOT EXISTS` o detección de estado previo)
- **AND** la migración DEBE registrarse en `__EFMigrationsHistory` sin error.

### Requirement: Test `ScriptStandaloneSmokeMySqlFactTests` ajustado al nuevo conteo de migraciones

`ScriptStandaloneSmokeMySqlFactTests` DEBE reflejar el total de migraciones del ensamblado tras agregar la compensatoria. El contrato del test smoke cuenta migraciones declaradas en el ensamblado vs. filas esperadas en `__EFMigrationsHistory` tras aplicar el script `--idempotent`.

- **GIVEN** se agregó la migración compensatoria al ensamblado `SGV.Infraestructura`
- **WHEN** se ejecuta `ScriptStandaloneSmokeMySqlFactTests`
- **THEN** `ExpectedMigrationCount` DEBE ser `18` (antes `17`)
- **AND** el test DEBE pasar contra MySQL 8.4 LTS y contra MySQL 8.0.

### Requirement: Regeneración coherente del script `--idempotent`

El script `docs/migracion-inicial-sgv.sql` DEBE regenerarse con `dotnet ef migrations script --idempotent` tras agregar la compensatoria. El script resultante DEBE aplicar limpio contra una DB nueva sin errores y DEBE respetar `__EFMigrationsHistory`: NO DEBE reaplicar migraciones ya registradas.

- **GIVEN** la compensatoria fue agregada al ensamblado
- **WHEN** se regenera `docs/migracion-inicial-sgv.sql` con `dotnet ef migrations script --idempotent`
- **THEN** el script DEBE contener los 18 bloques de migración
- **AND** aplicado contra una DB limpia contra MySQL 8.4 LTS DEBE finalizar con las 18 filas en `__EFMigrationsHistory` y sin errores.

- **GIVEN** una DB con las primeras 17 migraciones ya en `__EFMigrationsHistory`
- **WHEN** se ejecuta el script regenerado contra esa DB
- **THEN** las 17 migraciones previas DEBAN ser saltadas (idempotente vía `__EFMigrationsHistory`)
- **AND** solo la compensatoria DEBE ejecutarse y registrarse.

### Requirement: Restricciones sobre `migrationBuilder.Sql()` en migraciones que entran a `--idempotent`

Cuando un `migrationBuilder.Sql()` contenga statements con `;` internos (prepared statements compuestos, `PREPARE`/`EXECUTE`/`DEALLOCATE`, bloques `BEGIN ... END`), el generador de script `--idempotent` de EF los envuelve en un stored procedure con `DELIMITER`, lo que rompe la sintaxis del script. Las migraciones DEBEN evitar ese patrón en statements que entran al script idempotente.

- **GIVEN** una migración invoca `migrationBuilder.Sql("PREPARE stmt FROM '...'; EXECUTE stmt; DEALLOCATE PREPARE stmt;")` u otro bloque con `;` internos
- **WHEN** el script `--idempotent` se regenera
- **THEN** el generador de EF DEBE envolver el bloque en `DELIMITER $$ ... DELIMITER ;`
- **AND** ESE patrón DEBE romper la aplicabilidad del script y DEBE evitarse en migraciones futuras.

- **GIVEN** una migración futura necesita lógica condicional multi-statement
- **WHEN** se diseña la migración
- **THEN** DEBE evitarse `migrationBuilder.Sql()` con `;` internos en statements que entran a `--idempotent`
- **AND** se DEBE preferir `migrationBuilder.Sql()` con statements únicos o lógica declarada en `Up`/`Down` que no requiera `DELIMITER`.