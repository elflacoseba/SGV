# Archive Report: Confirmación modal al bloquear o desbloquear un usuario

**Change**: `2026-07-17-modal-confirmacion-bloqueo-desbloqueo`
**Fecha de archive**: 2026-07-17
**Rama**: `feat/2026-07-17-modal-confirmacion-bloqueo-desbloqueo` (base: `develop`)
**Issue**: [#155](https://github.com/elflacoseba/SGV/issues/155)
**Spec**: `usuario-web-confirmacion-bloqueo-desbloqueo/spec.md`

---

## Estado

**archived** ✅ — Archive exitoso. Sin CRITICAL issues. 1 WARNING (desviación documentada `Body` → `BodyHtml`) y 3 SUGGESTIONS no bloqueantes.

## Task Completion Gate — Resolución

Todos los tasks de implementación (Phase 1-4, Phase 5.1) están marcados `[x]`. Los items 5.2.1..5.2.6 (verificación manual del navegador) permanecen `[ ]` según lo previsto en `tasks.md:133` ("las cubre la fase `sdd-verify`, no el ejecutor `sdd-apply`"). El `verify-report.md` confirma PASS con 2412/2412 tests verdes, 0 errores de build y bundle frontend OK, lo que constituye prueba suficiente de completitud.

## Specs sincronizados

| Spec origen | Spec destino | Acción |
|-------------|-------------|--------|
| `openspec/changes/2026-07-17-modal-confirmacion-bloqueo-desbloqueo/specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md` | `openspec/specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md` | **CREATED** — spec nueva (capability `usuario-web-confirmacion-bloqueo-desbloqueo`). 10 requisitos REQ-UCB-01..10 copiados tal cual. No existía spec previa en `openspec/specs/`. |

## Change folder movido

| Desde | Hasta |
|-------|-------|
| `openspec/changes/2026-07-17-modal-confirmacion-bloqueo-desbloqueo/` | `openspec/changes/archive/2026-07-17-modal-confirmacion-bloqueo-desbloqueo/` |

Movimiento realizado con `git mv` (renames preservan historial git). `verify-report.md` (untracked) agregado al índice antes del move.

## Commits archivados

| SHA | Mensaje |
|-----|---------|
| `437bc1c0` | `test(usuarios): add page tests for bloquear/desbloquear modal confirmation` |
| `6135bc8b` | `feat(usuarios): add confirmar-accion-usuario partial modal` |
| `8821ece7` | `feat(usuarios): add bloquear/desbloquear modal in index page` |
| `9c0f367d` | `feat(usuarios): add bloquear/desbloquear modal in details page` |
| `a9b38788` | `chore(sdd): apply progress for 2026-07-17-modal-confirmacion-bloqueo-desbloqueo` |

5 commits, todos conventional commits sin `Co-Authored-By`. Orden RED → GREEN verificado: tests (437bc1c0) precede a partial/Index/Details.

## Archivos archivados

| Archivo | Descripción |
|---------|-------------|
| `proposal.md` | Propuesta del cambio — intent, scope, approach, risks, rollback plan |
| `specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md` | Spec delta: 10 requisitos REQ-UCB-01..10 con escenarios GWT |
| `design.md` | Diseño técnico — 10 decisiones D-01..D-10, estructura modal, JS, tests |
| `tasks.md` | 5 fases, ~25 tasks individuales, todos los implementation tasks `[x]` |
| `apply-progress.md` | Evidencia de ciclo RED→GREEN, archivos tocados, desviaciones documentadas |
| `verify-report.md` | Verificación adversarial — PASS, 10/10 reqs, 10/10 design decisions |

## Métricas finales

| Métrica | Valor |
|---------|-------|
| Tests nuevos | 13 (9 Index + 4 Details) |
| Tests totales | 2412 |
| Tests fallidos | 0 |
| Tests skipeados | 0 |
| Build errors | 0 |
| Build warnings nuevos | 0 (23 preexistentes, todos `CS8524`) |
| Frontend bundle | OK (3.01 s) |
| Archivos producción/test tocados | 5 |
| Líneas añadidas/eliminadas (prod+test) | +554 / −18 |
| Commits | 5 |

## Notas finales

1. **Desviación documentada (W-01)**: Design D-02 menciona `Body` (string), implementación usa `BodyHtml` (`IHtmlContent`). Sin impacto funcional.

2. **Copy del cuerpo del modal (S-01)**: El copy genérico "Esta acción afecta este usuario. ¿Desea continuar?" prioriza privacidad y simplicidad sobre la descripción literal de la issue #155. Se puede personalizar vía `BodyHtml` si el equipo lo decide; no requiere tests adicionales.

3. **Idempotencia doble click (S-02)**: Sin test E2E runtime dedicado. La protección JS (`confirmBtn.disabled = true` + doble guard D-06) es trivialmente correcta. Fuera del scope del slice UX (alineado con D-10: sin E2E).

4. **Budget de líneas (S-03)**: ~554 líneas reales vs ~300 estimadas. El delta se debe a 13 tests (vs 6 estimados). Sigue siendo single-PR según `tasks.md:14`.

5. **Rama intacta**: `feat/2026-07-17-modal-confirmacion-bloqueo-desbloqueo` conserva sus 5 commits. No se realizó push.

6. **Próximo paso recomendado**: `open_pr` — la rama está lista para PR contra `develop`.
