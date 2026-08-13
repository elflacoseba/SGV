# Proposal: fix-mysql84-compat

## Intent

Resolver la incompatibilidad entre la migración `InicialSgvo` (junio 2026) y MySQL 8.4.11 LTS en cuanto a la creación de columnas GENERATED VIRTUAL con índice UNIQUE. La cadena causal produce: (1) `InicialSgvo` crea `Personas.ActiveEmailUnique` como VIRTUAL sin UNIQUE INDEX, (2) `MariaDbStoredColumnsAndCollation` intenta hacer DROP de ese índice y falla, (3) los tests `[MySqlFact]` fallan contra MySQL 8.4.

## Scope

### In Scope
- Nueva migración compensatoria con timestamp `20260728XXXXXX_MySql84ActiveEmailUniqueFix` que convierte `ActiveEmailUnique` a STORED GENERATED y agrega el UNIQUE INDEX.
- Regeneración de `docs/migracion-inicial-sgv.sql` con `dotnet ef migrations script --idempotent`.
- Actualización de `ExpectedMigrationCount` de 17 a 18 en `ScriptStandaloneSmokeMySqlFactTests.cs`.
- Documentación en `docs/decisiones-implementacion.md` §6.

### Out of Scope
- Modificar `InicialSgvo.cs` (migración ya aplicada en producción).
- Reescribir el bootstrap de tests.
- Cambiar versión de `Pomelo.EntityFrameworkCore.MySql`.

## Approach

**Opción B: migración compensatoria.** Se descarta A (modificar la inicial viola la convención fuerte de no reescribir migraciones aplicadas) y C (combinar ambas introduce complejidad innecesaria sin beneficio adicional). La opción B respeta el principio de no tocar historial migracional existente y es suficiente para resolver el problema en MySQL 8.4.

La compensatoria será idempotente: detecta si la columna ya es STORED + tiene índice (MySQL 8.0) y es no-op en ese caso. Esto evita regressiones en DBs que ya estén correctamente migradas.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260728XXXXXX_MySql84ActiveEmailUniqueFix.cs` | New | Migración compensatoria |
| `docs/migracion-inicial-sgv.sql` | Modified | Regenerado con `--idempotent` |
| `tests/SGV.Tests/Persistencia/ScriptStandaloneSmokeMySqlFactTests.cs` | Modified | ExpectedMigrationCount: 17→18 |
| `docs/decisiones-implementacion.md` §6 | Modified | Contrato runtime MySQL 8.4 |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| DB productivas con 17 migraciones ya aplicadas | Low | La compensatoria es idempotente; no afecta flujos existentes |
| MySQL 8.0 no necesita la compensatoria | Low | IF EXISTS / IF NOT EXISTS en los ALTER para que sea no-op |
| Latencia server remoto (~4 min / 14 migraciones) | Med | Solo afecta tiempo de ejecución de tests; acceptable |
| Test smoke valida script pre-generado (no binario) | Med | El script regenerado contendrá la nueva migración; el test ajusta count |

## Rollback Plan

Si la migración compensatoria falla contra MySQL 8.4, ejecutar manualmente:
```sql
ALTER TABLE Personas MODIFY COLUMN ActiveEmailUnique VARCHAR(320) GENERATED ALWAYS AS (CASE WHEN DeletedAt IS NULL THEN Email END) STORED NOT NULL;
CREATE UNIQUE INDEX IX_Personas_ActiveEmailUnique ON Personas (ActiveEmailUnique) WHERE DeletedAt IS NULL;
```
Para revertir a binario: `dotnet ef migrations remove` (si no tiene código dependiente) y borrar la fila de la compensatoria en `__EFMigrationsHistory`.

## Dependencies

- MySQL 8.4.11 LTS remoto accesible en `192.168.0.216:3306`.
- ConnectionString con `Database=sgv_test` seteada en env var `ConnectionStrings__SgvDatabase`.
- `dotnet ef` CLI disponible (SDK 10.0.300).

## Success Criteria

- [ ] DB limpia nueva (DROP+CREATE) corre las 18 migraciones sin errores contra MySQL 8.4 LTS.
- [ ] `Personas.ActiveEmailUnique` queda como STORED GENERATED con UNIQUE INDEX.
- [ ] Las otras columnas `ActiveLegajoUnique` y `ActiveDocumentoUnique` también terminan como STORED GENERATED + UNIQUE INDEX.
- [ ] `ScriptStandaloneSmokeMySqlFactTests` pasa con `ExpectedMigrationCount = 18`.
- [ ] `dotnet ef database update` desde cero contra MySQL 8.4 termina con `Done.` y 18 filas en `__EFMigrationsHistory`.
- [ ] Suite `[MySqlFact]` completa pasa contra MySQL 8.4.
