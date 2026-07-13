# Archive Report — Refactor de `PersistenceToDomainMapper` para eliminar reflexión (issue #124)

> **Change**: `2026-07-13-fix-124-persistence-mapper-reconstitute`
> **Issue GitHub**: #124 — Mapper de persistencia muta entidades de dominio mediante reflexión
> **Branch**: `fix/124-persistence-mapper-reconstitute` (base: `develop @ 1fb2d391`)
> **Modo**: híbrido (Engram + filesystem). Documentos en español.
> **Strict TDD**: ACTIVO (`strict_tdd: true`).
> **`size:exception`**: aprobada por maintainer para 506 LoC forecast; LoC delta real **1310** documentado en §6.
> **Single PR cohesivo**, NO chained.

---

## Archive Metadata

| Field | Value |
|---|---|
| Change name | `2026-07-13-fix-124-persistence-mapper-reconstitute` |
| Issue GitHub | #124 |
| Branch | `fix/124-persistence-mapper-reconstitute` (base `develop @ 1fb2d391`) |
| Status final | **PASS WITH WARNINGS** (verdict del `verify-report.md §9`) |
| Verdict from verify | PASS WITH WARNINGS — 0 CRITICAL, 1 MEDIUM, 2 LOW, 3 SUGGESTION |
| Override / repair | Ninguno. Las 40 tasks de `tasks.md` están marcadas `- [x]`. Los 2 fallos WebIntegration son pre-existentes en `develop` (verificados con checkout temporal — no son regresión). |
| Commits de implementación | 8 commits atómicos (`ebd10db0` → `06458c94`, ver §3) |
| Total LoC delta real | **+1519 / -209 = 1310 changed lines** vs `develop` |
| `size:exception` | aprobado (`design.md §11`) |
| Forecast original | ~506 LoC (producción ~250 + tests ~256) |
| Desvío | **+159%** documentado en `verify-report.md §6` |
| Fecha de cierre | 2026-07-13 |
| Artifact store | híbrido (OpenSpec filesystem + Engram) |

---

## 1. Resumen ejecutivo

El change **#124** cierra de extremo a extremo en 8 fases SDD (`explore → propose → design → tasks → apply → verify → archive`). Se eliminó por completo el helper `SetProperty<T>` (12 call sites) y `using System.Reflection;` del `PersistenceToDomainMapper`. Las **6 entidades principales** (`Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion`, `UnidadOrganizativa`) ahora exponen `internal static Reconstitute(...)` con setters tipados. `UnidadOrganizativa` migró del patrón `with`-returning a `void`-return mutators + `private set` para paridad total con las otras 5 entidades. Se introdujeron **5 tests IL estructurales nuevos** que detectan reintroducción del helper de reflexión (junto con el de `UnidadOrganizativa` pre-existente, suman **6 IL guards verdes**). Build limpio (0 errores, 0 warnings nuevos sobre `develop`). `git status` sobre `Migraciones/` clean — sin migraciones EF Core nuevas. LoC delta real 1310 vs forecast 506 (+159%), pero `size:exception` aprobada explícitamente por el maintainer para mantener alcance completo (recortar tests reduciría ROI de la red IL contra `PropertyInfo.SetValue`).

**Recomendación**: SDD cycle complete. Próxima fase = `none` (lista para merge contra `develop` y nuevo change).

---

## 2. Artefactos generados (cycle SDD completo)

| Fase | Artefacto filesystem | Engram Obs ID | Topic key |
|---|---|---|---|
| preflight | — | #1041 | (sin topic) |
| (pattern) | — | #1042 | (sin topic) |
| explore | `openspec/changes/2026-07-13-fix-124-persistence-mapper-reconstitute/exploration.md` | #1043 | `sdd/resuelve la issue #124/explore` |
| propose | `proposal.md` | #1045 | `sdd/resuelve la issue #124/proposal` |
| design | `design.md` | #1046 | `sdd/resuelve la issue #124/design` |
| tasks | `tasks.md` | #1047 | `sdd/resuelve la issue #124/tasks` |
| apply | `apply-progress.md` | #1048 | `sdd/resuelve la issue #124/apply-progress` |
| verify | `verify-report.md` | #1049 | `sdd/resuelve la issue #124/verify-report` |
| archive | `archive-report.md` (este archivo) | (próximo ID disponible) | `sdd/resuelve la issue #124/archive-report` |

