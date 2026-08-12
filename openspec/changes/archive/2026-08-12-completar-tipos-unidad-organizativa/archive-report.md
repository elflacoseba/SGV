# Archive Report: completar-tipos-unidad-organizativa

**Change:** `completar-tipos-unidad-organizativa`
**Fecha de archivo:** 2026-08-12
**Modo de artifact store:** both (hybrid — OpenSpec + Engram)
**Decisión de archivo:** explícita, autorizada por el usuario; sin pasar por `sdd-apply`

---

## Motivo de Archivo Sin Aplicar

El cambio fue **planificado completamente** (proposal, design, tasks, delta spec) pero **nunca llegó a implementación**. No se ejecutó `sdd-apply`; no existe commit, PR ni artefacto de aplicación (`apply-progress`/`verify-report`). El usuario autorizó el archivo directo desde la fase de planning como decisión deliberada.

---

## Decisiones Tomadas

| Decisión | Detalle |
|----------|---------|
| **No aplicar** | El cambio se archiva tal cual, sin implementación. Decisión explícita del usuario. |
| **No modificar código** | Ningún archivo `.cs`, migración, test o spec del código fuente fue tocado. |
| **No sincronizar specs** | La delta spec en `specs/tipo-unidad-organizativa-catalog/spec.md` **no se mergeó** a `openspec/specs/`. El spec principal sigue reflejando 7 filas. |
| **Migración huérfana intacta** | `20260730000000_SemillaTipoUnidadOrganizativaAmpliada.cs` permanece en el filesystem sin `.Designer.cs`; sigue siendo invisible a `dotnet ef migrations list` y `Database.Migrate()`. |
| **Carpeta movida a archive** | `openspec/changes/completar-tipos-unidad-organizativa/` → `openspec/changes/archive/2026-08-12-completar-tipos-unidad-organizativa/` |

---

## Ausencia de PR / Commits

| Artefacto | Estado |
|-----------|--------|
| Commit en historial git | **Ninguno** — la carpeta `openspec/changes/completar-tipos-unidad-organizativa/` estaba sin trackear (`??` en `git status`) |
| PR creada | **Ninguna** |
| `apply-progress.md` | **No existe** — `sdd-apply` nunca se ejecutó |
| `verify-report.md` | **No existe** — `sdd-verify` nunca se ejecutó |
| Artefactos SDD existentes | `proposal.md`, `design.md`, `tasks.md`, `exploration.md`, `specs/tipo-unidad-organizativa-catalog/spec.md` — todos en estado "planificado", sin implementación |

---

## Riesgos Conocidos

### 1. Migración huérfana sin `.Designer.cs`

**Archivo:** `src/SGV.Infraestructura/Persistencia/Migraciones/20260730000000_SemillaTipoUnidadOrganizativaAmpliada.cs`

**Problema:** La migración contiene `InsertData` con los 13 tipos faltantes (`Sede → Escuela`) pero carece de su `.Designer.cs` counterpart. Esto la hace **invisible** para:
- `dotnet ef migrations list`
- `dotnet ef migrations script --idempotent`
- `Database.Migrate()` en producción

**Impacto:** Las bases de datos existentes que nunca recibieron estos 13 tipos vía SQL manual o script ad-hoc seguirán teniendo solo 7 filas.

**Estado:** Sin cambios. La migración huérfana sigue en el filesystem. No fue tocada durante el planning de este cambio y permanece en el mismo estado de siempre.

### 2. Catálogo sigue con 7 filas en producción

El spec principal `openspec/specs/tipo-unidad-organizativa-catalog/spec.md` (si existe) aún afirma 7 filas seed. La delta spec de este cambio (20 filas vía `Migrate()`) nunca se mergeó.

### 3. El gap de test no resuelto

El design (línea 69) documenta honestamente:

> **Gap honesto**: el escenario REQ-TUO-001 "Migrate produce 20 filas en base existente con 7 tipos" **no** tiene test automatizado (Out of Scope: no crear tests); se valida con `migrations list` + `script --idempotent` y `COUNT(*)` post-deploy.

