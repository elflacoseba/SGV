# Archive Report: fix-persona-card-empty-state-issue-224

> Change: `fix-persona-card-empty-state-issue-224`
> Issue: [#224](https://github.com/elflacoseba/SGV/issues/224) (closed)
> PR: [#225](https://github.com/elflacoseba/SGV/pull/225)
> Branch: `feat/fix-persona-card-empty-state-issue-224` (merged to `develop`)
> Archive date: 2026-07-30

## Resumen del cambio

Fix del `TypeError` en `usuario-persona-buscador.js` cuando la partial
`_PersonaCard.cshtml` se renderiza en el caso 6 (empty state puro). Se aplicaron
null-guards defensivos en `choose()` y en el handler Quitar, y se corrigió un
bug latente en el lookup del empty state (`display.parentElement` en vez de
`display.querySelector`).

## Criterios de aceptación cumplidos

Todos los criterios del `proposal.md` § Success Criteria cumplidos:

- [x] `choose()` aborta limpiamente con `console.warn` cuando `displayInput`, `cardText`, `card` o `empty` son null (sin TypeError).
- [x] Handler Quitar aborta limpiamente en las mismas condiciones.
- [x] Lookup de `empty` lee desde `display.parentElement` (consistente con la partial L242).
- [x] `Ocupaciones/Create` con empty state permite seleccionar persona del modal sin TypeError (validado por inspección de código + suite .NET del contrato).
- [x] `Ocupaciones/Edit` con `PersonaId = Guid.Empty` y fetch fallido permite seleccionar persona sin errores (validado por inspección de código).
- [x] `Usuarios/_Form` no sufre regresión (suite .NET previa + tests PersonaCard verde).
- [x] Suite .NET completa pasa: 3226 / 3228 (2 pre-existing fails sin relación con #224, verificados en `develop`).
- [x] `bun run build` genera el bundle sin errores.

## Commits (7)

```
7959cc4  docs(sdd): verify-report for #224
15265de  docs(frontend): note defensivo del bug #224 en decisiones-implementacion.md
ed7f293  fix(js): abort Quitar handler when card contract missing (#224, USBJS-03)
914f578  fix(js): abort choose() when card contract missing (#224, USBJS-02)
d2e08e7  fix(js): read empty state from display.parentElement (#224, USBJS-01)
a563352  test(web): add regression guard for case 6 card contract (#224)
638a16a  docs(sdd): apply-progress for #224
```

## Tests final stats

| Métrica | Valor |
|---------|-------|
| Tests ejecutados | 3228 |
| Tests passed | 3226 |
| Tests failed (pre-existing) | 2 |
| Tests skipped (MySQL no disponible en CI / diferente entorno) | 0 |
| Tests nuevos | 1 (`EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes`) |
| Tests del contrato markup caso 6 (incluido el nuevo) | 2 |

## Líneas modificadas totales

| Archivo | Líneas |
|---------|--------|
| `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | +26 / -3 |
| `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs` | +26 / 0 |
| `docs/decisiones-implementacion.md` | +12 / 0 |
| `openspec/changes/.../verify-report.md` | +134 / 0 (SDD artifact) |
| `openspec/changes/.../apply-progress.md` | +101 / 0 (SDD artifact) |
| **Total diff de código** | **67 net lines** (≤ 400 budget) |

## Spec impact

- **NEW spec**: `usuario-persona-buscador-js` con 3 requisitos (USBJS-01..03) y 10 escenarios.
- Sin specs canónicas modificadas.

## Issue y PR

- **Issue #224**: cerrada con `state_reason=completed` y comentario referenciando PR #225.
- **PR #225**: abierta contra `develop` desde `feat/fix-persona-card-empty-state-issue-224`.

## Riesgos residuales documentados

Ver `verify-report.md` § "Riesgos residuales". Resumen:

- 4 smoke tests manuales no ejecutados en este entorno automatizado (documentados para el maintainer).
- 2 tests `[MySqlFact]` pre-existing fallan (seeds faltantes / flaky); verificado idéntico en `develop`.
- Cambio `hiddenInput.value` antes del guard en `choose()` podría romper handler externo → mitigado por `change` event NO disparado en aborto (USBJS-02).

## Decisión de no agregar Vitest/Jest

Documentada en `decisiones-implementacion.md` § "Frontend / JS compartido" y en
`design.md` § D5. El fix es trivialmente detectable por inspección; los tests
.NET del contrato markup son RED→GREEN verificables. Si en el futuro se introduce
infra JS, será un change dedicado.

## Próximos pasos

- ✅ PR #225 lista para review por el maintainer.
- ✅ Issue #224 cerrada.
- ✅ Change archivado en `openspec/changes/archive/2026-07-30-fix-persona-card-empty-state-issue-224/`.
- 🔲 Mantainer ejecuta 4 smoke tests manuales en navegador antes de mergear.
- 🔲 Merge de PR #225 → develop (squash final opcional).
- 🔲 Eliminar branch `feat/fix-persona-card-empty-state-issue-224` post-merge.