Todas las observaciones Engram son `architecture` con `capture_prompt: false` (artefactos automatizados).

---

## 3. Commits de implementación (8 atómicos)

| # | SHA | Mensaje |
|---|---|---|
| CU-1 | `ebd10db0` | `test(mapper): add IL reflection guards for 5 entities (issue #124)` |
| CU-2 | `24b1d684` | `feat(mapper): add Habilidad.Reconstitute and wire persistence mapper (issue #124)` |
| CU-3 | `3387c2e0` | `feat(mapper): add Cargo.Reconstitute and wire persistence mapper (issue #124)` |
| CU-4 | `12164e07` | `feat(mapper): add Ocupacion.Reconstitute and wire persistence mapper (issue #124)` |
| CU-5 | `c53dba5d` | `feat(mapper): add Persona.Reconstitute and wire persistence mapper (issue #124)` |
| CU-6 | `2a447feb` | `feat(mapper): add Puesto.Reconstitute and wire persistence mapper (issue #124)` |
| CU-7 | `e4513036` | `refactor(organizacion): migrate UnidadOrganizativa to Reconstitute + void mutators (issue #124)` |
| CU-8 | `06458c94` | `refactor(mapper): drop SetProperty helper and System.Reflection (issue #124)` |

HEAD actual: `06458c94`. Cada commit es atómico (producción + tests en el mismo cambio) y mantiene el repo compilando.

---

## 4. Decisiones cerradas por el usuario (7)

Síntesis de `design.md §Decisiones de arquitectura` + `design.md:5` (product decisions):

| # | Decisión | Choice | Alternativa descartada | Rationale |
|---|---|---|---|---|
| 1 | **Forma del factory** | `internal static Reconstitute(...)` | `public factory` o `init`-only con `with` | `internal` mantiene encapsulación; `InternalsVisibleTo` ya es patrón vigente en Infraestructura. |
| 2 | **Alcance UO** | Migrar también a `Reconstitute` (paridad total) | Dejar UO con `with` y demás con `private set` | El usuario cerró paridad total; mantiene un único patrón mental en el codebase. |
| 3 | **Persona document fields** | `Persona.Reconstitute` acepta `telefono` / `tipoDocumento` / `numeroDocumento` explícitos no-nullable y los asigna vía `private set` | Agregar setters externos o cambiar ctor primario | Evita modificar el contrato público de creación de `Persona`; el factory es la **única** vía de hidratación. |
| 4 | **Validación dentro del factory** | Replicar invariantes del ctor primario dentro de `Reconstitute` | Delegar al ctor primario y luego `private set` | Asignar con `private set` evita duplicar asignaciones; el factory controla el orden canónico. |
| 5 | **Tests IL estructurales** | 5 nuevos, uno por entidad afectada | Mantener solo el de UO | `strict_tdd: true` lo exige; replican el patrón vigente de `UnidadOrganizativaRepositoryTests.cs:984-1045`. |
| 6 | **Reescritura UO mutadores** | `void`-return con `private set` | Mantener `with`-return | Paridad con las otras 5 entidades (todas `void`-return). Impacto acotado a `UnidadOrganizativaServicioComandos.cs` + 4 tests. |
| 7 | **Documentación diferida** | `docs/decisiones-implementacion.md` solo en `archive-report` | Actualizar en este change | Decisión del usuario; mantiene el change acotado a código + tests. **(Resuelta en §11 de este archive — sección "Inmutabilidad de Codigo en UnidadOrganizativa" ampliada.)** |

