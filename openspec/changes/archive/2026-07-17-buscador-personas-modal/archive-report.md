# Archive Report: Buscador modal reutilizable de Personas

> **Cambio:** `2026-07-17-buscador-personas-modal`
> **Issue:** [#157](https://github.com/elflacoseba/SGV/issues/157)
> **Archivado el:** 2026-07-17
> **Branch final:** develop @ `ff50b8ab`
> **Modo:** hybrid (openspec + Engram)
> **SDD Cycle:** COMPLETE

## Resumen ejecutivo

Cierre del ciclo SDD del buscador modal reutilizable de Personas en Crear/Editar Usuario. Reemplaza el combo plano de `IPersonaOptionsProvider` por un selector modal Bootstrap 5 paginado server-side vía `GET /api/v1/personas/consulta?soloSinUsuario=true`. 3 PRs encadenados stacked-to-main mergeados a develop.

## PRs mergeados

| PR | Slice | Commits | Archivos | Diff | URL |
|----|-------|---------|----------|------|-----|
| #158 | PR-1 backend (WU-1..3) | 5 | 12 | +600/-6 | https://github.com/elflacoseba/SGV/pull/158 |
| #159 | PR-2 cliente (WU-4) | 3 | 5 | +202/-12 | https://github.com/elflacoseba/SGV/pull/159 |
| #160 | PR-3 frontend (WU-5..8) | 5 | 18 | +763/-451 (size:exception) | https://github.com/elflacoseba/SGV/pull/160 |

## Specs sincronizadas

| Domain | Action | Detalle |
|--------|--------|---------|
| `persona-management` | Updated | REQ-PM-01 agregado + endpoint `/consulta` extendido con `soloSinUsuario` |
| `usuario-web-crear-editar` | Updated | REQ-UCE-02 redefinido (selector modal) + UCE-08/09/10 agregados |
| `usuario-web-selector-persona-buscador` | Created | NEW completo (REQ-USB-01..11) |

## Decisions aplicadas (D-01..D-10)

| ID | Decisión | Estado final |
|----|----------|--------------|
| D-01 | Query param `soloSinUsuario=true\|false` | ✅ Implementado en repo, service, controller, client |
| D-02 | `PersonaListQuery` + `bool? SoloSinUsuario = null` | ✅ Nullable back-compat |
| D-03 | ViewData contrato modal (`ModalId`, `HiddenInputName`, etc.) | ✅ `_PersonaBuscadorModal.cshtml` |
| D-04 | Paginación numérica con elipsis si >7 | ✅ JS `renderPagination()` |
| D-05 | Eliminación `IPersonaOptionsProvider` + tests | ✅ 0 hits en `src/`/`tests/` |
| D-06 | Create.OnGetAsync invoca `QueryAsync(page=1, pageSize=1)` para banner | ✅ REQ-UCE-09 |
| D-07 | `aria-live="polite"` SHOULD | ✅ Implementado como SHOULD |
| D-08 | JS modular en `wwwroot/js/pages/usuario-persona-buscador.js` | ✅ 204 líneas, bundle OK |
| D-09 | Sin `BuscarAsync` — un solo endpoint `/consulta` | ✅ |
| D-10 | 409 → `ModelState.AddModelError("Input.PersonaId", ...)` | ✅ Siguiendo tasks/spec (no design.md que sugería `string.Empty`) |

## Deviations documentadas

| # | Deviation | Resolución |
|---|-----------|------------|
| 1 | BFF same-origin en `Program.cs:212-229` (proxy cookie-auth → API). No estaba en design.md. | Aceptado. RIS-001/RIS-002 como follow-ups. |
| 2 | `UsuarioDto` sin `PersonaDisplay`/`Documento` → Edit card muestra solo `Apellidos, Nombres` | Aceptado. REL-001 como follow-up para extender DTO. |
| 3 | D-10 contradictorio: design.md sugería `string.Empty`, tasks/spec exigían `Input.PersonaId` | Aceptado `Input.PersonaId` (más verificable, mejor UX). |
| 4 | Password reingresable en POST 409 (Razor no preserva passwords) | Aceptado (práctica auth vigente). |
| 5 | Tests BFF + POST Edit sin Persona agregados durante implementación | Aceptado (cobertura fortalece suite). |

## Veredicto del verify final (PR-3)

**PASS WITH WARNINGS** — 0 BLOCKER / 0 CRITICAL / 5 WARNING / 8 SUGGESTION.

Gate `pre-pr`: `allow`. Lineage: `review-9520f99489d7dbeb`, tier `high` (4 lenses: risk/resilience/readability/reliability).

### Warnings bounded (no bloqueantes)

| ID | Lens | Claim | Acción recomendada |
|----|------|-------|--------------------|
| RIS-001 | risk | BFF no acota longitud de `search` | Hardening post-archive |
| RIS-002 | risk | BFF hard-coda `Sort="apellidos_asc"` y `Segmento=Activas` | Exponer parámetros con whitelist |
| RES-001 | resilience | BFF no envuelve `QueryAsync` en try/catch | Mapear a ProblemDetails |
| RES-002 | resilience | JS sin `AbortController` | Minor race condition |
| REL-001 | reliability | REQ-USB-02 violación parcial en Edit card | Extender UsuarioDto con PersonaDisplay |

### Follow-up issues sugeridas

1. Extender `UsuarioDto` con `PersonaDisplay`/`Documento`/`Legajo` (REL-001)
2. Hardening BFF: `?search` length cap + sort whitelist (RIS-001/002)
3. BFF manejo explícito de 5xx upstream (RES-001)

## Strict TDD

8 WU implementadas con RED → GREEN, documentado en `apply-progress.md`, `apply-progress-pr2.md`, `apply-progress-pr3.md`.

| WU | Tests RED | Tests GREEN final |
|----|-----------|-------------------|
| WU-1 | 4 `[MySqlFact]` | Suite backend completa |
| WU-2 | 4 `[Fact]` | ídem |
| WU-3 | 4 `[ApiIntegration]` | ídem |
| WU-4 | 5 tests / 7 invocaciones | 53/53 client tests |
| WU-5 | 3 `[WebIntegration]` | 95/95 frontend tests |
| WU-6 | 2 `[WebIntegration]` | 95/95 |
| WU-7 | 3 `[WebIntegration]` | 95/95 |
| WU-8 | BFF 404 → GREEN | 95/95 + suite 2440/2440 |

## Conventional commits

Todos los commits siguen conventional commits en español/inglés sin `Co-Authored-By`.

## Artefactos del archive

- `proposal.md` (63 líneas)
- `design.md` (78 líneas, D-01..D-10)
- `tasks.md` (8/8 WU completas)
- `specs/persona-management/spec.md` (delta)
- `specs/usuario-web-crear-editar/spec.md` (delta)
- `specs/usuario-web-selector-persona-buscador/spec.md` (delta)
- `apply-progress.md`
- `apply-progress-pr2.md`
- `apply-progress-pr3.md`
- `verify-report.md`
- `archive-report.md` (este archivo)

## Native Review Receipt

- **Lineage:** `review-9520f99489d7dbeb`
- **Store revision:** `sha256:cc3f11be7d97b23a2b9792e858aa3316130a088c81ee546ad5cbd893dcbfee45`
- **Receipt path:** `.git/gentle-ai/review-transactions/v2/review-9520f99489d7dbeb/review-receipt.json`
- **Gate result:** `allow`
- **Lenses:** risk, resilience, readability, reliability

## Cierre de issue #157

Issue lista para cerrarse con comentario que resuma los 3 PRs mergeados (#158 backend, #159 cliente, #160 frontend).

## Engram traceability

- `sdd/2026-07-17-buscador-personas-modal/proposal` — proposal
- `sdd/2026-07-17-buscador-personas-modal/spec` — spec delta specs
- `sdd/2026-07-17-buscador-personas-modal/design` — design D-01..D-10
- `sdd/2026-07-17-buscador-personas-modal/tasks` — tasks 8 WUs
- `sdd/2026-07-17-buscador-personas-modal/apply-pr1` — apply progress PR-1
- `sdd/2026-07-17-buscador-personas-modal/apply-pr2` — apply progress PR-2
- `sdd/2026-07-17-buscador-personas-modal/apply-pr3` — apply progress PR-3
- `sdd/2026-07-17-buscador-personas-modal/verify-pr3` — verify report PR-3
- `sdd/2026-07-17-buscador-personas-modal/archive-report` — este reporte
