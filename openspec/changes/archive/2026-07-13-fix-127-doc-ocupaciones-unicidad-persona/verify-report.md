# Verify report: Alineación doc/modelo de unicidad de Ocupaciones (issue #127)

## Resumen
**PASS CON OBSERVACIONES**. El change implementa exactamente lo que pidió el issue #127: alinea la prosa de `docs/decisiones-implementacion.md` con el modelo EF Core vigente, elimina la nota sobre cargos concurrentes y blinda el resultado con un test de coherencia (`tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs`) que prueba el ciclo RED→GREEN ante el drift textual. El modelo está intacto, la spec canónica `sgv-database` está intacta, el scope discipline se respeta y la suite completa corre verde (1390/1390 tests, 0 fail, 0 skip). Encontré 0 CRITICAL, 0 WARNING y 1 SUGGESTION (un anglicismo "no se enforce" en la prosa, no bloqueante).

## Acceptance criteria del issue
| # | Criterio (proposal §Acceptance) | Estado | Evidencia |
|---|----------------------------------|--------|-----------|
| 1 | `docs/decisiones-implementacion.md:19-21` describe los DOS invariantes con nombres de shadow property explícitos y SIN frase de cargos concurrentes | PASS | `docs/decisiones-implementacion.md:21` cita `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique`; grep confirma ausencia de "cargos concurrentes", "tipo de ocupación" y "porcentaje de dedicación". |
| 2 | Test `CoherenciaDecisionesImplementacionTests` asserta presencia de ambos shadow properties Y ausencia de `ActivePersonaIdUnique`/"única por persona" sin matizar | PASS | `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs:30-73`; tres `[Fact]` con nombres descriptivos y aserciones puntuales. |
| 3 | `dotnet test SGV.slnx` pasa verde (persistencia + aplicación + API + compat) | PASS | Suite `--filter "FullyQualifiedName!~Web"`: **1390 passed, 0 failed, 0 skipped**. Subset Ocupacion: **135/135**. |

## Spec compliance
**Spec**: `openspec/changes/2026-07-13-fix-127-doc-ocupaciones-unicidad-persona/specs/decisiones-implementacion-mantenimiento/spec.md`.

| Requirement / Scenario | Estado | Evidencia |
|------------------------|--------|-----------|
| **REQ-1 Coherencia prosa-modelo** | PASS | L21 declara explícitamente los DOS invariantes con nombres de shadow property. Cita `ActivePersonaIdUnique` al matizar la regla no vigente. |
| REQ-1.Scenario: sección declara los DOS invariantes vigentes | PASS | Test `Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes` pasa verde (24 ms). |
| REQ-1.Scenario: modelo expone shadow properties esperadas | PASS | `OcupacionConfiguracion.cs:42-53` define `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique` con índices únicos; `ActivePersonaIdUnique` ausente. Test `Modelo_Ocupaciones_ExponeShadowPropertiesUnicasVigentes` pasa verde. |
| REQ-1.Scenario: test de coherencia pasa verde en CI | PASS | Filtro `~CoherenciaDecisionesImplementacion`: **3/3 passed, 28 ms** (< 5 s). |
| **REQ-2 Nota de cargos concurrentes removida** | PASS | grep contra L21: "Si el negocio requiere cargos concurrentes" = 0 ocurrencias. También ausente "tipo de ocupación" y "porcentaje de dedicación". |
| REQ-2.Scenario: ausencia de la nota de extensibilidad | PASS | Test `Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes` pasa verde. |
| **Fuera de alcance respetado** | PASS | git diff/grep confirman que `src/SGV.Infraestructura/`, `src/SGV.Aplicacion/`, `src/SGV.Dominio/`, `src/SGV.Api/`, `src/SGV.Web/` y `openspec/specs/sgv-database/spec.md` están intactos. |

## Test report
**RED (markdown revertido vía `git stash` para baseline)**:
```
Failed SGV.Tests.Docs.CoherenciaDecisionesImplementacionTests.Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes [1 ms]
  Assert.DoesNotContain() Failure: Sub-string found
  Found:  "Si el negocio requiere cargos concurrente"
Failed SGV.Tests.Docs.CoherenciaDecisionesImplementacionTests.Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes [< 1 ms]
  Assert.Contains() Failure: Sub-string not found
  Not found: "ActivePuestoIdUnique"
Failed!  - Failed: 2, Passed: 1, Skipped: 0, Total: 3, Duration: 27 ms
```
Reproducido verbatim lo declarado en `apply-progress.md`. El test de modelo siempre queda verde porque el modelo EF Core ya es correcto (este cambio sólo alinea prosa).

**GREEN (markdown actual)**:
```
Correctas! - Con error: 0, Superado: 3, Omitido: 0, Total: 3, Duración: 24 ms
```

**Suite completa (regresión)** — `dotnet test SGV.slnx --no-build --configuration Release --filter "FullyQualifiedName!~Web"`:
```
Correctas! - Con error: 0, Superado: 1390, Omitido: 0, Total: 1390, Duración: 22 s
```

**Subset Ocupacion**:
```
Correctas! - Con error: 0, Superado: 135, Omitido: 0, Total: 135, Duración: 2 s
```

Sin fallos nuevos del change. Las fallas pre-existentes en `Web/*` (bootstrap de `WebApplicationFactory`) ya documentadas en `apply-progress.md:77` y fuera de scope.