Adicional — invariante `Cargo.Desactivar` con `_puestos` activos: **fuera de scope**; abrir issue aparte para endurecer.

---

## 5. Specs Sincronizados

**Ninguno.**

El refactor cumple **textualmente** `Observable Persistence Invariants` de `sgv-persistence-architecture`: schema idéntico, contratos idénticos, comportamiento de repositorio idéntico. No agrega, modifica ni remueve ninguna capability. No requiere delta spec.

| Domain | Action | Details |
|---|---|---|
| — | **No aplica** | El refactor preserva todas las invariantes observables; no introduce capacidades nuevas ni modifica las existentes. |

> **Nota**: `openspec/changes/2026-07-13-fix-124-persistence-mapper-reconstitute/specs/` no existe porque no se generaron specs/deltas en este change (el `proposal.md §Capabilities` declara explícitamente "New Capabilities: None" y "Modified Capabilities: None"). Esto es consistente con la convención OpenSpec: deltas solo son necesarias cuando hay cambio de contrato observable.

---

## 6. Riesgos y mitigaciones (resumen del verify)

| Sev | Finding | Estado | Acción |
|---|---|---|---|
| CRITICAL | — | — | Ninguno. |
| MEDIUM | **M-1** — 2 tests WebIntegration pre-existentes fallan en change y en `develop` HEAD | Warning, no blocker | No requiere fix en este change. Abrir issue aparte para hardening de setup local de `WebApplicationFactory` (probablemente falta `dotnet user-secrets set "ConnectionStrings__SgvDatabase" ...`). |
| LOW | **L-1** — `InternalsVisibleTo("SGV.Infraestructura")` adicional no listado en `design.md §4` | Resuelto en apply (CU-2) | Documentado como desviación #1 en `apply-progress.md §3`. Atributo agregado en `SGV.Dominio.csproj:12-14`. |
| LOW | **L-2** — `Codigo_EsInmutableTrasCreacion` cambió de chequeo de `IsExternalInit` a "setter NO público" | Resuelto en apply (CU-7) | Documentado como desviación #2 en `apply-progress.md §3`. Invariante de negocio se preserva — la implementación cambió de `init` a `private set`, no la semántica. |
| SUGGESTION | **S-1** — Centralizar `InternalsVisibleTo` en `Directory.Build.props` | No requerido | Limpieza para change/cleanup aparte. |
| SUGGESTION | **S-2** — `[SuppressMessage]` para CS8524 en `ErrorCategoriaMappers.cs` | No requerido | Warning pre-existente en `develop`. |
| SUGGESTION | **S-3** — Incrementar `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS` para `WebIntegrationFixtureBootstrapCleanupTests` | No requerido | Vinculado a M-1 (setup local). |

---

## 7. Lessons learned

### 7.1 Qué salió bien

- **Patrón IL-walk reutilizable**: el test estructural de `UnidadOrganizativa` se extendió limpiamente a 5 entidades nuevas. Patrón guardado como Engram #1042 para futuras referencias.
- **Refactor estrictamente local**: Dominio + Infraestructura/Mapeos + tests. **Cero blast radius** sobre migraciones, schema, contratos HTTP, shell web, ni auditoría. `git status` sobre `Migraciones/` clean.
- **TDD estricta cumplida**: 8 ciclos RED→GREEN→REFACTOR ejecutados. Verificación IL guard como red de seguridad contra reintroducción de `PropertyInfo.SetValue`.
- **Paridad de patrón**: las 6 entidades ahora comparten el mismo shape (`internal Reconstitute` + `private set` + `void`-mutators para UO). El equipo ya no necesita recordar "UO es distinto".
- **Documentación XML exhaustiva**: cada `Reconstitute` y cada IL guard justifica decisiones de diseño. ~30% más de líneas por archivo, pero reduce preguntas en review.

