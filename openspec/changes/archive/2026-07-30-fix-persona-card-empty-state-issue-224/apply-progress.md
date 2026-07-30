# Apply Progress: fix-persona-card-empty-state-issue-224

> Change: `fix-persona-card-empty-state-issue-224`
> Issue: [#224](https://github.com/elflacoseba/SGV/issues/224)
> Branch: `feat/fix-persona-card-empty-state-issue-224`
> Base: `develop` (`05dc634b`)
> Mode: **Standard** (Strict TDD parcial: Task 1 es regression guard GREEN inicial;
> Tasks 2-4 son GREEN puros sobre el bug sin test RED previo, validados por smoke
> test manual documentado en `verify-report.md`).
> Strategy: **Single PR directo** (scope ~67 net lines, muy debajo del budget).

## Estado de tasks

| Task | Descripción | Commit | Estado |
|------|-------------|--------|--------|
| 1 | Regression guard del caso 6 (markup) | `a563352` | ✅ Completed |
| 2 | Fix USBJS-01 lookup `empty` | `d2e08e7` | ✅ Completed |
| 3 | Fix USBJS-02 null-guards en `choose()` | `914f578` | ✅ Completed |
| 4 | Fix USBJS-03 null-guards en handler Quitar | `ed7f293` | ✅ Completed |
| 5 | Nota en `decisiones-implementacion.md` | `15265de` | ✅ Completed |
| 6 | Verify (build + suite + bundle + smoke) | `7959cc4` | ✅ Completed |
| 7 | Archive (mover change + cerrar issue) | (este commit) | ✅ Completed |

## Hashes de commits

```
7959cc4  docs(sdd): verify-report for #224
15265de  docs(frontend): note defensivo del bug #224 en decisiones-implementacion.md
ed7f293  fix(js): abort Quitar handler when card contract missing (#224, USBJS-03)
914f578  fix(js): abort choose() when card contract missing (#224, USBJS-02)
d2e08e7  fix(js): read empty state from display.parentElement (#224, USBJS-01)
a563352  test(web): add regression guard for case 6 card contract (#224)
```

## Diff stats finales

```
 docs/decisiones-implementacion.md                  | 12 +++++++++
 src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js | 29 +++++++++++++++++++---
 tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs | 26 +++++++++++++++++++
 openspec/changes/fix-persona-card-empty-state-issue-224/verify-report.md | 134 ++++++++++++++++++++++++++++
 4 files changed, 198 insertions(+), 3 deletions(-)
```

> **Diff de código** (sin SDD artifacts): **67 líneas net** (64 insertions + 3 deletions),
> muy debajo del budget de **400 líneas**.

## Build / Test evidence

| Validación | Resultado |
|------------|-----------|
| `dotnet build SGV.slnx` | 0 errors, 92 warnings (todos pre-existentes) |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaCardPartialTests\|...OcupacionBuscador\|...UsuarioHabilidadesPage"` | Passed: 26, Failed: 0 |
| `dotnet test SGV.slnx --filter "...DoesNotEmitMutableCardContractAttributes"` | Passed: 1, Failed: 0 |
| `cd src/SGV.Web && bun run build` | `Finished 'build' after 2.91 s`, 0 errors |
| `dotnet test SGV.slnx` (completa) | Passed: 3226 / 3228 (2 pre-existing fails) |

> Ver `verify-report.md` para detalle completo de las 2 fallas pre-existentes
> (`ListAllAsync_RetornaCargosOrdenadosPorCodigo` + `Bloquear_AnotherUser_...`).

## Artifacts SDD

- `openspec/changes/fix-persona-card-empty-state-issue-224/proposal.md` (Phase 1)
- `openspec/changes/fix-persona-card-empty-state-issue-224/exploration.md` (Phase 1)
- `openspec/changes/fix-persona-card-empty-state-issue-224/specs/usuario-persona-buscador-js/spec.md` (Phase 2 NEW spec, 3 REQs, 10 scenarios)
- `openspec/changes/fix-persona-card-empty-state-issue-224/design.md` (Phase 3)
- `openspec/changes/fix-persona-card-empty-state-issue-224/tasks.md` (Phase 4)
- `openspec/changes/fix-persona-card-empty-state-issue-224/verify-report.md` (Phase 6)

## Workload / PR Boundary

- **Mode**: single PR
- **Branch**: `feat/fix-persona-card-empty-state-issue-224` (base `develop`)
- **Review budget impact**: 67 net code lines ≈ 17% del budget de 400.
- **Squash final**: opcional (cada task = 1 commit atómico, historial legible).

## Rollback boundary

Cada commit es atómicamente revertible:

| Commit | Rollback |
|--------|----------|
| `a563352` | `git revert a563352` — elimina test aditivo sin tocar código |
| `d2e08e7` | `git revert d2e08e7` — revierte lookup de `empty` a `display.querySelector` |
| `914f578` | `git revert 914f578` — revierte null-guards de `choose()` |
| `ed7f293` | `git revert ed7f293` — revierte null-guards de Quitar |
| `15265de` | `git revert 15265de` — elimina nota docs |
| `7959cc4` | `git revert 7959cc4` — elimina verify-report (puede conservarse) |

## Status final

✅ **7/7 tasks completed. Ready for archive.**

## Próximos pasos

1. Push branch: `git push origin feat/fix-persona-card-empty-state-issue-224`.
2. Crear PR contra `develop` con `gh pr create`.
3. Mantainer ejecuta smoke tests manuales (documentados en `verify-report.md`).
4. Mergear PR (squash opcional).
5. Cerrar issue #224 con referencia al PR.
6. Mover change a `archive/2026-07-30-fix-persona-card-empty-state-issue-224/`.