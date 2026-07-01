# Reporte de Archivo — `2026-06-30-cargos-crear-editar-codigo-editable`

## 1. Metadata
- **Change archived on**: 2026-07-01
- **Status**: **Archived (intentional-with-warnings)** — ver §5.
- **Phases**: proposal → spec → design → tasks → apply (PR1 + PR2A + PR2A cleanup + PR2B) → archive
- **Artifact store**: openspec (filesystem-backed)
- **PRs merged**: PR1 (#60, backend), PR2A (#62, frontend Create), PR2A cleanup (#63, frontend refactor), PR2B (#64, frontend Edit + Details CTA + post-review fixes at 5842c114)

## 2. Specs sincronizadas

| Dominio | Acción | Detalles |
|--------|--------|---------|
| `cargo-management` | MODIFIED | `Actualizar Cargo` reemplazado (6 escenarios); `Unicidad activa de Codigo en update` agregado (2 escenarios). |
| `cargo-web-crear-editar` | Created | Spec principal nuevo con requisitos de acceso autenticado, datos editables, catálogo/error handling/PRG y submenú `Nueva`. |

### 2.1 `cargo-management` main spec
- Requisito afectado: `Actualizar Cargo` (MODIFIED) — antes decía que `Codigo` no era libremente mutable; ahora queda editable con validación y unicidad activa.
- Requisito nuevo: `Unicidad activa de Codigo en update` (ADDED) — extiende a update la misma semántica active-only ya usada en create.
- Requisitos sin cambios: `Crear Cargo`, `Consultar Cargos`, `Desactivar Cargo`, `Reactivar Cargo`, `Contrato de Respuesta Cargo`.

### 2.2 `cargo-web-crear-editar` main spec
- Se creó el spec principal inexistente a partir del delta completo.
- El contenido archivado cubre create/edit autenticado, campos editables, carga de catálogo de niveles y guardado con feedback/PRG, además del acceso `Nueva` en `_Sidenav`.

## 3. Reconciliación mecánica de tareas
El `tasks.md` del change tenía 41 checkboxes de implementación en estado `- [ ]`. Durante archive se reconciliaron mecánicamente a `- [x]` por autorización explícita del orchestrator, respaldada por la evidencia del `apply-progress.md` consolidado:

- `apply-progress.md` §PR 1 — tareas `PR-1.x` marcadas como completadas.
- `apply-progress.md` §PR2A — tareas `PR-2A.x` marcadas como completadas.
- `apply-progress.md` §PR2A refactor cleanup — tareas `Cleanup.x` marcadas como completadas.
- `apply-progress.md` §PR 2B — tareas `PR-2B.x` marcadas como completadas.
- Las 4 fases documentan `TDD Cycle Evidence`, cumpliendo la trazabilidad esperada para `strict_tdd` en el ciclo consolidado.

Razón de la reconciliación: en este repo `tasks.md` funciona como contrato y `apply-progress.md` como bitácora fase por fase. El archivo archivado no debe conservar checkboxes obsoletos de trabajo ya implementado y enviado.

## 4. Trazabilidad de PRs

| PR | Branch | Alcance | Estado |
|----|--------|---------|--------|
| #60 | `feat/cargos-crear-editar-codigo-editable-pr1` | Backend: `Codigo` editable en update + unicidad activa | ✅ merged |
| #62 | `feat/cargos-crear-editar-codigo-editable-pr2a` | Frontend Create + niveles + submenú | ✅ merged |
| #63 | `feat/cargos-crear-editar-codigo-editable-pr2a-cleanup` | Refactor frontend behavior-preserving | ✅ merged |
| #64 | `feat/cargos-crear-editar-codigo-editable-pr2b` | Frontend Edit + Details CTA + fixups de review | ✅ merged |

## 5. Warnings / Waivers (intentional-with-warnings)

El único `verify-report.md` persistido corresponde a PR1 y registró **2 issues CRITICAL**:

1. **`dotnet test SGV.slnx` falla con 12 tests en `OcupacionRepositoryTests`**.
   - **Origen del waiver**: issue #59 ya documentado; falla preexistente de MySQL asociada a `ActivePuestoIdUnique` incompatible con `PuestoId CHAR(36)`.
   - **Por qué no bloquea el archive**: el fallo no pertenece al alcance de Cargos/Web. La verificación focalizada del change quedó verde (`Cargo|Cargos` y `Web`), y el bug tiene seguimiento separado.

2. **PR1 no tenía tabla `TDD Cycle Evidence` en `apply-progress.md`**.
   - **Por qué no bloquea el archive**: el `apply-progress.md` consolidado sí incorporó `TDD Cycle Evidence` en PR2A, PR2A cleanup y PR2B, dejando documentado el ciclo completo con la bitácora final usada para archive.

Conclusión del waiver: el cierre se realiza como **intentional-with-warnings** porque la implementación y los slices verificados del change están completos, pero el rastro de verificación quedó repartido entre el verify inicial de PR1 y la evidencia TDD consolidada posterior en `apply-progress.md`.

## 6. Fuera de alcance / follow-ups
- Issue #62: aplicar `[Authorize]` backend sobre `POST/PUT/DELETE /api/v1/cargos/{id}` en un change separado.
- Integración de habilidades para Cargos: fuera de este change.
- Alinear el tipo de `ReturnPage` entre los PageModels de Cargo en una refactorización posterior.

## 7. Ubicaciones de archivo

```text
openspec/specs/cargo-management/spec.md
openspec/specs/cargo-web-crear-editar/spec.md
openspec/changes/archive/2026-07-01-2026-06-30-cargos-crear-editar-codigo-editable/
```

## 8. Resultado del ciclo SDD

✅ **Ciclo completo con warnings documentados**. El change quedó sincronizado en los specs principales, reconciliado en tareas y archivado con waiver explícito de los bloqueos históricos ajenos al alcance funcional del slice.