### 7.2 Qué se haría diferente

- **Forecast de LoC subestimado por 159%** (506 → 1310):
  - **Tests más verbosos**: forecast asumió 4-5 tests por entidad con `[Theory]`+`InlineData]`; realidad fue 4-6 `[Fact]` por entidad con casos diferenciados (round-trip, IsActive=false, nav null, nav hydrated, validación de shape, invariantes audit). Verboso pero más robusto.
  - **XML doc comments**: no cuantificados en forecast. Añaden ~30% de líneas por archivo sin funcionalidad nueva.
  - **`UnidadOrganizativaReconstituteTests.cs` separado**: en lugar de ampliar `UnidadOrganizativaRepositoryTests.cs` (99 LoC nuevas).
  - **Tests UO migración no listados**: `UnidadOrganizativaTests.cs` requirió 34 LoC modificadas (no listadas en `design.md §9`); el blast radius real fue mayor al anticipado.
  - **Lección**: en próximos refactors similares, multiplicar forecast de tests por **2.5x** y agregar ~25% extra para XML docs.

- **UO migration más grande de lo anticipado**: la reescritura de `UnidadOrganizativa` de `with` a `void` mutators fue **atómica** y rompió compilación si se aplicaba en pasos intermedios. Esto se documentó como `CU-7` en `tasks.md`. El blast radius de UO consumers (`UnidadOrganizativaServicioComandos.cs` + 4 tests) fue subestimado en el `design.md §7.4`. Grep exhaustivo durante apply detectó sitios adicionales (`UnidadOrganizativaTests.cs` + `UnidadOrganizativaRepositoryTests.cs`).

- **`InternalsVisibleTo` transitividad olvidada en diseño**: `design.md §4` solo listaba `SGV.Tests`. La realidad es que el mapper vive en `SGV.Infraestructura` y también necesita visibilidad → `InternalsVisibleTo` no es transitivo entre assemblies de Clean Architecture. Detectado en apply (CU-2) cuando `error CS0117: 'Habilidad' does not contain a definition for 'Reconstitute'` apareció. Documentado como desviación #1.

### 7.3 Lo que sorprendió

- **`Cargo.Desactivar` invariante `_puestos` activos sigue silenciada**: hoy, la reflexión podía reconstituir `Cargo` con `IsActive=false` sin disparar la validación de `Desactivar()` (que verifica que no haya puestos subordinados activos). El cambio preserva ese comportamiento (decisión cerrada por el usuario: fuera de scope). **Riesgo latente** que debe abordarse en issue aparte.
- **Test existente `Codigo_EsInmutableTrasCreacion` chequeaba implementación, no invariante**: el test verificaba el modifier `IsExternalInit`, no la invariante de negocio. Tras migrar UO a `private set`, el test tuvo que reformularse para verificar "setter NO público" en vez del modifier específico. La invariante se preserva, pero el test era frágil al detalle de implementación.

---

## 8. Non-goals cumplidos

| Non-goal (de `proposal.md`) | Estado | Evidencia |
|---|---|---|
| No migrar a `record init` total | ✅ Cumplido | 6 entidades con `internal Reconstitute`; ninguna migración a `record init` total. |
| No tocar `DomainToPersistenceMapper` | ✅ Cumplido | `git status -- src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` = no tocado. |
| No tocar clases `*Entity` | ✅ Cumplido | `git status -- src/SGV.Infraestructura/Persistencia/Entidades/` = no tocado. |
| No tocar migraciones EF Core | ✅ Cumplido | `git status -- src/SGV.Infraestructura/Persistencia/Migraciones/` = clean. |
| No tocar `AuditoriaSaveChangesInterceptor` | ✅ Cumplido | `git status -- src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` = no tocado. |
| No tocar contratos HTTP (`SGV.Contracts`) | ✅ Cumplido | `git status -- src/SGV.Contracts/` = no tocado. |
| No tocar shell web (`SGV.Web`) | ✅ Cumplido | `git status -- src/SGV.Web/` = no tocado. |
| No endurecer `Cargo.Desactivar()` | ✅ Cumplido | Comportamiento idéntico al pre-change. Documentado en §9 (pending follow-ups). |
| Actualización de `docs/decisiones-implementacion.md` diferida al archive-report | ✅ Cumplido | Ver §11 — sección "Inmutabilidad de Codigo en UnidadOrganizativa" ampliada. |

