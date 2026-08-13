# Apply Progress — fix-mysql84-compat (archivo por paralización Fase 0)

**Change**: `fix-mysql84-compat`
**Estado final**: ⛔ Abortado en Fase 0 (validación empírica bloqueante)
**Fecha de paralización**: 2026-08-12
**Próxima fase si se reabre**: re-evaluar alcance contra target específico (MariaDB o MySQL con sql_mode distinto) donde el failure mode SÍ se reproduzca.

## Resumen

El change se diseñó con la hipótesis central:
> MySQL 8.4 LTS rechaza `CREATE UNIQUE INDEX` sobre columnas GENERATED VIRTUAL, dejando a `Personas.ActiveEmailUnique` mal definida en la migración `20260614183103_InicialSgvo`.

Esa hipótesis se marcó como **Open Question** en el design y se validó empíricamente en la Fase 0 de `sdd-apply` (task T-01). El resultado fue **opuesto al esperado**: las 17 migraciones del repo aplicaron limpias desde cero contra MySQL 8.4.11 LTS. `__EFMigrationsHistory` registró las 17, y todas las columnas `ActiveXxxUnique` quedaron como `STORED GENERATED` con sus `UNIQUE INDEX` correspondientes.

## Evidencia empírica (Fase 0 — T-01 a T-04)

### T-01 — Reproducción del failure
- DB efímera `sgv_validate_84` creada en `192.168.0.216`.
- `dotnet ef database update --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj --no-build`.
- Resultado: **Exit code 0**, "Done." Las 17 migraciones aplicaron limpias.
- Estado final de `information_schema.COLUMNS` para `Active*Unique`:

  | Tabla.Columna | EXTRA |
  |---|---|
  | Cargos.ActiveCodigoUnique | STORED GENERATED |
  | Habilidades.ActiveCodigoUnique | STORED GENERATED |
  | Ocupaciones.ActivePersonaPuestoUnique | STORED GENERATED |
  | Ocupaciones.ActivePuestoIdUnique | STORED GENERATED |
  | Personas.ActiveDocumentoUnique | STORED GENERATED |
  | **Personas.ActiveEmailUnique** | **STORED GENERATED** |
  | Personas.ActiveLegajoUnique | STORED GENERATED |
  | Postulantes.ActivePersonaIdUnique | STORED GENERATED |
  | Puestos.ActiveCodigoUnique | STORED GENERATED |
  | UnidadesOrganizativas.ActiveCodigoUnique | STORED GENERATED |
  | Vacantes.ActivePuestoIdUnique | STORED GENERATED |

  Todas las 11 columnas tienen sus `IX_*_Active*Unique` con `NON_UNIQUE=0`.

  Conclusión: **MySQL 8.4.11 LTS NO rechaza UNIQUE INDEX sobre GENERATED VIRTUAL.** Acepta el `IX_Personas_ActiveEmailUnique` en `InicialSgvo` sin error. La conversión VIRTUAL→STORED de `MariaDbStoredColumnsAndCollation` es trabajo intencional de esa migración, no del supuesto failure mode.

### T-02 — FixActivePuestoIdUniqueType (jul-11)
- Su `DropIndex` no falló porque `InicialSgvo` ya había creado los UNIQUE INDEX correctamente (validación de T-01).

### T-03 — SgvDbContextModelSnapshot
- Las columnas se describen con `b.Property<string>("...")` + `b.HasIndex("...")`. Sin flag VIRTUAL/STORED explícito; la naturaleza la resuelve la DDL de la migración.

### T-04 — Envolvimiento del script generator
- Confirmado: cada `migrationBuilder.Sql()` se envuelve en `DROP PROCEDURE → DELIMITER // CREATE PROCEDURE MigrationsScript() BEGIN IF NOT EXISTS(...) THEN ... END IF; END // DELIMITER ; CALL; DROP PROCEDURE`.
- Implicación: `mb.Sql()` con `;` internos rompe la sintaxis del BEGIN/END. **Restricción confirmada** y útil para futuros cambios (no se intentó implementar compensatoria con este patrón porque T-01 la hizo innecesaria).

## Causa probable del falso positivo inicial

La sesión que reportó el bug al usuario incluyó `dotnet ef database update` durante los intentos intermedios del orquestador (helper idempotente, luego revertido). Esos intentos dejaron `sgv_test` en estado parcialmente migrado, con la mitad de las migraciones aplicadas y la `MariaDbStoredColumnsAndCollation` aún sin aplicar. La evidencia que vio el orquestador (`Can't DROP 'IX_Personas_ActiveEmailUnique'`) provino de ese estado contaminado, no de la inicial corriendo limpia en una DB nueva.

El error real que el usuario reportó (`tests [MySqlFact] fallan`) tiene otra causa verificada independientemente: los tests heredan connection string por env var / appsettings / default `localhost`. Sin env var, apuntan a localhost apagado. Eso se resuelve exportando `ConnectionStrings__SgvDatabase='server=192.168.0.216;database=sgv_test;...'`.

## Decisión

Archivar el change. La compensatoria no tiene nada que arreglar en MySQL 8.4.11 LTS.

## Lecciones aprendidas (memoria Engram)

- `strict_tdd: true` y el guard de Open Questions en `sdd-design` evitan invertir tiempo en fixes de problemas que no existen.
- MySQL 8.4 LTS, según nuestras pruebas empíricas, **mantiene el comportamiento de MySQL 8.0**: UNIQUE INDEX sobre GENERATED VIRTUAL está permitido.
- La restricción al respecto vive en MariaDB (donde la inicial sí lo rechaza) y/o en escenarios de `sql_mode` específicos no presentes en este repo.

## Artefactos preservados

Todos los artefactos siguen en `openspec/changes/archive/2026-08-12-fix-mysql84-compat/`:
- `proposal.md`
- `design.md` (incluye la decisión D.3 vs D.2 sobre el patrón de envoltura)
- `tasks.md` (17 tasks; 4 marcadas `[OPEN]` resueltas por Fase 0)
- `specs/mysql-migration-compat/spec.md` (5 requirements, 8 scenarios)

Estos artefactos quedan como referencia por si en un target diferente (MariaDB, MySQL con `sql_mode=ANSI_SQL_SERVER`, etc.) el failure mode SÍ se reproduce y se reabre el change.

## Archivos modificados por la sesión

Cero. `git status --short` está limpio. El working tree no tiene cambios sin commitear atribuibles a este change. La sesión introdujo un commit anterior (`e5981145` con `MySql:ServerVersion` externalizable) que sigue intacto y es independiente.
