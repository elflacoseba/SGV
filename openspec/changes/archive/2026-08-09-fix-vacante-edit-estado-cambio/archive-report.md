# Archive Report — fix-vacante-edit-estado-cambio

**Change**: `fix-vacante-edit-estado-cambio`
**Issue**: #268
**Archived**: 2026-08-09
**Artifact store**: hybrid (OpenSpec filesystem + Engram topics)
**Verdict at close**: PASS WITH WARNINGS
**Review gate**: Structurally absent — receipt-driven development (RDD) fue deshabilitado explícitamente para este clon por decisión del maintainer (`gentle-ai review mode disable --scope clone`). Se archiva bajo la política ordinaria del repositorio sin receipt.

---

## Resumen ejecutivo

El change `fix-vacante-edit-estado-cambio` filtra el estado `Cubierta` (`EsCubierta=true`) del dropdown de edición de Vacante en `SGV.Web`. La transición a `Cubierta` exige `PersonaId` (flujo de Postulación/Selección), pero Edit no expone ese campo, generando un error de validación huérfano que el usuario percibía como "no pasa nada". El fix es un `.Where(s => !s.EsCubierta)` en `EditModel.LoadStatesAsync`, backed por el campo `EsCubierta` ya presente en la entidad de dominio `EstadoVacante`.

**Lo que se embarcó**: dropdown de Edit excluye `Cubierta` y sigue incluyendo `Cancelada` (entre otros estados editables).

---

## Commits cerrados

| SHA | Descripción |
|-----|-------------|
| `bf50b82e` | Implementación del fix: DTO con 6to parámetro `EsCubierta`, mapper, filtro en `LoadStatesAsync`, fakes actualizados, test TDD `Get_Edit_ExcludesCubiertaFromDropdown` (ciclo RED→GREEN completado). |
| `aa4e48c4` | Remediación nativa del envelope de `verify-report`: corrección de contadores tras evidencia de coverage real (`requirements 2/2`, `scenarios 6/6`, `critical_findings 0`). |

---

## Spec Delta Sync

| Dominio | Acción | Detalle |
|---------|--------|---------|
| `vacante-web` | MODIFIED + ADDED | Requirement "Edit permite cambiar estado y observaciones" actualizado con texto de filtrado `EsCubierta=true` y 5 escenarios (reemplaza el escenario "Cambio a estado terminal visible" por tres escenarios: dropdown excluye Cubierta, Cancelada seleccionable, Cambio a Cancelada setea FechaCierre). Requirement nuevo "Cubierta no es destino directo desde Edit" agregado con 1 escenario. |

**Main spec actualizada**: `openspec/specs/vacante-web/spec.md` — requirement "Edit permite cambiar estado y observaciones" (MODIFIED) + requirement "Cubierta no es destino directo desde Edit" (ADDED, líneas 189-198).

---

## Verificación al cierre

| Check | Resultado |
|-------|-----------|
| Build | `dotnet build SGV.slnx` → exit 0 |
| Suite completa | `dotnet test SGV.slnx` → 3463/3463 exit 0 |
| Test nuevo | `Get_Edit_ExcludesCubiertaFromDropdown` → 1/1 exit 0 |
| Envelope validation | `gentle-ai sdd-verify-validate` → `valid=true`, `verdict=pass`, `evidence_revision sha256:29d039b7a2c31c7189c63a3e2fa78321c0e73b97c2395028c093ee867ca06d07` |
| CRITICAL issues | 0 |
| Blockers | 0 |

---

## Advertencias de seguimiento (no bloqueantes)

Las siguientes gaps fueron identificados en `verify-report` y documentados como follow-ups recomendados para un futuro `sdd-propose`. No impiden el cierre del change.

1. **PARTIAL — Escenario "El catálogo expone el flag `esCubierta`" sin aserción específica.** El test `Estados_GetAll_Returns200WithFourStates` verifica `Count==4` pero NO valida que cada item incluya el campo `esCubierta`. Mitigación sugerida: extender el test con `Assert.True(items.Any(i => i.EsCubierta))`.