---

## 9. Pending follow-ups

### 9.1 Issue aparte — Setup local WebApplicationFactory (vinculado a M-1)

**Severidad**: MEDIUM. **Recomendación**: Abrir issue GitHub aparte (no parte de este change) para investigar por qué los 2 tests WebIntegration fallan en local sin MySQL 8 + `dotnet user-secrets` config:

- `SGV.Tests.Web.Cargo.ApiBearerTokenIntegrationTests.Get_CargosIndex_WhenAuthenticated_ForwardsBearerTokenToApi`
- `SGV.Tests.Web.Puesto.PuestoIndexPageTests.Get_Index_WhenApiFails_ShowsVisibleError`

Ambos retornan `Found` (302) en lugar de `OK` (200) y **fueron verificados pre-existentes en `develop` HEAD sin los cambios del change**. Causa raíz probable: setup local de `WebIntegrationFixture` requiere `dotnet user-secrets set "ConnectionStrings__SgvDatabase" ...` o similar para que la DB de testing esté accesible.

### 9.2 Issue aparte — `Cargo.Desactivar` invariante `_puestos` activos (MED)

**Severidad**: MEDIUM. **Recomendación**: Endurecer `Cargo.Desactivar()` para que cualquier reconstitución de `Cargo` con `IsActive=false` cargada con `_puestos` activos al `Load` dispare validación. Esto puede lograrse con un test estructural adicional (`Reconstitute_Cargo_IsActiveFalse_ConPuestosActivos_DisparaValidacion`) o moviendo la validación al factory. Documentar la decisión y abrir issue.

### 9.3 Sugerencias S-1/S-2/S-3 (LOW)

- **S-1** — Centralizar `InternalsVisibleTo` en `Directory.Build.props`. Limpieza opcional.
- **S-2** — `[SuppressMessage]` para CS8524 en `ErrorCategoriaMappers.cs`. Preexistente en `develop`.
- **S-3** — Incrementar `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS` para `WebIntegrationFixtureBootstrapCleanupTests`. Vinculado a M-1.

---

## 10. Archivos tocados (resumen vs `develop`)

| Path | Rol | Cambio |
|---|---|---|
| `src/SGV.Dominio/SGV.Dominio.csproj` | Prod | `InternalsVisibleTo("SGV.Tests")` + `InternalsVisibleTo("SGV.Infraestructura")` |
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Prod | `Reconstitute(...)` factory |
| `src/SGV.Dominio/Organizacion/Cargo.cs` | Prod | `Reconstitute(...)` factory |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Prod | `Reconstitute(...)` factory (con validación FechaFin ≥ FechaInicio) |
| `src/SGV.Dominio/Personas/Persona.cs` | Prod | `Reconstitute(...)` factory (con document fields explícitos) |
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Prod | `Reconstitute(...)` factory (reusa `CambiarPuestoSuperior`) |
| `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | Prod | `Reconstitute(...)` + mutadores `with` → `void` + propiedades `init` → `private set` |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Prod | 6 `ToDomain(TEntity)` refactorizados + helper `SetProperty<T>` eliminado + `using System.Reflection;` eliminado |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Prod | 5 sitios con `unidad = unidad.X(...)` → `unidad.X(...)` |
| `tests/SGV.Tests/Persistencia/CargoMapperTests.cs` | Tests | NUEVO (1 IL + 5 behavior) |
| `tests/SGV.Tests/Persistencia/HabilidadMapperTests.cs` | Tests | NUEVO (1 IL + 4 behavior) |
| `tests/SGV.Tests/Persistencia/OcupacionMapperTests.cs` | Tests | AMPLIADO (1 IL + 4 behavior) |
| `tests/SGV.Tests/Persistencia/PersonaMapperTests.cs` | Tests | NUEVO (1 IL + 6 behavior) |
| `tests/SGV.Tests/Persistencia/PuestoMapperTests.cs` | Tests | NUEVO (1 IL + 5 behavior) |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaReconstituteTests.cs` | Tests | NUEVO (6 behavior) |
| `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` | Tests | Actualizado `Codigo_EsInmutableTrasCreacion` (init→private set) + 3 tests `Actualizar` (return→void) |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | Tests | Quitar `padre = padre.X(...)` y `hijo = hijo.X(...)` en 4 sitios |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Tests | `unidad.Actualizar(...)` ya no captura retorno |
| `docs/decisiones-implementacion.md` | Doc | Sección "Inmutabilidad de `Codigo` en `UnidadOrganizativa`" ampliada (este archive, §11) |

