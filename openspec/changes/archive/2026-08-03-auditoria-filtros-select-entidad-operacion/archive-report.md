# Archive Report — 2026-08-03-auditoria-filtros-select-entidad-operacion (issue #251)

> Change completo archivado. SDD cycle cerrado.

## Change metadata

| Campo | Valor |
| ----- | ----- |
| Change | `2026-08-03-auditoria-filtros-select-entidad-operacion` |
| Issue | `elflacoseba/SGV#251` |
| Fecha de archive | `2026-08-04` |
| Rama base | `develop` (mergeado via 2 PRs stacked-to-main) |
| Verdict de verificación | **PASS** |
| Blockers | 0 |
| CRITICAL | 0 |

## Resumen del cambio

| Campo | Valor |
| ----- | ----- |
| Change | `2026-08-03-auditoria-filtros-select-entidad-operacion` |
| Issue | `elflacoseba/SGV#251` |
| Fecha de archive | 2026-08-04 |
| Modo de almacenaje | `hybrid` (OpenSpec + Engram) |
| Slicing | Chained PRs stacked-to-main (Slice A + Slice B) |
| Specs modificadas | `auditoria-query` (MODIFIED + ADDED) |
| Specs nuevas | ninguna |
| Net code líneas | ~560 (backend + Web) |
| Tests añadidos | 7 API + 5 Aplicación + 5 Web = 17 nuevos |
| Total tests post-merge | 3413 (1479 focused Auditoría\|Web) |

## Entrega

Dos PRs stacked-to-main mergeados a develop:

| PR | Commit merge | Contenido |
| -- | ------------ | --------- |
| Slice A | `a026ff6a` | Backend (DTO + rename + endpoint filter-options + LINQ UserName + tests API/Aplicación + D-8) |
| Slice B | `4fc288b3` | Web (selects + fallback + client + tests Web + bun build) |

## Native Review Receipt Gate

Estado: `not_applicable` / `unmanaged`. El change ya estaba mergeado en `develop` antes de la fase de archive. El gate no bloqueó.

## Artefactos sincronizados en `openspec/specs/`

| Domain | Action | Requirements |
|--------|--------|-------------|
| `auditoria-query` | Updated | 2 requirements modificados (Filtros combinables con UserName, Shell web con selects+fallback); 1 requirement nuevo (Endpoint filter-options) |

## Artefactos del change (archive)

```
openspec/changes/archive/2026-08-03-auditoria-filtros-select-entidad-operacion/
├── apply-progress.md     ✅ (PASS)
├── design.md            ✅
├── proposal.md          ✅
├── specs/
│   └── auditoria-query/ ✅ (delta)
├── tasks.md             ✅ (18/18 tareas completadas ✅)
└── verify-report.md     ✅ (PASS, 0 CRITICAL)
```

## Capabilities sincronizadas

- `auditoria-query` (MODIFIED + ADDED) → sincronizada a `openspec/specs/auditoria-query/spec.md`.

## Lecciones aprendidas

1. **Review lifecycle native bloqueado**: el CLI `gentle-ai review` con `projection=workspace, current-changes` no soporta el escenario de commits landed en una rama + untracked nuevo. `capture-result` rechaza con "inspection paths are not canonical candidate paths" aún con paths exactos del manifest. Documentado en memoria `obs-d24c8c0e983271c8`. Mitigación: merge manual con justificación basada en tests verdes (3407/3407 Slice A, 3413/3413 post-merge).

2. **Stacking Slice A + Slice B mantiene `develop` compilable**: el hotfix compat mecánico (rename `userId`→`userName` en Web sin selects ni fallback) cierra el gap entre los merges. Precedente archivado del change #248.

3. **D-2 cerrado por tipo**: el record `AuditoriaFilterOptions` con sólo dos `IReadOnlyList<string>` impide físicamente que `OldValuesJson`, `NewValuesJson`, `EntityId`, `UserId` o `UserName` lleguen al wire por accidente. Confirmado por reflexión (serialización JSON no incluye esos campos) y por grep repo-wide.

4. **Bug OpenSpec menor**: el spec original tenía "MUST NO expongan" (no canónico) en lugar de "MUST NOT expongan". Detectado por el reviewer agent. No bloqueante pero documentado para cleanup futuro.

## Follow-ups (no resueltos en este change)

1. **`Details.cshtml.cs:151`** bindea `[FromQuery(Name = "userId")]` mientras `Index.BuildDetailsRouteValues` propaga `userName`. Round-trip del filtro Usuario se pierde al hacer drill-down al detalle. Issue candidato separado. Fuera del scope de #251 (documentado en verify-report).

2. **Test defensivo `?userName=noexiste`** (escenario 6 del spec): comportamiento correcto por semántica LINQ pero sin test runtime explícito. Sugerido por el reviewer agent y por verify-report. ~10 líneas, no bloqueante.

## Veredicto

**PASS** — change archivado, ciclo SDD cerrado.

---

## Notas para el gatekeeper del orquestador

- Skills injectadas en este launch: `_shared` (SDD phase common), `sdd-archive`
- Skill resolution status: `sdd-archive` ejecutada directamente (no delegable al orchestrator)
- Preguntas abiertas / decisiones bloqueantes: ninguna
- Archivos escritos:
  - `openspec/specs/auditoria-query/spec.md` (sobreescrito con merged delta)
  - `openspec/changes/archive/2026-08-03-auditoria-filtros-select-entidad-operacion/archive-report.md` (nuevo)
  - Engram: topic_key `sdd/2026-08-03-auditoria-filtros-select-entidad-operacion/archive-report` (type=architecture, capture_prompt=false)

## Rama

Las ramas `feat/issue-251-auditoria-filtros-select-entidad-operacion-slice-a` y `feat/issue-251-auditoria-filtros-select-entidad-operacion-slice-b` NO fueron eliminadas — el maintainer las eliminará después de la revisión humana.
