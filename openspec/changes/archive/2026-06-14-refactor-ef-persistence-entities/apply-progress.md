# Apply Progress: refactor-ef-persistence-entities

**Change**: refactor-ef-persistence-entities
**Mode**: Strict TDD
**Current slice**: PR 3 (final)
**Chain strategy**: stacked-to-main (`develop` as integration base)

## Merge confirmation

Se fusionó el progreso previo de PR 1 + PR 2 desde el artifact local. Este documento conserva todas las tareas cerradas anteriormente y suma el trabajo final de PR 3 (snapshot alignment + schema-drift proof + verification gate).

## Historical note

Las tareas 1.1-2.4 ya estaban implementadas antes de que existiera este `apply-progress`, así que su evidencia TDD sigue siendo reconstruida históricamente con `tasks.md`, `verify-report.md`, los archivos vigentes y las pruebas que continúan verdes. En PR 1 también se cerraron 3.1 y 3.3. En PR 2 se completaron 3.2 y 3.4. En PR 3 se completan 4.1-4.4.

## Completed Tasks

- [x] 1.1 RED: Update `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` to assert SGV tables map to `src/SGV.Infraestructura/Persistencia/Entidades/*Entity.cs` CLR types while `AspNet*` mappings stay unchanged.
- [x] 1.2 RED: Add schema-invariant assertions in `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` for current table names, generated columns, unique indexes, FK/check-constraint semantics, and EF Core 9/Pomelo metadata.
- [x] 1.3 GREEN: Create `src/SGV.Infraestructura/Persistencia/Entidades/` with `EntityBase.cs`, `AuditableEntityBase.cs`, and `*Entity.cs` files for every SGV mapped table named in `design.md`.
- [x] 2.1 GREEN: Switch `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` and `Configuraciones/ConfiguracionComun.cs` to `DbSet<*Entity>` and Infrastructure base entities only.
- [x] 2.2 GREEN: Retarget `src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs` to persistence entities, preserving table names, column names, keys, indexes, relationships, generated-column workarounds, and Identity exclusions.
- [x] 2.3 GREEN: Update `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` to seed identical values through `NivelHabilidadEntity`, `EstadoVacanteEntity`, `EstadoPostulacionEntity`, `CargoEntity`, and `HabilidadEntity`.
- [x] 2.4 REFACTOR: Remove direct Domain-type EF usage from Infrastructure persistence files without changing Application contracts (DbContext, Configs, Seeds, and the minimum repository retargeting for a green PR 1 base are complete; audit flow remains scoped to PR 2).
- [x] 3.1 RED: Extend `tests/SGV.Tests/Persistencia/{CargoRepositoryTests.cs,HabilidadRepositoryTests.cs,UnidadOrganizativaRepositoryTests.cs,PuestoRepositoryTests.cs}` to prove ordering, filters, and `Puesto` relationship graphs remain unchanged.
- [x] 3.2 RED: Add `tests/SGV.Tests/Persistencia/AuditoriaSaveChangesInterceptorTests.cs` for create/update/soft-delete semantics, sensitive-field exclusion, and logical audit names without the `Entity` suffix.
- [x] 3.3 GREEN: Add `src/SGV.Infraestructura/Persistencia/Mapeos/*.cs` plus updates in `Repositorios/ReadOnlyRepository.cs`, `CargoRepository.cs`, `HabilidadRepository.cs`, `UnidadOrganizativaRepository.cs`, and `PuestoRepository.cs` to query `*Entity` sets and return Domain models.
- [x] 3.4 GREEN: Update `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` to track `EntityBase`/`AuditableEntityBase`, write `AuditoriaEntity`, and normalize logical entity names.
- [x] 4.1 RED/GREEN: Verify `tests/SGV.Tests/Persistencia/RepositoryTestData.cs` already uses `*Entity` types — confirmado en PR 1/2, no requiere cambios adicionales.
- [x] 4.2 GREEN: Actualizar `SgvDbContextModelSnapshot.cs` — se regeneró vía `dotnet ef migrations add __SnapshotSync` (Up/Down vacíos, schema invariante); se descartaron los archivos de migración temporales y se conservó el snapshot actualizado.
- [x] 4.3 VERIFY: Agregar `Migraciones_SnapshotUsaTiposEntityYNoDominio` (prueba canaria de CLR-type en snapshot, no requiere MySQL) + `Migraciones_ScriptIdempotenteNoGeneraDDL` (verifica que `IMigrator.GenerateScript` deltas no contengan DDL).
- [x] 4.4 VERIFY: `dotnet test` pasa 77/77 (modelo 14 + repositorios 12 + auditoría 4 + schema-drift 2 + otras).

