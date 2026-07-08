# Archive Report: Expone el botón Editar por fila en Puestos y cierra la frontera admin en PuestosController

> Resumen del cierre del ciclo SDD para el change `2026-07-08-implementa-edicion-puesto-frontend`. Modo orquestador: `interactive`. Artifact store: `both` (OpenSpec filesystem + Engram). Strict TDD: ACTIVO. Idioma: español.

## Archive Metadata

| Campo | Valor |
|-------|-------|
| Change | `2026-07-08-implementa-edicion-puesto-frontend` |
| Archived on | 2026-07-08 |
| Archived to | `openspec/changes/archive/2026-07-08-implementa-edicion-puesto-frontend/` |
| Verdict from verify | `pass-with-notes` (S1 REMEDIATED con test de round-trip dedicado) |
| PR | #95 (draft, `feat/edicion-puesto-frontend` → `develop`) |
| Branch | `feat/edicion-puesto-frontend` |
| Override | No (S1 remediado antes del archive; no quedan CRITICAL abiertos) |

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `puesto-management` | **Created** en canonical | Spec nueva: 1 requirement (`Autorización de endpoints de puestos`) con 3 escenarios (Lectura autenticada exitosa, Acceso anónimo rechazado, Mutación protegida por rol administrador), más metadatos `Source` y `Verification` que enlazan al archive. Formato canónico (Propósito + Requisitos + Escenarios en DADO/CUANDO/ENTONCES), paralelo 1:1 al requirement "Autorización de endpoints de cargos" de `cargo-management/spec.md:259-280`. |

### Diff stats (delta → canonical)

| Archivo | Líneas antes | Líneas después | Δ |
|---------|--------------|----------------|---|
| `openspec/specs/puesto-management/spec.md` | inexistente | 41 | +41 (1 requirement con 3 escenarios + Source/Verification) |

## Archive Contents

