# Apply Progress — agrega-navegacion-personas-habilidades

> Issue #187 — botón Habilidades en Personas/Index (punto 1)
> Strategy: stacked-to-main · 3 chained PRs (PR A UI Personas, PR B Backend, PR C Frontend)

## Estado por PR

### PR A — UI Personas (50–80 líneas estimadas)

| Tarea | Estado | SHA | Notas |
|-------|--------|-----|-------|
| Branch + base | ✅ | — | `feat/agrega-navegacion-personas-habilidades-pr-a-ui-personas` based on `develop` |
| RED: tests A.1 + A.2 + A.3 (botón admin, no-admin, helper) | ✅ | `2c8e5d39` | 3 tests en `tests/SGV.Tests/Web/Persona/PersonasIndexHabilidadesButtonTests.cs` |
| GREEN: helper `BuildHabilidadesRouteValues` | ✅ | `2c8e5d39` | `src/SGV.Web/Pages/Personas/Index.cshtml.cs` (espejo de `BuildDetailsRouteValues`) |
| GREEN: botón Habilidades entre Detalle y Editar | ✅ | `2c8e5d39` | `src/SGV.Web/Pages/Personas/Index.cshtml` (anchor admin-only, gating `Model.EsAdministrador`) |
| Build | ✅ | `2c8e5d39` | `dotnet build SGV.slnx`: 0 errors |
| Suite completa | ✅ | `2c8e5d39` | `dotnet test SGV.slnx`: **2790/2790 PASS** (+3 desde baseline 2787) |

#### TDD Cycle Evidence (Strict TDD mode)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|-----------|-------|------------|-----|-------|-------------|----------|
| A.1 (admin→visible) | `PersonasIndexHabilidadesButtonTests.cs` | Integration (WebApplicationFactory) | ✅ 2787/2787 | ✅ Written (FAIL) | ✅ Pass | ✅ Caso admin + caso no-admin (triangulación de gating) | ➖ Markup limpio |
| A.2 (no-admin→oculto) | `PersonasIndexHabilidadesButtonTests.cs` | Integration (WebApplicationFactory) | ✅ 2787/2787 | ✅ Written (PASS trivial: no había botón) | ✅ Pass | ✅ Mismo test cubre regresión futura de gating | ➖ Limpio |
| A.3 (helper preserva contexto) | `PersonasIndexHabilidadesButtonTests.cs` | Integration (WebApplicationFactory) | ✅ 2787/2787 | ✅ Written (FAIL) | ✅ Pass | ➖ Single scenario (preservar contexto end-to-end) | ➖ Regex helper ajustado para query string opcional |

#### Decisiones técnicas durante implementación

- **Firma helper ajustada al PageModel real**: el orquestador sugirió
  `BuildHabilidadesRouteValues(PersonaListItemViewModel item, PersonaListQuery query)`
  pero el PageModel NO expone `Model.Query` como `PersonaListQuery` (esos
  viven en Contracts y se construyen dentro de `LoadAsync()`). Para mantener
  consistencia con `BuildEditRouteValues` y `BuildDetailsRouteValues` ya
  existentes en el mismo PageModel, la firma adoptada es:
  ```csharp
  public object BuildHabilidadesRouteValues(Guid id) => new
  {
      id,
      p = CurrentPage,
      search = Search,
      sort = Sort,
      returnStatus = Segmento
  };
  ```
  Las propiedades se leen del PageModel state vigente (`CurrentPage`, `Search`,
  `Sort`, `Segmento` ya están normalizadas en `OnGetAsync`).

- **Test 3 ajustado a `p=1`**: la implementación inicial usó `p=3` con
  search="juan" sobre 1 persona sembrada en el fake. El `QueryAsync` del
  `FakePersonaApiClient` aplica `Skip((page-1) * pageSize)` con
  `pageSize=10`, así que `p=3` produce `Skip(20)` y el resultado cae a
  página vacía. Se ajustó el test a `p=1` (también preservado en el href
  generado) para que la fila se renderice.

