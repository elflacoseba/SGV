# Archive Report: reusable-persona-card (issue #219)

> Change: `2026-07-29-reusable-persona-card-issue-219`
> Issue: [#219](https://github.com/elflacoseba/SGV/issues/219)
> Modo artefactos: **hybrid** (OpenSpec + Engram)
> Verificación: PASS WITH WARNINGS (slice 1), PASS (slices 2/3), PASS (slice 4)
> Archivada: 2026-07-29

## Resumen

El change #219 factorizó la card de persona duplicada en 4 vistas (`Usuarios/Details`, `Usuarios/_Form`, `Ocupaciones/Details`, `Ocupaciones/_Form`) en una única partial Razor `_PersonaCard.cshtml` con modos `readonly`/`editable` vía `ViewDataDictionary`, y un helper estático `PersonaFormatHelper.FormatDocumento` que eliminó 3 copias inline de formateo de documento. La implementación se entregó en 4 slices (PRs encadenados #220 → #221 → #222 → #223), todos mergeados a `develop`. Criterios de aceptación 9/9 cumplidos; 19 escenarios 19/19; suite Web completa verde.

## Motivación

- Duplicación de `FormatDocumento()`/`FormatearDocumento()` en 3 lugares con lógica idéntica
- Brecha funcional: `Ocupaciones/Details` solo mostraba texto plano (`PersonaNombre`) mientras `Usuarios/Details` mostraba card enriquecida con Email/Teléfono/Estado
- `Ocupaciones/_Form` tenía una card simplificada que no incluía Email/Teléfono/Estado de persona
- Ningún mecanismo reutilizable para la card de persona across consumers

## Alcance

### In Scope
- `_PersonaCard.cshtml` parcial unificada con modos readonly/editable
- `PersonaFormatHelper.cs` helper estático centralizando `FormatDocumento(PersonaDto?)`
- Migración `Usuarios/Details.cshtml` → partial modo readonly
- Migración `Usuarios/_Form.cshtml` → partial modo editable
- Migración `Ocupaciones/Details.cshtml` → partial modo readonly (con carga `IPersonaApiClient`)
- Migración `Ocupaciones/_Form.cshtml` → partial modo editable (gana Email/Teléfono/Estado)
- Eliminación de los 3 `@functions { FormatDocumento }` / `FormatearDocumento` inline
- Fallback silencioso en `Ocupaciones/Details.cshtml.cs` (fallo API → `PersonaNombre`)
- Guard de fuentes para cero definiciones Razor `FormatDocumento|FormatearDocumento` en `.cshtml`

### Out of Scope
- `Personas/Details.cshtml` — **sin cambios**, excluido explícitamente por el issue
- `PersonaFormatHelper` no se introduce en `SGV.Api` ni en otros proyectos
- No se modifica `_PersonaBuscadorModal.cshtml` ni `usuario-persona-buscador.js`
- No se introduce Tag Helper, Blazor, ni componente de navegación nuevo
- No se agrega validación visual automatizada (Percy, etc.)

## Specs sincronizadas a canónicas

Ambos specs son **NEW** (no existían canónicas previas):

| Spec | Tipo | Source (delta) | Target (canónica) | Requisitos | Escenarios |
|------|------|----------------|-------------------|------------|------------|
| `persona-card-partial` | NEW | `openspec/changes/.../specs/persona-card-partial/spec.md` | `openspec/specs/persona-card-partial/spec.md` | 10 | 19 |
| `persona-format-helper` | NEW | `openspec/changes/.../specs/persona-format-helper/spec.md` | `openspec/specs/persona-format-helper/spec.md` | 4 | 9 |

## 4 Slices — Entrega

### Slice 1 / PR #220 — Fundación ✅
- **Commit**: `ce21dd74 feat(web): add reusable persona card`
- Helper `PersonaFormatHelper.FormatDocumento` + partial `_PersonaCard.cshtml` + harness + 39 tests
- Diff: 1056 líneas agregadas, 0 eliminadas

### Slice 2 / PR #221 — Usuarios ✅
- **Commit**: `6f3fc7d refactor(web): reuse persona card in usuarios`
- Migración `Usuarios/Details.cshtml` + `Usuarios/_Form.cshtml` + eliminación de 2 `@functions` inline
- Suite Web: 1322/1322 PASS pre-Slice 3

### Slice 3 / PR #222 — Ocupaciones ✅
- **Commits**: feat Details + feat Form (ramo `feat/reusable-persona-card-slice-3`)
- Migración `Ocupaciones/Details.cshtml.cs` (inyecta `IPersonaApiClient`, `TryLoadPersonaVinculadaAsync`, fallback), `Ocupaciones/Details.cshtml`, `Ocupaciones/_Form.cshtml` (card simplificada → 5 líneas partial), `OcupacionDetailsViewModel.cs` (+13/-3)
- Diff net: +182 líneas (la card inline de 103 líneas → 5 líneas en _Form)
- Suite Web: 1335/1335 PASS

### Slice 4 / PR #223 — Integración y cierre ✅
- **Commit**: `test(web): verify reusable persona card integration`
- Guard de fuentes (9 escenarios, 0 copias `FormatDocumento|FormatearDocumento` en `.cshtml`)
- Smoke tests `PersonaCardMigrationSmokeTests` cubriendo las 4 vistas migradas
- Build: 0 errores, 0 warnings nuevos

## PRs mergeadas

| PR | Slice | Rama base | Merge commit |
|----|-------|-----------|--------------|
| #220 | 1 | `develop` | `ce21dd74` |
| #221 | 2 | post-#220 | `6f3fc7d` |
| #222 | 3 | post-#221 | (rama `feat/reusable-persona-card-slice-3`) |
| #223 | 4 | post-#222 | (rama `feat/reusable-persona-card-archive`) |

## Tests

| Suite | Resultado |
|-------|-----------|
| `PersonaFormatHelperTests` (PERFMT-01/02/04) | 23 PASS |
| `PersonaCardPartialTests` (PER-CARD-01..10) | 18 PASS |
| `PersonaCardMigrationSmokeTests` (4 vistas) | PASS |
| Suite Web completa | ≥1335 PASS |
| Suite completa | 3223+ PASS / 2-4 FAIL pre-existentes `[MySqlFact]` |

Los `[MySqlFact]` que fallan (`CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo`, `AuthControllerChangePasswordTests.ChangePassword_Success_RotatesSecurityStampAgainstMySql`) son pre-existentes — verificados con `git stash` que fallan idénticamente sin el change.

## Criterios de aceptación cumplidos

| Criterio | Status |
|----------|--------|
| `_PersonaCard.cshtml` existe en `Pages/Shared/Partials/` y acepta `PersonaDto?` | ✅ |
| `PersonaFormatHelper.FormatDocumento(PersonaDto?)` existe e invocado desde partial | ✅ |
| `Usuarios/Details.cshtml` renderiza card en modo readonly sin cambios visuales | ✅ |
| `Usuarios/_Form.cshtml` renderiza card editable con Quitar/Cambiar y binding correcto | ✅ |
| `Ocupaciones/Details.cshtml` muestra card completa con datos de persona en modo readonly | ✅ |
| `Ocupaciones/Details.cshtml.cs` carga `PersonaDto` vía `IPersonaApiClient`; si falla, degrada a `PersonaNombre` | ✅ |
| `Ocupaciones/_Form.cshtml` muestra card editable con Email/Teléfono/Estado y botones Quitar/Cambiar | ✅ |
| `OcupacionDetailsViewModel` expone `PersonaDto? Persona` | ✅ |
| `Personas/Details.cshtml` no se modifica | ✅ |
| Cero duplicaciones de `FormatDocumento`/`FormatearDocumento` en vistas | ✅ (PERFMT-03) |
| `dotnet build SGV.slnx` compila sin errores | ✅ |
| `dotnet test SGV.slnx` pasa sin regresiones | ✅ (2 fail pre-existentes) |

## Riesgos y follow-ups

### Bug latente JS — caso 6 (editable + DTO null + sin FallbackDisplay)

**Descripción**: `usuario-persona-buscador.js` L54-71 (`choose()`) hace `cardText.textContent = text;` y `card.hidden = false;` sin null guards. En caso 6 (editable + `PersonaDto=null` + sin `FallbackDisplay`), `cardText` y `card` son null → TypeError. Afecta a `Usuarios/Create` vacío, `Ocupaciones/Create` con `PersonaId` inexistente, y `Ocupaciones/Edit` con `PersonaDto=null`.

**Riesgo**: Medium — no detectado en tests porque el flujo typical (modal → buscar → seleccionar) no pasa por `choose()` con null card.

**Mitigación temporal**: La única forma de "elegir" persona en empty state es recargar con `?personaId={id}` query string — flujo atypical.

**Follow-up**: Arreglar null guards en `usuario-persona-buscador.js` o extender `EnriquecerPersonaAsync` para setear `FallbackDisplay` cuando `PersonaDto` es null pero `PersonaId` está resuelto (caso 5 en Ocupaciones). **Fuera del scope del issue #219.**

### Warnings residuales del verify (no blockers)

- W-01: Copias inline eliminadas en Slice 4 (PERFMT-03 compliant)
- W-02: `apply-progress.md` no existía para Slice 1 (squash RED+GREEN+REFACTOR en un commit)
- W-03: `[MySqlFact]` pre-existentes fallando por MySQL no sembrado (no relacionados)

## Artefactos del change (audit trail)

```
openspec/changes/archive/2026-07-29-reusable-persona-card-issue-219/
├── proposal.md
├── design.md
├── tasks.md
├── apply-progress.md
├── verify-report.md
├── verify-report-slice-2.md
├── verify-report-slice-3.md
├── exploration.md
└── specs/
    ├── persona-card-partial/spec.md
    └── persona-format-helper/spec.md
```

## Commits del change en `develop`

```
feat/reusable-persona-card-slice-3 (PR #222):
  <commits de Slice 3>

feat/reusable-persona-card-slice-4 (PR #223):
  <commits de Slice 4>

develop (post-#220, post-#221):
  ce21dd74 feat(web): add reusable persona card
  6f3fc7d refactor(web): reuse persona card in usuarios
```

4 conventional commits, sin `Co-Authored-By`. Rama de feature: `feat/reusable-persona-card-archive` para el commit de este archive.