Este gap permanece sin resolución.

---

## Artefactos Existentes en el Cambio

| Artefacto | Ruta | Estado |
|-----------|------|--------|
| `proposal.md` | `openspec/changes/…/proposal.md` | ✅ Planificado |
| `design.md` | `openspec/changes/…/design.md` | ✅ Planificado |
| `tasks.md` | `openspec/changes/…/tasks.md` | ✅ Planificado (7 fases, 13 tareas unchecked) |
| `exploration.md` | `openspec/changes/…/exploration.md` | ✅ Planificado |
| Delta spec | `openspec/changes/…/specs/tipo-unidad-organizativa-catalog/spec.md` | ✅ Creado (REQ-TUO-001/002/007 actualizados a 20 filas) |
| `apply-progress` | No existe | ❌ No se generó |
| `verify-report` | No existe | ❌ No se generó |

### Tareas en `tasks.md` — Estado

Todas las tareas están sin marcar (`- [ ]`). La Task Completion Gate **no aplica** aquí porque `sdd-apply` nunca se ejecutó; el usuario autorizó explícitamente archivar con tareas pendientes como parte de una decisión intencional de no aplicar.

---

## Observaciones Engram Relacionadas (para trazabilidad)

| Topic | ID | Observación |
|-------|----|-------------|
| `sdd/completar-tipos-unidad-organizativa/explore` | #1772 | Exploration completa del problema (migración huérfana, gap de test, 7→20) |
| `sdd/completar-tipos-unidad-organizativa/proposal` | #1773 | Proposal SDD con scope, approach, riesgos, rollback |
| `sdd/completar-tipos-unidad-organizativa/spec` | #1774 | Delta spec REQ-TUO-001 (20 filas), REQ-TUO-002, REQ-TUO-007 (forward-only) |
| `sdd/completar-tipos-unidad-organizativa/design` | #1775 | Design con decisiones: hand-author Up(), InsertData vs Sql, Down=NotSupportedException |
| `sdd/completar-tipos-unidad-organizativa/tasks` | #1776 | Tasks comprimidas, Review Workload Forecast High, size:exception |

---

## Recomendación para Retomar el Cambio

### Nombre sugerido para el futuro cambio

`completar-tipos-unidad-organizativa-aplicar`

### Qué se debería hacer

1. **Reutilizar los artefactos existentes** — el `proposal.md`, `design.md`, `tasks.md` y delta spec ya están planificados y archivados. No es necesario rehacer el planning.

2. **Comenzar por ejecutar las tareas de `tasks.md`** — comenzar desde la Fase 1 (`dotnet ef migrations add …`) ejecutando cada fase en orden.

3. **Ejecutar `sdd-apply`** — paratrackear el progreso de implementación, marcar tareas completadas y generar `apply-progress.md`.

4. **Ejecutar `sdd-verify`** — al final para verificar que la migración se aplicó correctamente y genera los 13 inserts esperados.

5. **Decisión de delivery previa** — el Review Workload Forecast en `tasks.md` advierte `High` risk y `size:exception` necesaria (Designer auto ≈ 2.500 líneas). Solicitar confirmación de `size:exception` antes de apply.

6. **Gestionar el riesgo de diff cero** — la migración `dotnet ef migrations add` con diff cero (snapshot ya tiene 20 HasData) fue identificada como riesgo en el design; el approach elegido fue hand-author del `Up()`. Asegurar que ese riesgo se gestiona explícitamente en apply.

---

## Decisión Explícita Registrada

Este archivo de archive **no es un fracaso de ciclo SDD**. Es el registro de una decisión deliberada del usuario de **no aplicar** el cambio tal como fue planificado. El ciclo se cierra aquí por decisión explícita, no por bloqueo técnico ni por fallo de verificación.

---

*Generado por `sdd-archive` el 2026-08-12. Modo: hybrid (OpenSpec + Engram). Sin pass por `sdd-apply`.*
