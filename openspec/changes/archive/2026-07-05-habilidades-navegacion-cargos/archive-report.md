# Archive Report: habilidades-navegacion-cargos

## 1. Resumen ejecutivo

El change `habilidades-navegacion-cargos` completó su ciclo SDD y quedó archivado después de
sincronizar sus tres delta specs hacia `openspec/specs/`. La verificación final quedó en
**PASS** (veredicto global consolidado en `verify-report.md`), con 0 issues CRITICAL,
2 WARNING documentados y justificados (cobertura 0% del repositorio y del cliente reales,
sustituidos por fakes por issue #59), y 4 SUGGESTION cosméticas.

El change se implementó como cadena de dos work units (`stacked-to-develop`):

- **WU-A (Foundation + API)**: T1, T2, T3, T4, T8 — subrecurso `GET /api/v1/skills/{skillId}/cargos`,
  DTO `SkillCargoDetailDto`, servicio de consulta, repositorio EF Core con gotcha Pomelo
  documentado, endpoint autenticado en `SkillsController`, y 8 tests del controller.
- **WU-B (Web layer)**: T5, T6, T7, T9 — cliente tipado `HabilidadApiClient.GetCargosAsync`,
  Razor Page readonly `Pages/Organizacion/Habilidades/Cargos.cshtml` con gating admin,
  helper `BuildCargosRouteValues` + botón **Cargos** en `Habilidades/Index`
  (preservando `p/search/sort/status`), y 12 tests nuevos (2 de Index + 10 de PageModel).

**Tareas fuera de alcance documentadas**: T10 omitido por issue #59 (ausencia de harness
InMemory); no requiere ejecución.

El merge de specs fue no destructivo: las specs existentes (`habilidad-management` y
`habilidad-web-listado-detalle-baja`) extendieron sus requisitos; la spec nueva
(`skill-cargo-query-contract`) se creó como source of truth inicial.

## 2. Specs sincronizados

| Dominio | Delta source | Main spec | Acción | Detalle |
|---|---|---|---|---|
| `habilidad-management` | `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/specs/habilidad-management/spec.md` | `openspec/specs/habilidad-management/spec.md` | Updated | Agregado `Requirement: Consultar cargos asociados a una habilidad` con 3 escenarios (paginado, vacío, 404). Modificado `Requirement: Excluir Asignaciones Iniciales` para abrir la puerta a la lectura readonly del subrecurso sin tocar writes. Modificado `Requirement: Autorización de endpoints de habilidades` para incluir el nuevo `GET /api/v1/skills/{skillId}/cargos` en la lista de lecturas autenticadas. |
| `habilidad-web-listado-detalle-baja` | `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/specs/habilidad-web-listado-detalle-baja/spec.md` | `openspec/specs/habilidad-web-listado-detalle-baja/spec.md` | Updated | Modificado `Requirement: Acciones contextuales por segmento` para incluir la acción `Cargos` en filas activas, preservación de `p/search/sort/status` y ocultar `Cargos` en eliminadas. Renombrado `Scenario: Vista activas muestra acciones de catálogo activo` para reflejar las 4 acciones (Detalle, Cargos, Editar, Eliminar). |
| `skill-cargo-query-contract` | `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/specs/skill-cargo-query-contract/spec.md` | `openspec/specs/skill-cargo-query-contract/spec.md` | Created | Nuevo main spec copiado como source of truth inicial de la capability del subrecurso readonly de cargos por habilidad. No existía spec previo en el repo. |

## 3. Verificación del archive

- [x] Main specs actualizados correctamente (3 specs: 2 MODIFIED + 1 ADDED).
- [x] Carpeta del change movida a `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/`.
- [x] Archive contiene `exploration.md`, `proposal.md`, `specs/`, `design.md`, `tasks.md`,
  `apply-progress.md` y `verify-report.md`.
- [x] `tasks.md` archivado tiene todas las tasks WU-A + WU-B marcadas
  (`grep -c "\- \[ \]" tasks.md` debe ser `0` para tasks WU-A/WU-B; T10 justificado,
  T11 ejecutado).
- [x] `openspec/changes/` activo ya no contiene `habilidades-navegacion-cargos`.
- [x] `dotnet build SGV.slnx` corrido post-merge: **0 warnings, 0 errors**.
- [x] Suite `dotnet test --filter "FullyQualifiedName!~OcupacionRepositoryTests"`:
  **1398/1398 PASS** post-merge.
- [x] `cd src/SGV.Web && bun run build`: **exit 0**.

## 4. Source of truth actualizado

Los siguientes paths quedan como fuente vigente del comportamiento archivado:

- `openspec/specs/habilidad-management/spec.md` (MODIFIED)
- `openspec/specs/habilidad-web-listado-detalle-baja/spec.md` (MODIFIED)
- `openspec/specs/skill-cargo-query-contract/spec.md` (Created)