## Files Changed In PR 1

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Created | Agregó el mapeo explícito mínimo `*Entity -> Dominio` para `Cargo`, `Habilidad`, `UnidadOrganizativa` y `Puesto`. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/ReadOnlyRepository.cs` | Modified | Reemplazó `Context.Set<TDomain>()` por una base genérica `TPersistence/TDomain` que consulta entidades EF y mapea a Dominio. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/{CargoRepository,HabilidadRepository,UnidadOrganizativaRepository,PuestoRepository}.cs` | Modified | Retargeteó los repositorios a `DbSet<*Entity>` y preservó filtros, orden e includes antes de mapear a Dominio. |
| `tests/SGV.Tests/Persistencia/{CargoRepositoryTests,HabilidadRepositoryTests,UnidadOrganizativaRepositoryTests,PuestoRepositoryTests}.cs` | Modified | Extendió las pruebas para verificar explícitamente que el boundary devuelve tipos de Dominio y mantiene relaciones en `Puesto`. |
| `openspec/changes/refactor-ef-persistence-entities/tasks.md` | Modified | Ajustó el límite real del PR 1, marcó tareas completadas y registró `stacked-to-main`. |

## Files Changed In PR 2

| File | Action | What Was Done |
|------|--------|---------------|
| `tests/SGV.Tests/Persistencia/AuditoriaSaveChangesInterceptorTests.cs` | Created | Agregó regresiones de alta, modificación, baja lógica, exclusión de campos sensibles y preservación del nombre lógico sin suffix `Entity`. |
| `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` | Modified | Retargeteó el interceptor a `EntityBase`/`AuditableEntityBase`, persistió `AuditoriaEntity` y normalizó `EntityName`. |
| `openspec/changes/refactor-ef-persistence-entities/tasks.md` | Modified | Marcó como completadas únicamente las tareas 3.2 y 3.4 del slice PR 2. |
| `openspec/changes/refactor-ef-persistence-entities/apply-progress.md` | Modified | Fusionó el progreso acumulado de PR 1 con la nueva evidencia de PR 2. |

