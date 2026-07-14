# Archive report: Alineación doc/modelo de unicidad de Ocupaciones (issue #127)

## Resumen ejecutivo
El change #127 cierra una divergencia entre `docs/decisiones-implementacion.md:21` (que afirmaba "una única ocupación vigente por persona") y el modelo EF Core vigente (que sólo aplica unicidad por Puesto y por la combinación Persona + Puesto). Se adoptó la Opción A del exploration: reescribir la prosa al estado real del modelo, blindarla con un test de coherencia prosa↔modelo, y NO reintroducir la columna `ActivePersonaIdUnique`. Archivos finales: 1 línea de prosa modificada en `docs/decisiones-implementacion.md` (neto `+0`) + nuevo `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` de 119 líneas con 3 `[Fact]`.

## Issue y referencia
- **Issue GitHub**: #127 — "Documentación de Ocupaciones inconsistente con el modelo: unicidad por persona no implementada"
- **Change**: `2026-07-13-fix-127-doc-ocupaciones-unicidad-persona`
- **Fecha de archivo**: 2026-07-13
- **Decisión clave**: Opción A (corregir doc) + test de coherencia prosa↔modelo. NO se reintrodujo `ActivePersonaIdUnique`. NO se modificó la spec canónica `sgv-database`.

## Acceptance criteria (issue)
1. ✅ Documentación y modelo coinciden: `docs/decisiones-implementacion.md:21` cita explícitamente `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique` y declara los DOS invariantes vigentes (per-Puesto + per-Persona+Puesto).
2. ✅ Tests cubren el invariante documentado: `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` con 3 `[Fact]` que validan la prosa y la sombra del modelo EF Core.

## Spec delta archivado
- Capability: `decisiones-implementacion-mantenimiento`
- Requirements: REQ-1 (Coherencia prosa-modelo) + REQ-2 (Remoción de nota de cargos concurrentes)
- 4 escenarios Given/When/Then. Todos en PASS al momento del archive.
- Pendiente próximo: ninguno — la spec canónica `sgv-database` mantiene su texto sobre "Historial de Ocupaciones" sin modificaciones. Si en el futuro el negocio requiere reintroducir `ActivePersonaIdUnique`, este delta queda obsoleto y debe migrarse a un delta del canonical `sgv-database`.

## Archivos modificados y creados
| Path | Tipo | Resumen |
|------|------|---------|
| `docs/decisiones-implementacion.md` | Modificado | L21 reescrita dentro de la sección "Ocupaciones Activas"; 1 inserción / 1 borrado. |
| `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` | Nuevo | 119 líneas, namespace `SGV.Tests.Docs`, 3 `[Fact]` cubriendo prosa↔modelo y ausencia de nota de extensibilidad. |

Líneas totales: 119 (test) + 0 (markdown net diff) = ~119 modificadas dentro del presupuesto de revisión (400).

## Tests añadidos
- `CoherenciaDecisionesImplementacionTests.Doc_SeccionOcupacionesActivas_DeclaraLosDosInvariantesVigentes` (verde, 24 ms).
- `CoherenciaDecisionesImplementacionTests.Doc_SeccionOcupacionesActivas_NoContieneNotaDeCargosConcurrentes` (verde, 1 ms).
- `CoherenciaDecisionesImplementacionTests.Modelo_Ocupaciones_ExponeShadowPropertiesUnicasVigentes` (verde, 4 ms).
- RED → GREEN demostrado: el apply phase reprodujo los 2 fallos de prosa pre-cambio (mediante `git stash` + revert puntual) y los 3 verdes post-cambio.

## Migraciones y dependencias
- **Migraciones nuevas**: 0 (no se tocó persistencia).
- **Paquetes nuevos**: 0 (el test usa sólo `System.Text.RegularExpressions`, `Microsoft.EntityFrameworkCore` y dependencias ya presentes en `tests/SGV.Tests/SGV.Tests.csproj`).
- **APIs / contratos cambiados**: ninguno.

## Diff final vs main
```
$ git status --porcelain
 M docs/decisiones-implementacion.md
?? openspec/changes/archive/2026-07-13-fix-127-doc-ocupaciones-unicidad-persona/   ← archivado
?? tests/SGV.Tests/Docs/                                                           ← test nuevo

$ git diff --stat
 docs/decisiones-implementacion.md | 2 +-   (1 inserción, 1 borrado)
```

## Estado de los findings del verify
- CRITICAL: 0
- WARNING: 0
- SUGGESTION (post-fix del orchestrator): 0
  - El orchestrator aplicó un fix-up inline de la prosa reemplazando "no se enforce" por "no se aplica" y "per-persona" por "por persona", cumpliendo la SUGGESTION del verify. Test re-verificado: 3/3 verde.
- archive_ready: yes

## Pendiente para el orquestador (post-archive)
1. Crear branch y commit (`feat: alinear doc de Ocupaciones con modelo vigente (issue #127)`).
2. Commit aparte para los artefactos SDD (`chore: add SDD artifacts for issue #127`) — patrón usado por archive `2026-07-13-fix-124-persistence-mapper-reconstitute`.
3. Abrir PR hacia `develop` con cuerpo que cierre #127.

## Lecciones / Notas para el equipo
- Pattern reusable: cualquier sección de `decisiones-implementacion.md` puede blindarse parseándola con regex case-insensitive contra los shadow properties del modelo EF Core.
- Lección: el modelo `OcupacionEntity` ya tenía un test (`Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto`) que assertaba la ausencia de `ActivePersonaIdUnique`. Esa preexistencia fue clave para demostrar que el issue #127 era un problema de docs, no de código.
- Si en el futuro se quiere reintroducir `ActivePersonaIdUnique` (Opción B del issue), el workflow mínimo sería: issue aparte → sdd-new → propuesta → spec-delta a `sgv-database` → migrate forward-only con cleanup previo de duplicados → reactivation de los tests estructurales y de servicio.

## Artefactos del change archivado
- `exploration.md` (296 líneas)
- `proposal.md` (89 líneas)
- `specs/decisiones-implementacion-mantenimiento/spec.md` (50 líneas)
- `tasks.md` (90 líneas)
- `apply-progress.md` (100 líneas)
- `verify-report.md` (114 líneas)
- `archive-report.md` (éste)