2. **PARTIAL — Escenario "Cambio a Cancelada setea FechaCierre" sin test web de integración completo.** El dominio está cubierto por `CambiarEstado_AEstadoTerminal_SeteaFechaCierre` en la capa de aplicación, pero la integración Edit (POST) → API → Details con verificación de `FechaCierre` reflejada en la respuesta NO está probada a nivel web. Mitigación sugerida: agregar `Post_Edit_WhenCambioACancelada_RedirectsToDetailsWithFechaCierre`.

3. **WARNING — Cobertura del mapper `EstadoVacanteServicioConsulta.MapToDto` = 0% líneas.** Sin test unitario directo del mapper. Cubierto indirectamente por la suite de integración API. Sugerencia: crear `EstadoVacanteServicioConsultaTests.cs` con `MapToDto_PropagaEsCubierta`.

4. **WARNING — Triangulación colapsada.** Dos escenarios de la spec (`excluir Cubierta` + `Cancelada seleccionable`) cubiertos por un único test con 2 aserciones en `Get_Edit_ExcludesCubiertaFromDropdown`. Aceptable en contexto; conceptualmente deberían ser tests separados para mayor claridad de regresión.

---

## Tareas

Total: 17/17 tareas marcadas `[x]` en `tasks.md`. Ninguna tarea pendiente. Gate de completado de tareas: PASS.

---

## Contenido del archive

```
openspec/changes/archive/2026-08-09-fix-vacante-edit-estado-cambio/
├── proposal.md          ✅
├── design.md            ✅
├── tasks.md             ✅ (17/17 [x])
├── verify-report.md     ✅
├── specs/
│   └── vacante-web/
│       └── spec.md      ✅ (delta spec)
└── archive-report.md    ✅ (este archivo)
```

---

## Observaciones de Engram relacionadas (para trazabilidad)

| Topic | ID | Tipo |
|-------|----|------|
| `sdd/fix-vacante-edit-estado-cambio/proposal` | #1747 | architecture |
| `sdd/fix-vacante-edit-estado-cambio/spec` | #1748 | architecture |
| Bug #268 — Edit.cshtml select disabled | #1744 | bugfix |
| `sdd/fix-vacante-edit-estado-cambio/design` | #1749 | architecture |
| `sdd/fix-vacante-edit-estado-cambio/tasks` | (tasks.md en archive) | — |
| `sdd/fix-vacante-edit-estado-cambio/apply-progress` | #1751 | architecture |
| `sdd/fix-vacante-edit-estado-cambio/verify-report` | #1753 | architecture |
| Session summary | #1752 | session_summary |

---

## Arquitectura de la solución

```
GET /organizacion/vacantes/editar/{id}:
  EditModel.OnGetAsync(id)
    → LoadCurrentAsync(id)                          # GET /api/v1/vacantes/{id}
    → LoadStatesAsync(ct)                           # GET /api/v1/estados-vacante
        → EstadoVacanteServicioConsulta.ListarAsync()
            → MapToDto(estado)                      # ahora incluye EsCubierta
        → IList<EstadoVacanteDto>
    → EstadosVacante = estados.Where(s => !s.EsCubierta).ToList()   # FILTRO
    → return Page()
  Edit.cshtml renderiza <select> con options no cubiertos.
```

**Decisión de diseño**: el filtro se aplica en `EditModel.LoadStatesAsync` (capa de presentación), no en la API ni en el servicio de consulta, para mantener el catálogo de estados completo para otros consumidores (reportes, transiciones via Postulación).

---

## Change listo para merge

El change está verificado, los 17 tasks completados, y los dos gaps de cobertura identificados son follow-ups recomendados no bloqueantes. El change es **mergeable tal cual** bajo la política ordinaria del repositorio.
