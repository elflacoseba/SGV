# Informe de Archivo: Issue #253 — Auditoría drill-down pierde `userName`

## Resumen Ejecutivo

El cambio #253 cerró el bug de binding en `DetailsModel.OnGetAsync` donde `IndexModel` emitía `userName` en la URL de drill-down pero el detalle bindeaba `userId`, provocando la pérdida del filtro de usuario en la navegación round-trip.

**Veredicto**: PASS ✅
**Fecha de archivo**: 2026-08-04
**Modo**: Strict TDD
**Cambio absorbido**: `auditoria-drilldown-username-filter`

La corrección consistió en 4 cambios quirúrgicos en `Details.cshtml.cs`: renombrar la propiedad `UserId` → `UserName`, el binding `[FromQuery(Name = "userId")]` → `[FromQuery(Name = "userName")]`, actualizar `BuildBackUrl()` para emitir `userName = UserName`, y actualizar el doc-comment. Se agregó un test de regresión `[Theory]` con 2 casos que cubre tanto el filtro activo como la navegación directa sin filtro.

## Artefactos del Cambio

| Capa | Path |
|------|------|
| Proposal | `openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/proposal.md` |
| Exploration | `openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/exploration.md` |
| Spec (delta) | `openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/specs/auditoria-drilldown-username-filter/spec.md` |
| Design | `openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/design.md` |
| Tasks | `openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/tasks.md` |
| Apply progress | `openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/apply-progress.md` |
| Verify report | `openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/verify-report.md` |
| Test diff | `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` (68 líneas agregadas) |
| Production diff | `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` (10 líneas modificadas) |

## Cumplimiento de Requisitos

| Requisito | Escenarios | Resultado |
|------------|-------------|-----------|
| REQ-1: Details bindea `userName` desde query string | 2 | ✅ COMPLIANT |
| REQ-2: Back-link preserva el filtro `userName` | 2 | ✅ COMPLIANT |
| REQ-3: Test de regresión del round-trip `userName` | 2 | ✅ COMPLIANT |

**Total**: 3/3 requisitos compliant · 6/6 escenarios compliant

## Tareas

| Tarea | Estado |
|-------|--------|
| 1.1 Escribir test de regresión `[Theory]` round-trip | ✅ Completa |
| 1.2 Ejecutar RED y confirmar falla | ✅ Completa |
| 2.1 Renombrar propiedad y binding `UserId`→`UserName` | ✅ Completa |
| 2.2 Actualizar `BuildBackUrl()` | ✅ Completa |
| 2.3 Ejecutar GREEN y confirmar pass | ✅ Completa |
| 3.1 Build `dotnet build SGV.slnx` | ✅ 0 errores |
| 3.2 Test suite `dotnet test SGV.slnx` | ✅ 1406/1406 pass |
| 3.3 Review diff | ✅ Dentro de budget |

**Total**: 8/8 checks completos · 0 tareas pendientes

## Spec Absorbida en Source of Truth

| Spec | Acción | Detalles |
|------|--------|----------|
| `auditoria-drilldown-username-filter` | **Creada** | Nueva spec copiada a `openspec/specs/auditoria-drilldown-username-filter/spec.md`. No existía spec previa para este capability. |

## Métricas de Verificación

- **Build**: `dotnet build SGV.slnx` → 0 errores, 4 warnings (preexistentes)
- **Tests focalizados**: 6/6 pass (`AuditoriasDetailsTests` filter)
- **Tests módulo Auditoria**: 97/97 pass
- **Suite web completa**: 1406/1406 pass
- **Diff**: 73 líneas añadidas / 5 borradas en 2 archivos

## Observaciones Engram Vinculadas

| ID | Tipo | Tema |
|-----|------|------|
| #1675 | bugfix | Issue #253 audit drill-down userName binding mismatch |
| #1676 | architecture | `sdd/2026-08-04-audit-drilldown-username-lost-issue-253/proposal` |
| #1677 | architecture | `sdd/…/spec` |
| #1678 | architecture | `sdd/…/design` |
| #1679 | architecture | `sdd/…/tasks` |
| #1680 | architecture | Apply Progress: Issue #253 |
| #1683 | architecture | `sdd/…/verify-report` |
| #1682 | session_summary | Session summary: sgv |

## Issues Encontrados

| Severidad | Cantidad | Notas |
|-----------|----------|-------|
| CRITICAL | 0 | Ninguno |
| WARNING | 0 | Ninguno |
| SUGGESTION | 1 | Drift menor en doc-comment de `Index.cshtml.cs:20` (menciona `UserId` cuando el binding real es `userName`). Fuera de scope del fix; registrado en `design.md` §"Preguntas abiertas". |

## Decisión de Diseño Registrada

| Decisión | Rationale |
|-----------|-----------|
| Renombrar propiedad + binding (no aliasar) | Ningún consumidor interno referencia `DetailsModel.UserId`. El alias duplicaría estado y rompería la simetría con `IndexModel.UserName`. El renombrado limpio refleja la realidad semántica. |

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Regresión accidental en tests que referencien `DetailsModel.UserId` | Baja | Búsqueda en repo sin matches. Los `UserId` restantes son del DTO (`detalle.UserId`), fuera del scope. |
| Back-link arrastrando `userName=jperez` espurio cuando no hay filtro | Baja (mitigada por test) | Test cubre explícitamente `Assert.DoesNotContain("userName=jperez", content)` en caso `null`. |
| MySqlFact flaky failures en suite completa | Baja (preexistente) | Rerun confirmó 3415/3415 pass. |

## Siguiente Paso Recomendado

1. **(Opcional)** Follow-up de limpieza para alinear el doc-comment drift en `Index.cshtml.cs:20` y cerrar la pregunta abierta de `design.md`.
2. El ciclo SDD para issue #253 está completo. Listo para el próximo cambio.

---

## Detalle del Cambio de Archivo

```
openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/
  → openspec/changes/archive/2026-08-04-audit-drilldown-username-lost-issue-253/

openspec/specs/auditoria-drilldown-username-filter/ (nueva)
  └── spec.md

Active changes: 2026-08-04-audit-drilldown-username-lost-issue-253 removido ✅
```

## Skill Resolution

| Skill | Cargado | Usado para |
|-------|---------|------------|
| `sdd-archive` | ✅ | Marco de archivo, merge de specs, movimiento a archive |
| `cognitive-doc-design` | ✅ | Estructura del informe de archivo con progressive disclosure y chunking |
| `sdd-verify` | ✅ (previo) | Verificación PASS documentada en verify-report |
| `dotnet-csharp` | ✅ (previo) | Contexto C# 14, binding patterns, Razor Pages |
| `dotnet-xunit` | ✅ (previo) | Convenciones `[Theory]`/`[InlineData]` en tests |

---

*Archivo generado: 2026-08-04*
*Expedido por: `sdd-archive` executor*
*Modo: openspec (filesystem)*