- `proposal.md` ✅ (preservado, 81 líneas)
- `exploration.md` ✅ (preservado, 193 líneas)
- `design.md` ✅ (preservado, 86 líneas)
- `specs/puesto-management/spec.md` ✅ (preservado, 26 líneas — delta spec original)
- `tasks.md` ✅ (preservado, 50 líneas — 15/15 tareas de implementación marcadas GREEN)
- `apply-progress.md` ✅ (preservado, 230+ líneas — incluye registro de remediación S1 y correcciones post-review PR #95)
- `verify-report.md` ✅ (preservado, 195 líneas — verdict `pass-with-notes`, S1 documentado y remediado)
- `archive-report.md` ✅ (este documento)

## Source of Truth Updated

- `openspec/specs/puesto-management/spec.md` — capability nueva que captura el contrato durable de autorización HTTP de `Puestos`. Vive en canonical con el mismo formato que `cargo-management`, lista para que cambios futuros (segmentación `?status=activas|eliminadas`, nuevos endpoints) extiendan este archivo en lugar de crear deltas efímeras.

El spec canónico vigente `openspec/specs/puesto-web-listado-detalle-baja/spec.md:27` ya exigía el botón `Editar` por fila; este change **cumple** ese requisito y no requiere delta de UI.

## Cycle Evidence

| Métrica | Valor |
|---------|-------|
| Tasks de implementación | 15/15 GREEN (Fase 1: 1.1-1.3, Fase 2: 2.1-2.4, Fase 3: 3.1-3.2, Fase 4: 4.1-4.5, Fase 5: 5.1-5.3) |
| Build | 0 errors, 0 warnings (`dotnet build SGV.slnx`) |
| Tests targeted | 44/44 PASS (`PuestosControllerTests` 27 + `PuestoIndexPageTests` 17) |
| Suite web | 408/408 PASS (sin regresión, incluye el test nuevo del round-trip S1) |
| Suite API | 431/431 PASS (sin regresión) |
| Suite completa | 1527 PASS, 12 FAIL pre-existentes (issue #59, `OcupacionRepositoryTests` por `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` — no relacionado) |
| S1 remediation | ✅ `RoundTrip_FromEliminadasSegment_PreservesSegmentInPostSaveRedirect` en `PuestoEditPageTests.cs` (RED → GREEN tras rename de binding `status` → `returnStatus` en `Edit.cshtml.cs`) |
| Post-review PR #95 | ✅ `CaptureReturnContext` helper extraído en `Edit.cshtml.cs` (-2 LoC neto), `<remarks>` documental en `PuestosController.Update`, asserts "cada fila" extendidos en `PuestoIndexPageTests` |
| LoC change | +221 / −21 en 5 archivos de código (+95 / −6 en 2 archivos por S1) = **~316 LoC en scope**, dentro del presupuesto 400 LoC |
| Working tree isolation | ✅ verificado: `DatosSemilla.cs` y migración `20260706221558_*` permanecen sin tocar |
| HEAD `feat/edicion-puesto-frontend` | `22b9ed7f` (post-review corrections) antes del archive; HEAD post-archive = SHA del commit `chore(sdd): archive change 2026-07-08 edicion puesto frontend` |

## Work-Unit Commits (8 commits en la rama antes del archive)

1. `5517affa test(puestos): assert presencia y ausencia del botón Editar en Index`
2. `2b75e911 feat(web): expone botón Editar en Puestos Index con helper BuildEditRouteValues`
3. `5a925336 test(api): assert autorización admin y matriz 401/403 en PuestosController`
4. `46ae4d42 feat(api): requiere rol Administrador en writes de PuestosController`
5. `858858b8 fix(web): preserva segmento returnStatus en round-trip Index-Edit-Details de Puestos` (S1)
6. `9b3b9463 test(puestos): assert round-trip del segmento returnStatus Index-Edit` (S1)
7. `e8a017c3 docs(sdd): artefactos del change 2026-07-08 edicion puesto frontend`
8. `22b9ed7f refactor(puestos): apply PR #95 post-review corrections`

## Lecciones aprendidas

- **Paridad con Cargos acelera el slice**: el helper `BuildEditRouteValues` y el botón `btn-warning` con `ti ti-edit` reusaron verbatim el patrón ya validado en `Cargos/Index`; el bug de round-trip (`returnStatus` vs `status`) se descubrió sólo porque el verify-report levanta la asimetría entre helper (emisor) y binding (receptor) y se blindó con un test dedicado.
- **El verify-report detectó un bug que el apply-progress no vio**: S1 (pérdida del segmento `status=eliminadas` tras Edit+Save) surgió del contraste entre el design.md (que afirma `returnStatus`) y la realidad del binding (`[FromQuery(Name = "status")]`). La separación verify↔apply valió la pena: el bug se cerró con 1 rename atómico y un test RED → GREEN sin reabrir el scope del change.
- **Working tree isolation disciplinada**: el branch arrastraba 3 archivos sucios de otro trabajo en curso (`DatosSemilla.cs` + migración `20260706221558_*`). El path-scoped `git diff -- src/SGV.Web/Pages/Organizacion/Puestos src/SGV.Api/Controllers/PuestosController.cs tests/SGV.Tests/...` antes de cada commit mantuvo el PR #95 quirúrgico; el archive commit sigue el mismo principio y stagea sólo paths `openspec/`.
- **Reutilización de `ApiWebApplicationFactory.CreateAdminClient` / `CreateNonAdminClient`**: el harness extendido en `2026-07-01-cargos-crear-autorizacion-admin` fue suficiente para cubrir la matriz 401/403/2xx de Puestos sin agregar variabilidad ni policies nuevas.

## Próximos pasos (futuro)

- **`puestos-filtro-activos-eliminados`** — habilitar el segmento `Eliminadas` (hoy `disabled` con tooltip "Próximamente") cuando exista el endpoint `/api/v1/puestos/consulta?status=activas|eliminadas` en el backend. El requirement de autorización canónico ya está listo para extenderse con la nueva segmentación.
- **Issue #59** — bug preexistente que bloquea 12 tests de `OcupacionRepositoryTests` (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`). Fuera del scope de este change; requiere su propio PR.

## SDD Cycle Complete

El change fue planificado (`proposal` + `exploration` con refinamiento de alcance), especificado (1 delta spec nuevo `puesto-management`), diseñado (`design` con 4 decisiones de arquitectura), desglosado en tareas (`tasks` con 15 tareas en 5 fases), implementado en 8 commits work-unit cubriendo frontend + backend + tests + remediación S1 + post-review, verificado con TDD estricto + suite web/API sin regresión (`verify` `pass-with-notes` con S1 remediado) y archivado (sincronización a canonical `puesto-management/spec.md` + movimiento del change folder a `archive/` con este `archive-report.md`). El módulo `Puestos` queda con su entry point Edit por fila operativo y la frontera admin de `PuestosController` cerrada por rol, paridad 1:1 con Cargos. Listo para el próximo change.