- **Regex del precedent extendido para query string opcional**: el regex
  original de `DetailsHabilidadesButtonTests` requería `"` inmediatamente
  después de `/habilidades` (sin query string). El botón en
  `Personas/Index` SÍ lleva query string (`?p=...&search=...&sort=...&returnStatus=...`)
  porque el helper preserva contexto. Se extendió con
  `(?:\?[^"]*)?` para admitir ese sufijo sin perder especificidad.

#### Archivos modificados/creados

| Path | Action | Líneas | Notas |
|------|--------|--------|-------|
| `src/SGV.Web/Pages/Personas/Index.cshtml.cs` | Modified | +19 | Helper `BuildHabilidadesRouteValues(Guid id)` |
| `src/SGV.Web/Pages/Personas/Index.cshtml` | Modified | +3 | Anchor admin-only entre Detalle y Editar |
| `tests/SGV.Tests/Web/Persona/PersonasIndexHabilidadesButtonTests.cs` | Created | +142 | 3 tests integration (WebApplicationFactory) |

#### Verificaciones ejecutadas

- `dotnet build SGV.slnx`: PASS (0 errors) — 2026-07-22 15:25
- `dotnet test --filter "FullyQualifiedName~PersonasIndexHabilidadesButton"`: **3/3 PASS** — 2026-07-22 15:25
- `dotnet test SGV.slnx`: **2790/2790 PASS** (baseline 2787 + 3 nuevos) — 2026-07-22 15:27

#### Rollback boundary

`git revert` del commit `2c8e5d39`:
- Revierte `Index.cshtml` (+3 líneas) — el botón Habilidades desaparece.
- Revierte `Index.cshtml.cs` (+19 líneas) — el helper deja de existir.
- Revierte `PersonasIndexHabilidadesButtonTests.cs` (elimina los 3 tests).
- Cero impacto en otras rutas. `PersonaHabilidades` (página destino) sigue
  accesible desde `Details.cshtml` (REJ-PM-01 precedente).

### PR B — Backend subreverso (pendiente)

Sin iniciar. Dependencias: ninguna. Base: `develop` (PR A ya mergeado).
Estimado: 400–500 líneas. Tareas B.1–B.11 según `tasks.md`.

### PR C — Frontend subreverso (pendiente)

Sin iniciar. Dependencias: PR B. Base: `develop` (PR B ya mergeado).
Estimado: 400–500 líneas. Tareas C.1–C.7 según `tasks.md`.

## Commits

| SHA | Mensaje | Tests | Notas |
|-----|---------|-------|-------|
| `2c8e5d39` | `feat(personas): agrega botón Habilidades en Personas/Index (admin-only)` | 2787 → 2790 | PR A consolidado en un solo commit (chico, ~80 líneas) |

## Limitaciones / notas

- Strict TDD observado: tests RED escritos antes de la implementación; los
  3 tests pasan tras la implementación. El test 2 (no-admin oculta) pasó
  trivialmente antes de la implementación (no había botón); ese patrón
  queda como guard de regresión para futuros cambios que relajen el gating.
- El botón Habilidades NO se renderiza en la vista `eliminadas` (cumple
  REQ-PM-NEW-ADMIN: `Model.EsAdministrador && !Model.IsDeletedView`).
- `returnStatus=activas` no aparece en el href cuando el segmento vigente
  es Activas (porque `Model.Segmento` se normaliza a `null`); sólo aparece
  `returnStatus=eliminadas` cuando se navega desde Eliminadas. Esto es
  coherente con el patrón existente (`BuildEditRouteValues`,
  `BuildDetailsRouteValues`) y no afecta la preservación de contexto
  porque el handler destino (`PersonaHabilidades.OnGetAsync`) sólo usa `id`.

## Próximo paso

PR B — Backend subreverso. El orquestador decidirá cuándo lanzar el
siguiente batch de apply.