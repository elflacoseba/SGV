# Apply Progress: módulo de Habilidades en SGV.Web con paridad completa con Cargos

**Change**: `modulo-habilidades-paridad-cargos`
**Mode**: Strict TDD (`openspec/config.yaml` → `strict_tdd: true`)
**Delivery**: Stacked-to-main, 4 PRs (Slice 1/A, Slice 2, Slice 3A, Slice 3B)

Estado inicial: baseline limpio, sin cambios previos. `dotnet build SGV.slnx --configuration Release` produce 0 warnings / 0 errors.

## Estrategia

- Test runner: `dotnet test SGV.slnx`.
- Reglas: cada task declara su test xUnit; flujo test primero (rojo) → implementación (verde) → refactor. Los commits se hacen por work-unit cohesivo, con prefijo conventional y sin Co-Authored-By.
- Estrategia stacked-to-main: cada PR commitea sobre el HEAD local y se valida antes de pasar al siguiente.

## Resumen por PR

| PR | Estado | Tasks # | Commits | Verif build | Verif tests |
|----|--------|---------|---------|-------------|-------------|
| PR 1 — Slice 1/A (Backend + tests xUnit) | Pendiente | #1.1 a #1.11 | — | — | — |
| PR 2 — Slice 2 (Cliente + shell) | Pendiente | #2.1 a #2.5 | — | — | — |
| PR 3 — Slice 3A (Index + JS + tests listado) | Pendiente | #3.1 a #3.3 | — | — | — |
| PR 4 — Slice 3B (Create/Edit/Details + _Form + tests + anti-drift) | Pendiente | #3.4 a #3.9 | — | — | — |

## TDD Cycle Evidence

> Tabla consolidada al final del apply (cuando todas las tasks estén completadas).

## Próximo paso lógico

`sdd-verify` cuando todos los PRs estén verdes y registrados.