## 5. Notas de merge y reconciliación

- **Merge destructivo**: No. Las dos specs MODIFIED extendieron requisitos existentes y agregaron
  escenarios en línea con el patrón vigente del catálogo (formato Escenario: DADO/CUANDO/ENTONCES/Y).
- **Reconciliación mecánica de tasks**: No. Las tasks WU-A y WU-B están todas marcadas en `tasks.md`
  archivado; T10 sigue marcado con justificación, T11 está marcado (esta verificación es T11).
- **Regla `rules.archive` aplicada**: sí. Se verificó que el merge no fuera destructivo antes de
  sincronizar los deltas (`MODIFIED → extiende` y `ADDED → crea spec`).
- **Observación 1**: la spec nueva `skill-cargo-query-contract` se creó desde el delta completo
  porque no existía spec principal previo en el repo; sigue exactamente el formato usado por
  `cargo-skill-query-contract` (su espejo paralelo del lado Cargo).
- **Observación 2**: las 3 delta specs usan formato OpenSpec con `## Purpose / ## Requirements /
  ### Requirement / #### Scenario`. El catálogo principal todavía usa formato legacy
  (`## Propósito / ## Requisitos / ### Requirement: ...` con `**DADO/CUANDO/ENTONCES**` en
  negrita). Para esta sincronización se mantuvieron los formats respectivos para no romper
  el repo: las MODIFIED se editaron en formato legacy, la nueva spec `skill-cargo-query-contract`
  se creó en formato OpenSpec consistente con su contraparte `cargo-skill-query-contract`.

## 6. Riesgos abiertos transferidos al catálogo

- **R-NEW-1 (T10 reactivable)**: si en el futuro se introduce un harness InMemory en el repo de
  tests, los 8 tests de `HabilidadesCargosControllerTests` deben seguir pasando como regresión.
- **R-NEW-2 (firma de servicio padre)**: cualquier refactor de `IHabilidadServicioConsulta.GetByIdAsync`
  impacta el chequeo 404↔vacío tanto en `SkillsController.GetCargos` como en
  `HabilidadesCargosModel.OnGetAsync` (recuperable). Cubrir con tests equivalentes antes de tocar ese método.
- **R-NEW-3 (drift de contrato entre WUs)**: si el subrecurso `/api/v1/skills/{skillId}/cargos`
  cambia de shape entre merge de PR #1 y PR #2, los tests de WU-B fallarán en CI (FAIL rápido).
- **R-NEW-4 (fixture compartido)**: `CargoWebTestFixture` reutilizado en T9 #10 — si el fixture
  cambia para apuntar a otra Program, ese test requerirá refactor.

## 7. Artefactos archivados y referencias

- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/exploration.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/proposal.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/design.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/tasks.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/apply-progress.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/verify-report.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/specs/habilidad-management/spec.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/specs/habilidad-web-listado-detalle-baja/spec.md`
- `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/specs/skill-cargo-query-contract/spec.md`

## 8. Cierre

El change queda archivado con trazabilidad completa en OpenSpec. El próximo estado recomendado
para este flujo es **archive-complete**; el siguiente paso natural es abrir los 2 PRs de la
cadena `stacked-to-develop` con `branch-pr` (PR #1 WU-A → PR #2 WU-B), pero ese trabajo
queda fuera del archive.

---

## Result Contract

- **status**: success
- **executive_summary**: Change `habilidades-navegacion-cargos` archivado. 3 specs sincronizadas
  al catálogo (`habilidad-management` MODIFIED, `habilidad-web-listado-detalle-baja` MODIFIED,
  `skill-cargo-query-contract` ADDED). Carpeta movida a `archive/2026-07-05-...`. Suite
  1398/1398 PASS verificada post-merge. 0 CRITICAL, 2 WARNING, 4 SUGGESTION en el verify-report.
- **artifacts**:
  - `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/` (carpeta completa)
  - `openspec/changes/archive/2026-07-05-habilidades-navegacion-cargos/archive-report.md` (este archivo)
  - `openspec/specs/habilidad-management/spec.md` (updated)
  - `openspec/specs/habilidad-web-listado-detalle-baja/spec.md` (updated)
  - `openspec/specs/skill-cargo-query-contract/spec.md` (created)
- **next_recommended**: `branch-pr` con la cadena `stacked-to-develop` (PR #1 WU-A Foundation+API,
  PR #2 WU-B Web layer con base `feat/habilidades-navegacion-cargos-api`). Cualquier trabajo
  posterior sobre el subrecurso o la página debe abrirse como un change nuevo.
- **risks**: 4 riesgos abiertos transferidos (R-NEW-1 a R-NEW-4), todos no bloqueantes, listados
  en §6.
- **skill_resolution**: paths-injected — `sdd-archive`, `sdd-verify`, `chained-pr`,
  `work-unit-commits`, `Razor Pages Patterns`.