**Total**: 18 archivos productivos / de tests + 1 doc. **+1519 / -209 = 1310 changed lines**.

---

## 11. Source of Truth actualizado

### 11.1 `docs/decisiones-implementacion.md`

Sección "Inmutabilidad de `Codigo` en `UnidadOrganizativa`" (líneas 78-88 del archivo pre-change) ampliada con nota que documenta:

- El patrón `Reconstitute(...)` se generalizó a las 6 entidades principales (`Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion`, `UnidadOrganizativa`) tras el change #124.
- `UnidadOrganizativa` migró de `init`-only + `with`-returning mutators a `private set` + `void`-return mutators para paridad total con las otras 5 entidades.
- `PersistenceToDomainMapper.cs` ya no usa `PropertyInfo.SetValue` ni `SetProperty<T>`; los 12 call sites fueron reemplazados por invocación directa de cada `Reconstitute(...)`.
- Referencia a este `archive-report.md` para detalles completos.

### 11.2 `openspec/specs/`

**No modificado.** El refactor preserva invariantes observables (`sgv-persistence-architecture`) sin agregar/modificar capabilities. No requiere delta spec.

---

## 12. TDD Cycle Evidence (resumen)

| CU | Cycle | Tests |
|---|---|---|
| CU-1 | RED (5 IL guards nuevos) | 5 tests IL estructurales en RED (falla porque `SetProperty` aún existe) |
| CU-2 | RED → GREEN (Habilidad) | 4 behavior nuevos verdes; T-1.3 (IL guard Habilidad) pasa a GREEN |
| CU-3 | RED → GREEN (Cargo) | 5 behavior nuevos verdes; T-1.2 (IL guard Cargo) pasa a GREEN |
| CU-4 | RED → GREEN (Ocupacion) | 4 behavior nuevos verdes; T-1.6 (IL guard Ocupacion) pasa a GREEN |
| CU-5 | RED → GREEN (Persona) | 6 behavior nuevos verdes; T-1.5 (IL guard Persona) pasa a GREEN |
| CU-6 | RED → GREEN (Puesto) | 5 behavior nuevos verdes; T-1.4 (IL guard Puesto) pasa a GREEN |
| CU-7 | Atómico (UO) | 6 Reconstitute verdes + tests previos pre-cambio siguen verdes |
| CU-8 | REFACTOR (cleanup) | Helper eliminado, build verde, suite completa verde |

**Total tests nuevos**: 36 (1 IL × 5 + 22 behavior + 6 Reconstitute UO + 4 service tests actualizados).
**Total tests verdes al cierre**: 6 IL guards + ~22 behavior nuevos + suite previa preservada.
**TDD Compliance**: 6/6 checks passed (`verify-report.md §8.5`).

---

## 13. Engram Observation Reference

