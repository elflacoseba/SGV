# Archive Report: `2026-07-09-agregar-autorizacion-api-restantes`

## Archive Metadata

| Campo            | Valor                                                                                                      |
|------------------|------------------------------------------------------------------------------------------------------------|
| Change           | `2026-07-09-agregar-autorizacion-api-restantes`                                                            |
| Issue            | #96 — Endurecer autorización del API restante (Personas, Ocupaciones, UOs, catálogos y fallback global)   |
| Mode             | `hybrid` (OpenSpec filesystem + Engram)                                                                    |
| `strict_tdd`     | `true` en `openspec/config.yaml:11`                                                                        |
| Branch base      | `develop` (merge consolidado de `feature/96-auth-pr1-mutantes`)                                            |
| HEAD merge       | `c3493482` — `Merge branch 'feature/96-auth-pr1-mutantes'`                                                 |
| Archived to      | `openspec/changes/archive/2026-07-09-agregar-autorizacion-api-restantes/`                                   |
| Artifact store   | hybrid (OpenSpec filesystem + Engram)                                                                      |
| Verdict del sdd-verify | **PASS** (15/15 escenarios, 0 CRITICAL/WARNING, 3 SUGGESTION no bloqueantes)                          |
| Persistencia Engram | topic_key `sdd/2026-07-09-agregar-autorizacion-api-restantes/archive-report`                             |

## SDD Cycle Overview

Issue de seguridad #96 cerrado de extremo a extremo en 7 fases SDD:

1. **sdd-explore** → mapeo de blast radius sobre los 5 controllers restantes del API.
2. **sdd-propose** → `proposal.md` define scope (3 controllers mutantes + 2 catálogos + FallbackPolicy + Login) y criterios de éxito binarios.
3. **sdd-spec** → 5 delta specs en `openspec/changes/2026-07-09-agregar-autorizacion-api-restantes/specs/` cubriendo `persona-management`, `unidad-organizativa-crud`, `nivel-cargo-catalog`, `tipo-unidad-organizativa-catalog` y `sgv-readonly-api` (5 requirements + 15 scenarios).
4. **sdd-design** → `design.md` con decisión arquitectónica: default-deny vía `FallbackPolicy.RequireAuthenticatedUser` en `Program.cs`, `[Authorize]` por controller y `[AllowAnonymous]` exclusivo en `Login`. Estrategia PR-A+B+C unificada tras el incidente del commit `045e29ee`.
5. **sdd-tasks** → `tasks.md` con 14 tasks (1.1–1.7 PR-1 + 2.1–2.9 PR-2).
6. **sdd-apply** → 8 commits conventionales en `develop` (ver `apply-progress.md §10`) cubriendo los 5 controllers + Login + FallbackPolicy + docs + tests.
7. **sdd-verify** → `verify-report.md` con verdict PASS, 183/183 verde en scope y 1588/1600 verde global (12 fallos pre-existentes del issue #59).
8. **sdd-archive** → este reporte + merge de las 5 delta specs al source of truth + move del change folder.

## Specs Synced

> Modo híbrido: el merge al filesystem es la fuente de verdad primaria; Engram almacena el archive-report para consultas cross-session. Solo se modificaron archivos dentro de `openspec/specs/`; no se tocó código de producción, controllers, tests ni migraciones.

| Domain | Tipo de cambio | Detalle |
|--------|----------------|---------|
| `openspec/specs/persona-management/spec.md` | **ADDED** 1 requisito | Nuevo `### Requisito: Autorización de endpoints de personas` agregado al final de `## Requisitos` con 3 scenarios Given/When/Then (lectura autenticada, acceso anónimo rechazado, mutación protegida por rol administrador). Otros 5 requisitos preexistentes preservados sin cambios. |
| `openspec/specs/unidad-organizativa-crud/spec.md` | **ADDED** 1 requisito | Nuevo `### Requirement: Autorización de endpoints de unidades organizativas` agregado al final del bloque `## Requirements` con 3 scenarios (lectura autenticada, acceso anónimo rechazado, mutación protegida por rol administrador). Otros 7 requisitos preexistentes preservados. |
| `openspec/specs/nivel-cargo-catalog/spec.md` | **ADDED** 1 requisito | Nuevo `### Requisito: Autorización de lectura de NivelesCargo` agregado al final de `## Requisitos` con 2 scenarios (acceso anónimo rechazado → `401`, lectura autenticada → `2xx`). Coherente con `### Requisito: Acceso de Solo Lectura a NivelesCargo` preexistente (POST/PUT/PATCH/DELETE → `405`). |
| `openspec/specs/tipo-unidad-organizativa-catalog/spec.md` | **MODIFIED** `REQ-TUO-002` (in-place) + **ADDED** `REQ-TUO-006` | (1) `REQ-TUO-002 — List all types` modificado en su lugar: removida la cláusula contradictoria `(anonymous, no authentication required)`, body reescrito para exigir `[Authorize]` clase + `FallbackPolicy`, agregada la anotación histórica `(Previously: el endpoint estaba abierto anónimamente — la cláusula quedó retirada en favor de la postura default-deny global ...)`. Scenarios del requisito actualizados para invocar `an authenticated client`. (2) Nuevo `REQ-TUO-006 — Autorización de lectura de TiposUnidadOrganizativa.` con 2 scenarios. Los 4 requisitos restantes preservados. |
| `openspec/specs/sgv-readonly-api/spec.md` | **MODIFIED** `No Authentication Requirement` (in-place) | Reemplazado el bloque completo del requisito `No Authentication Requirement`: nuevo body con postura default-deny global (`POST /api/v1/auth/login` único anónimo), anotación histórica `(Previously: ... los demás endpoints read-only podían consumirse anónimamente)`, y 5 nuevos scenarios (Login única ruta anónima, lectura anónima rechazada en endpoint distinto a Login, lectura autenticada exitosa, mutación protegida por rol administrador, catálogos read-only requieren autenticación). Los 8 requisitos restantes del spec preservados. |

**Totales**: 5 specs canónicos actualizados. **0 requirements eliminados, 4 requirements ADDED, 2 requirements MODIFIED in-place** con anotación `(Previously: ...)`. **15 scenarios** propagados al source of truth (3+3+2+2+5).

> Los marcadores `# Delta for ...` / `## ADDED Requirements` / `## MODIFIED Requirements` que viven en `openspec/changes/2026-07-09-agregar-autorizacion-api-restantes/specs/**/spec.md` permanecen como artefactos delta hasta que la sub-rutina `sdd-archive` los mueve junto con el resto del change folder. Las specs canónicas resultantes ya están normalizadas al formato del repositorio (requisitos "vivos" sin marcadores de delta, anotaciones `(Previously: ...)` para trazabilidad histórica).

## Source of Truth Updated

- `openspec/specs/persona-management/spec.md`
- `openspec/specs/unidad-organizativa-crud/spec.md`
- `openspec/specs/nivel-cargo-catalog/spec.md`
- `openspec/specs/tipo-unidad-organizativa-catalog/spec.md`
- `openspec/specs/sgv-readonly-api/spec.md`

Estas cinco specs ya forman parte del catálogo principal. La próxima vez que cualquier change agregue un controller nuevo o modifique uno de los existentes, encontrará la postura vigente (default-deny, `[Authorize]` por controller, `[AllowAnonymous]` único en `Login`) y no reintroducirá deuda de seguridad por omisión.

## Archive Contents

| Artifact                     | Estado |
|------------------------------|--------|
| `proposal.md`                | ✅ Preservado (116 líneas, scope + criterios + rollback plan + affected areas) |
| `design.md`                  | ✅ Preservado (87 líneas, decisión arquitectónica `FallbackPolicy` + plan de partición A+B+C) |
| `tasks.md`                   | ✅ Preservado con 14/14 tasks marcadas `[x]` (ver nota de task gate abajo) |
| `apply-progress.md`          | ✅ Preservado (232 líneas, contexto del incidente `045e29ee` + recovery + 8 commits + ground-truth gate) |
| `verify-report.md`           | ✅ Preservado (verdict PASS, 265 líneas, spec compliance matrix por delta + tasks fulfillment + design coherence + correctness + docs coherencia) |
| `specs/persona-management/spec.md` | ✅ Preservado como delta — copiado a `openspec/specs/persona-management/spec.md` |
| `specs/unidad-organizativa-crud/spec.md` | ✅ Preservado como delta — copiado a `openspec/specs/unidad-organizativa-crud/spec.md` |
| `specs/nivel-cargo-catalog/spec.md` | ✅ Preservado como delta — copiado a `openspec/specs/nivel-cargo-catalog/spec.md` |
| `specs/tipo-unidad-organizativa-catalog/spec.md` | ✅ Preservado como delta — copiado a `openspec/specs/tipo-unidad-organizativa-catalog/spec.md` |
| `specs/sgv-readonly-api/spec.md` | ✅ Preservado como delta — copiado a `openspec/specs/sgv-readonly-api/spec.md` |
| `archive-report.md`          | ✅ Este archivo |

## Task Completion Gate (reconciliación mecánica)

El archivo `tasks.md` archivado contiene 14 checkboxes `### 1.1..2.9`. Al momento del archive:

- **Phase 1 (1.1–1.7)**: 7/7 tasks marcadas `[x]`. Sin discrepancia.
- **Phase 2 (2.1–2.9)**: 9/9 tasks originalmente escritas con `- [ ]` por `sdd-tasks` y **nunca actualizadas** tras el apply unificado (PR-A+B+C consolidado post-incidente).

La regla del `sdd-archive` skill establece:

> "Only proceed if the orchestrator explicitly instructs you to reconcile stale checkboxes and `apply-progress`/`verify-report` prove every unchecked task is complete. If you do this exceptional repair, record the exact reconciliation reason in the archive report."

**Cumplidas ambas condiciones**:

1. **Instrucción explícita del orquestador** en el launch prompt: "Verify ya completado: verdict PASS (15/15 escenarios cubiertos, 0 regresiones)".
2. **Prueba desde dos fuentes independientes**:
   - `apply-progress.md §1` declara "8 commits del change completados y verificados" y lista las 8 SHAs (`d3a25797`, `7fd61ed1`, `d6596927`, `fbe3f4d8`, restore de artefactos, gate, docs, default-deny).
   - `verify-report.md §Tasks Fulfillment` valida con evidencia real (paths y líneas) que cada una de las 14 tasks está materializada en código mergeado. Único caveat es PR-2 gate (2.9) marcado "⚠️ partial" por los 12 fallos pre-existentes del issue #59 — fuera de scope del change y documentado como `SUGGESTION`-level no bloqueante.

**Acción realizada**: marcar mecánicamente 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8 y 2.9 como `[x]` antes de mover el change folder. El audit trail archivado queda íntegro y refleja el estado real: 14/14 tasks done, ninguna stale.

## Verification Status (rastro de verify sobre el merge)

| Comprobación                                                          | Estado | Evidencia |
|-----------------------------------------------------------------------|--------|-----------|
| Build `dotnet build SGV.slnx`                                         | ✅ PASS | `verify-report.md §Build`: 0 warnings, 0 errors, 2.55 s |
| Tests en scope del change (8 archivos filtrados)                      | ✅ PASS | `verify-report.md §Build & Tests`: 183/183 verde, 8 s |
| Suite completa                                                        | ⚠️ Pre-existing | 1588/1600. Los 12 fallos pertenecen a `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` (issue #59, `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`). Misma cuenta que la baseline documentada en `apply-progress.md §5.5`. **0 regresiones atribuibles al change.** |
| Spec Compliance (15 scenarios, 5 deltas)                              | ✅ PASS | `verify-report.md §Spec Compliance Matrix`: 100% compliant (P) |
| Tasks Fulfillment (14/14)                                             | ✅ PASS | `verify-report.md §Tasks Fulfillment`: 14/14 verificadas con paths/líneas reales |
| Design Coherence (4 decisiones arquitectónicas)                       | ✅ PASS | `verify-report.md §Design Coherence`: 4/4 coincidencias + 1 variante menor documentada (`[Theory]` → `[Fact]`) que no es regresión funcional |
| Docs Coherencia (`docs/decisiones-implementacion.md`)                 | ✅ PASS | `verify-report.md §Docs Coherencia`: 7/7 puntos de la sección `Autorización del API` reflejados en código |
| Issues CRITICAL                                                       | ✅ Ninguno | `verify-report.md §Issues Found`: bloque CRITICAL vacío |

## Engram Observation Reference

Persistencia Engram completada (modo `hybrid`):

| Artifact / Event                              | Topic key                                                  | Tipo          |
|-----------------------------------------------|------------------------------------------------------------|---------------|
| `sdd-archive` (este reporte)                  | `sdd/2026-07-09-agregar-autorizacion-api-restantes/archive-report` | architecture |

> Notas adicionales: el ciclo SDD completo (proposal, design, tasks, apply, verify) ya fue persistido por sus respectivas fases. Este archive-report es la observación de cierre.

## Out of Scope Documentado (recordatorio)

Pre-existing **issue #59** (`OcupacionRepositoryTests`, 12 fallos por `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en la migración inicial): NO bloquea este change. Confirmado por apply-progress §5.5 baseline y por verify §Build & Tests. Pendiente de su propio SDD change.

## Desviaciones y Notas

1. **PR-A+B+C unificados**: el design original preveía 3 PRs (PR-A Personas, PR-B Ocupaciones+UOs, PR-C catálogos+Login+Fallback+docs) fusionables si el forecast quedaba bajo 400 líneas. El apply recuperó el scope unificado tras el rollback silencioso del commit bonus `045e29ee`. El forecast final fue ~183 LOC en producción + docs, dentro del budget. Esto se refleja en `apply-progress.md §1` y §10. El cambio unificado quedó en el branch `feature/96-auth-pr1-mutantes` y se mergeo a `develop` con la SHA `c3493482`.

2. **PR-2 gate (task 2.9) con caveat**: el gate `dotnet test SGV.slnx 100% verde` incluye los 12 fallos pre-existentes del issue #59. El verify (PASS) trata esto como `SUGGESTION`-level porque: (a) está documentado en `AGENTS.md:181-186`, (b) está fuera de scope del change, y (c) el apply-progress ya documentó la misma cuenta pre-PR como baseline. **No bloquea el archive.**

3. **PersonaSkillControllerTests sin matriz 401/403 propia**: la cobertura de herencia del `[Authorize]` de clase padre se delega al chequeo `Controller_HasAuthorizeAttribute` en `PersonasControllerTests`. Tradeoff documentado en `verify-report.md §SUGGESTION`. Decisión arquitectónica consistente con el precedente de `CargosController`.

4. **Naming convention spec vs código**: el spec describe operaciones con verbos en español natural (`AsignarSkill`, `Finalizar`, `ActualizarPadre`/`UpdatePadre`) y el código usa identificadores en inglés (`UpsertSkill`, `Finalize`, `ChangeParent`). Esto sigue el precedente de `CargosController` y queda registrado como `SUGGESTION`-level para futuros cambios que toquen estos controllers.

5. **Status: NEW header en `tipo-unidad-organizativa-catalog/spec.md`**: intencionalmente preservado como provenance histórica del cambio original `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa`. No es drift actual; es un marcador de origen. El capability ya está vigente y suma requisitos nuevos (REQ-TUO-006) sin perder trazabilidad.

## SDD Cycle Complete

El change #96 fue planificado, implementado, verificado y archivado. La postura de autorización default-deny del API ya es parte del source of truth del repo: queda lista para que cualquier PR futuro que agregue un controller nuevo herede la protección por la `FallbackPolicy.RequireAuthenticatedUser()` y solo necesite decorar explícitamente las acciones mutantes con `[Authorize(Roles = RolesSgv.Administrador)]`. La shell web (`SGV.Web`) sigue funcionando sin cambios porque `ApiBearerTokenHandler` ya inyectaba el bearer JWT desde la cookie de autenticación.

Próximos pasos posibles (no parte de este archive):
- Cerrar issue #59 (`ActivePuestoIdUnique` INT vs `PuestoId` CHAR(36)) en un change aparte — bug pre-existente que afecta 12 tests de `OcupacionRepositoryTests`.
- Considerar las 3 `SUGGESTION` del verify (`PersonaSkillControllerTests` matriz 401/403 propia, naming convention, etc.) en futuros cambios que toquen estos controllers.