## Files Changed In PR 3

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Modified | Snapchot regenerado vía `dotnet ef migrations add __SnapshotSync`; todos los CLR types migraron de `SGV.Dominio.*` a `SGV.Infraestructura.Persistencia.Entidades.*Entity`. Up/Down vacíos, schema sin cambios. |
| `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Modified | Agregó `Migraciones_SnapshotUsaTiposEntityYNoDominio` (canaria CLR-type en snapshot, no requiere MySQL) y `Migraciones_ScriptIdempotenteNoGeneraDDL` (prueba de script delta, requiere MySQL). |
| `openspec/changes/refactor-ef-persistence-entities/tasks.md` | Modified | Marcó 4.1-4.4 como completadas. |
| `openspec/changes/refactor-ef-persistence-entities/apply-progress.md` | Modified | Fusionó el progreso de PR 3 con el acumulado de PR 1 + PR 2. |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Integration | ⚠️ Histórica | ✅ Existía antes de esta ejecución | ✅ 14/14 | ✅ Múltiples escenarios del modelo | ⚠️ Evidencia reconstruida |
| 1.2 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Integration | ⚠️ Histórica | ✅ Existía antes de esta ejecución | ✅ 14/14 | ✅ Múltiples invariantes de esquema | ⚠️ Evidencia reconstruida |
| 1.3 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Integration | ⚠️ Histórica | ✅ Cubierta por 1.1/1.2 | ✅ 14/14 | ✅ Tipos + metadata | ⚠️ Evidencia reconstruida |
| 2.1 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Integration | ⚠️ Histórica | ✅ Cubierta por pruebas de modelo existentes | ✅ 14/14 | ✅ Tipos EF/Identity diferenciados | ⚠️ Evidencia reconstruida |
| 2.2 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Integration | ⚠️ Histórica | ✅ Cubierta por pruebas de metadata existentes | ✅ 14/14 | ✅ FK/check/index/generadas | ⚠️ Evidencia reconstruida |
| 2.3 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Integration | ⚠️ Histórica | ✅ Cubierta por pruebas de modelo/seed existentes | ✅ 14/14 | ✅ Seed + modelo | ⚠️ Evidencia reconstruida |
| 2.4 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Integration | ⚠️ Histórica | ✅ La incompatibilidad remanente quedó explicitada por el suite de repositorios | ✅ 71/71 luego del boundary mínimo | ✅ Modelo + repositorios | ✅ Base genérica `TPersistence/TDomain` |
| 3.1 | `tests/SGV.Tests/Persistencia/{CargoRepositoryTests,HabilidadRepositoryTests,UnidadOrganizativaRepositoryTests,PuestoRepositoryTests}.cs` | Integration | ✅ `ModeloPersistenciaTests` 14/14 | ✅ Se extendieron las pruebas para exigir tipos de Dominio y relaciones | ✅ 12/12 | ✅ Listado, filtros, orden, `GetById`, relaciones | ✅ Ajustes mínimos de aserciones explícitas |
| 3.2 | `tests/SGV.Tests/Persistencia/AuditoriaSaveChangesInterceptorTests.cs` | Integration | ✅ 71/71 | ✅ 0/4 (interceptor no auditaba `*Entity`) | ✅ 4/4 | ✅ Alta + modificación + baja lógica + sensible + nombre lógico | ➖ Sin refactor extra |
| 3.3 | `tests/SGV.Tests/Persistencia/{CargoRepositoryTests,HabilidadRepositoryTests,UnidadOrganizativaRepositoryTests,PuestoRepositoryTests}.cs` | Integration | ✅ Suite rojo conocido | ✅ 12 fallos por tipos no mapeados | ✅ 12/12 y 71/71 | ✅ Cuatro repositorios + grafo `Puesto` | ✅ Extrajo boundary mínimo a `PersistenceToDomainMapper` |
| 3.4 | `tests/SGV.Tests/Persistencia/AuditoriaSaveChangesInterceptorTests.cs` | Integration | ✅ 0/4 rojo | ✅ Las pruebas nuevas quedaron rojas | ✅ 4/4 y 75/75 | ✅ `CargoEntity` + `SensitiveAuditEntity` forzaron nombre lógico y filtrado | ✅ Extrajo `ObtenerNombreLogicoEntidad` |
| 4.1 | `tests/SGV.Tests/Persistencia/RepositoryTestData.cs` | N/A | — | Ya implementado en PR 1/2 | Ya usa `*Entity` | — | — |
| 4.2 | `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | N/A | ✅ Build + 75/75 | `dotnet ef` detectó cambios CLR | ✅ `dotnet ef migrations add __SnapshotSync` → Up/Down vacíos | ✅ Snapshot migrado a `*Entity`, 0 referencias `SGV.Dominio.*` | ✅ Se descartaron los archivos de migración temporales |
| 4.3 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Unit+Integration | ✅ 75/75 baseline | ✅ `Migraciones_SnapshotUsaTiposEntityYNoDominio` verifica CLR-types en snapshot | ✅ `dotnet test` → 77/77 | ✅ `Migraciones_ScriptIdempotenteNoGeneraDDL` verifica script sin DDL | ➖ Sin refactor extra |
| 4.4 | — | — | — | — | ✅ `dotnet test` → 77/77 | — | — |

## Test Summary

- **Total tests written in PR 3**: 2 pruebas nuevas (snapshot CLR-type check + script delta DDL check)
- **Total tests passing**: 77/77 en `dotnet test`
- **Layers used**: Unit (1 snapshot check) + Integration (76 including MySQL-dependent tests)
- **Approval tests**: Snapshot entity-type check validates snapshot integrity; script delta test validates no DDL drift
- **Pure functions created**: 0 nuevas en PR 3

## Deviations from Design

- Ninguna. PR 3 ejecutó exactamente lo planificado: alineación de snapshot, prueba canaria de schema drift, y verificación final.

## Issues Found

- `IMigrationsModelDiffer.GetDifferences` requiere modelos relacionales finalizados, lo que hizo inviable una prueba directa de diff. Se reemplazó con dos pruebas: una de verificación de CLR-type en snapshot (sin MySQL) y otra de script delta (con MySQL).

## Remaining Tasks

Ninguna. Las 15/15 tareas están completas.

## Workload / PR Boundary

- **Mode**: stacked PR slice (final)
- **Current work unit**: PR 3 snapshot alignment + schema-drift proof + final verification
- **Boundary**: parte desde la base verde de PR 2 y termina con el cambio completo verificado.
- **Estimated review budget impact**: slice acotado a snapshot regenerado + 2 pruebas nuevas + artefactos OpenSpec.

## Status

15/15 tasks complete. Ready for final verify and archive.