| Artifact | Observation ID | Topic Key |
|---|---|---|
| `sdd/124/.../preflight` | #1041 | (sin topic — config) |
| IL-walk structural guard pattern | #1042 | (sin topic — pattern reusable) |
| `sdd/resuelve la issue #124/explore` | #1043 | `sdd/resuelve la issue #124/explore` |
| (preference: stop and confirm per phase) | #1044 | (sin topic — preference) |
| `sdd/resuelve la issue #124/proposal` | #1045 | `sdd/resuelve la issue #124/proposal` |
| `sdd/resuelve la issue #124/design` | #1046 | `sdd/resuelve la issue #124/design` |
| `sdd/resuelve la issue #124/tasks` | #1047 | `sdd/resuelve la issue #124/tasks` |
| `sdd/resuelve la issue #124/apply-progress` | #1048 | `sdd/resuelve la issue #124/apply-progress` |
| `sdd/resuelve la issue #124/verify-report` | #1049 | `sdd/resuelve la issue #124/verify-report` |
| `sdd/resuelve la issue #124/archive-report` (este archivo) | (próximo ID disponible) | `sdd/resuelve la issue #124/archive-report` |

---

## 14. Archive Contents

| Artifact | State |
|---|---|
| `proposal.md` | ✅ Preservado (124 líneas) |
| `design.md` | ✅ Preservado (419 líneas) |
| `exploration.md` | ✅ Preservado (432 líneas — issue discovery) |
| `tasks.md` | ✅ 40/40 tareas marcadas `- [x]` (no requiere reconciliación) |
| `apply-progress.md` | ✅ Preservado (280 líneas, evidencia por task) |
| `verify-report.md` | ✅ Preservado (383 líneas, verdict PASS WITH WARNINGS) |
| `archive-report.md` | ✅ Este archivo |

> **Nota**: Este change NO tiene `specs/` folder porque no se generaron specs/deltas. Esto es consistente con el `proposal.md §Capabilities` que declara "New Capabilities: None / Modified Capabilities: None".

---

## 15. Source of Truth Final

- **Código**: 6 entidades con `internal Reconstitute(...)`, `PersistenceToDomainMapper` sin reflexión, `UnidadOrganizativa` con `private set` + mutadores `void`. Build limpio.
- **Tests**: 6 IL guards verdes + ~22 behavior nuevos + suite previa preservada.
- **Specs**: sin cambios (`sgv-persistence-architecture` invariantes observadas).
- **Documentación**: `docs/decisiones-implementacion.md` actualizado con nota del patrón `Reconstitute` generalizado a las 6 entidades (en este archive, §11).

---

## 16. SDD Cycle Complete

El change #124 fue **planificado, implementado, verificado y archivado**. La estrategia de `Reconstitute(...)` reemplaza definitivamente la reflexión (`PropertyInfo.SetValue` + `BindingFlags.NonPublic`) en el path MySQL → Dominio. La defensa contra reintroducción del helper es estructural (5 tests IL nuevos que recorren el cuerpo IL del `ToDomain` correspondiente y fallan si alguien re-introduce `SetProperty`) + 6 IL guards en total. El comportamiento observable de los repositorios es idéntico al pre-change (schema, contratos, queries).

### Próximos pasos para el orquestador

1. **Commitear los archivos SDD + archive + docs** (ejecutado en este archive — ver archivos en este directorio y el commit a continuación).
2. **Merge contra `develop`**: la rama `fix/124-persistence-mapper-reconstitute` queda lista para PR cohesivo (8 commits atómicos + commit de archive/docs). El orquestador/developer decide cuándo abrir PR.
3. **Issue #124 cerrada** cuando el PR se mergee (el usuario decidirá el cierre formal en GitHub).
4. **Issues aparte recomendadas**: setup local WebIntegration (M-1), endurecer `Cargo.Desactivar` invariante `_puestos` (MED).

Listo para el próximo change.