## Scope discipline
```
$ git status --porcelain
 M docs/decisiones-implementacion.md
?? openspec/changes/2026-07-13-fix-127-doc-ocupaciones-unicidad-persona/   ← change dir propio (expected)
?? tests/SGV.Tests/Docs/                                                   ← nuevo test (expected)

$ git diff --stat
 docs/decisiones-implementacion.md | 2 +-   (1 inserción, 1 borrado)
```

| Area | Estado | Detalle |
|------|--------|---------|
| `docs/decisiones-implementacion.md` | Modificado | Sólo L21 (1 línea reemplazada dentro de "Ocupaciones Activas" L19-21). |
| `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` | Nuevo | 119 líneas, namespace `SGV.Tests.Docs`, 3 `[Fact]`. |
| `src/SGV.Infraestructura/` | Intacto | `OcupacionConfiguracion.cs:42-53` mantiene `ActivePuestoIdUnique` + `ActivePersonaPuestoUnique` con `IsUnique()`. |
| `src/SGV.Aplicacion/`, `src/SGV.Dominio/`, `src/SGV.Api/`, `src/SGV.Web/` | Intactos | Sin cambios. |
| `openspec/specs/sgv-database/spec.md` | Intacto | Líneas 298-300 ("Historial de Ocupaciones") y 312-321 ("Duplicado activo por…") sin tocar. La spec canónica sigue siendo la fuente de verdad. |
| `openspec/changes/2026-07-13-fix-127-doc-ocupaciones-unicidad-persona/` | Presente | Cambio SDD en curso (untracked). El orchestrator deberá commitearlos junto con el commit del fix. |
| `tests/SGV.Tests/SGV.Tests.csproj` | Intacto | **Sin nuevas dependencias** (Markdig/ReverseMarkdown/etc.). Sólo `System.Text.RegularExpressions`, `Microsoft.EntityFrameworkCore` y los namespaces existentes. |
| **Total líneas modificadas** | ~120 | 1 en markdown + 119 en test nuevo, dentro del presupuesto de 400. |

## Findings

### CRITICAL
Sin hallazgos CRITICAL.

### WARNING
Sin hallazgos WARNING.

### SUGGESTION
1. **Anglicismo "no se enforce" en `docs/decisiones-implementacion.md:21`.** La frase actual reza: *"La regla vigente de unicidad per-persona simple no se enforce; una futura restricción de ese tipo requeriría reintroducir la columna `ActivePersonaIdUnique`…"*. "Enforce" es un anglicismo en prosa técnica en español; opciones de reemplazo natural:
   - *"La regla vigente de unicidad per-persona simple **no se aplica**…"*
   - *"La regla vigente de unicidad per-persona simple **no se exige**…"*
   - *"La regla vigente de unicidad per-persona simple **no está enforzada**"* (anglicismo "enforzada" igualmente, evitar).
   
   No bloquea archive (el test pasa, el spec se cumple, el significado es inequívoco) pero conviene pulirlo en un commit fix-up si el equipo tiene la Convención "documentación SDD en español neutro y libre de anglicismos triviales" como prioritaria. Mencionado por `apply-progress.md:19` como observación previa.

2. **Anglicismo "per-persona" en `docs/decisiones-implementacion.md:21`.** Mismo lugar. Alternativa: "**por persona**" o "**por-persona**". Menor; "per-" tiene uso extendido en español técnico (p. ej. "perimetral") por lo que no urge.

## Commit readiness
Comando que el orchestrator correrá: `feat: alinear doc de Ocupaciones con modelo vigente (issue #127)` hacia `develop`.

| Check | Estado |
|-------|--------|
| Conventional commit prefix | OK (`feat:`) |
| Mensaje conciso, scoped al issue | OK |
| Sin atribución IA (`Co-Authored-By`) | OK (a confirmar por orchestrator antes de `git commit`) |
| Sin secretos en diff | OK (`git diff` no muestra credenciales, connection strings, JWT keys, etc.) |
| Sin debris de `.editorconfig`/IDE | OK (única línea tocada es prosa markdown) |
| Stage de archivos esperados | `git status` muestra exactamente: `docs/decisiones-implementacion.md` (M) + `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` (??) + `openspec/changes/2026-07-13-.../` (??). El orchestrator debería stagear los tres (o sólo los dos de código si decide mover los artefactos SDD a un commit separado). |
| PR body sugerido en `tasks.md:38` | OK, listo para usar. |

Nota: el orchestrator debería confirmar el stage final. Recomiendo commit único con los tres paths para mantener el work-unit (prosa + test + artefactos SDD juntos, mismos permisos).

## Recomendación al orchestrator
- **Recomendar `sdd-archive`**: el change cumple los dos requirements del spec, los tres scenarios RED→GREEN están reproducidos, la suite completa pasa verde (1390/1390) y el scope discipline es estricto (1 línea de prosa + 1 archivo de test nuevo + 0 cambios en modelo/spec). No hay CRITICAL ni WARNING.
- **Sugerencia opcional, no bloqueante**: si el equipo prioriza prosa en español neutro, abrir un commit fix-up de una línea reemplazando "no se enforce" por "no se aplica" (ver SUGGESTION §1). Si se decide hacer, ejecutar después del archive como cleanup atómico; no reabrir el change.
- **Próximo paso natural**: `sdd-archive` → generar `archive-report.md` referenciando issue #127 y el precedente archivado `2026-07-11-fix-active-puesto-id-unique-type` (issue #59).
