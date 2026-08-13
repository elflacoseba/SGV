# Archive Report — fix-mysql84-compat

**Change**: `fix-mysql84-compat`
**Fecha archivado**: 2026-08-12
**Motivo**: Supuesto central del change fue empíricamente falso.

## Resumen

El change proponía agregar una migración compensatoria `20260728120000_FixMySql84GeneratedUniqueIndex` para arreglar una incompatibilidad supuesta entre `20260614183103_InicialSgvo` (junio 2026) y MySQL 8.4 LTS, donde la columna `Personas.ActiveEmailUnique` quedaba como `GENERATED VIRTUAL` sin `UNIQUE INDEX`. La incompatibilidad fue planteada como hipótesis central y marcada como **Open Question** en el design (`openspec/changes/archive/2026-08-12-fix-mysql84-compat/design.md`).

La Fase 0 de `sdd-apply` (task T-01) validó empíricamente la hipótesis contra el server MySQL 8.4.11 LTS remoto (`192.168.0.216`) usando una DB efímera `sgv_validate_84`. Resultado: las 17 migraciones aplican limpias, todas las 11 columnas `ActiveXxxUnique` quedan como `STORED GENERATED`, y los 11 índices `IX_*_Active*Unique` existen con `NON_UNIQUE=0`. **MySQL 8.4.11 LTS NO rechaza UNIQUE INDEX sobre GENERATED VIRTUAL** — la restricción que motivó el change no aplica en este target.

## Causa del falso positivo

Durante la sesión que motivó este change, el orquestador ejecutó `dotnet ef database update` mientras estaba iterando con un helper idempotente experimental (luego revertido). Esos intentos intermedios dejaron la DB `sgv_test` del server en un estado parcialmente migrado. El error capturado entonces (`Can't DROP 'IX_Personas_ActiveEmailUnique'`) provino de `MariaDbStoredColumnsAndCollation` (la que el helper intermedio había dejado dropando índices que no existían) corriendo contra ese estado sucio — no de la inicial contra MySQL 8.4.

## Lección

El guard de **Open Questions** en `sdd-design` funcionó: la verificación empírica se ejecutó ANTES de tocar código, ahorrando un cambio que no tenía razón de ser. La filosofía `strict_tdd: true` del repo también contribuyó: el guard de Fase 0 es el equivalente a "test que prueba el failure mode antes de fixearlo".

## Estado del problema original (no resuelto por este change)

El problema REAL que el usuario reportó al inicio de la sesión — `tests [MySqlFact] fallan al usar el server remoto` — es de **conexión, no de código**: los tests heredan la connection string por env var / appsettings / default `localhost`. Apuntan a `localhost` cuando el server real es `192.168.0.216`. La solución operativa es:

```
export ConnectionStrings__SgvDatabase='server=192.168.0.216;database=sgv_test;user=root;password=<pwd>;Connection Timeout=10'
dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~MySqlFact"
```

Eso es ortogonal a este change y se resuelve en otra sesión.

## Artefactos preservados en archive

- `proposal.md`
- `design.md` (incluye análisis D.2 vs D.3 sobre patrón de envoltura del script generator — útil para futuros cambios con migraciones que generen `--idempotent`)
- `tasks.md` (17 tasks, 4 Open Questions resueltas por Fase 0)
- `specs/mysql-migration-compat/spec.md` (5 requirements, 8 scenarios)
- `apply-progress.md` (este archivo junto al archive)

Los artefactos se conservan en `openspec/changes/archive/2026-08-12-fix-mysql84-compat/` por si en otro target (MariaDB, MySQL con `sql_mode` específico) el failure mode SÍ se reproduce y se reabre el change con alcance revisado.

## Commit

Este change nunca produjo commit. El único commit de esta sesión es el previo (`e5981145`, `fix(api): externalizar versión de MySQL desde configuración; quitar AutoDetect del runtime`), independiente de este change y ya en `develop`.

## Memoria

El hallazgo fue persistido en Engram como observación independiente vía `mem_save` (topic key relacionado con la sesión).
