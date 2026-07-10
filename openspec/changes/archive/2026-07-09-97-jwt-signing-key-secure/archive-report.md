# Archive Report: Validación del signing key de JWT al arranque

## Archive Metadata

| Field | Value |
|-------|-------|
| Change | `97-jwt-signing-key-secure` |
| Issue | #97 — [Security] Eliminar JWT signing key default hardcodeado y validar al arranque |
| Archived on | 2026-07-09 |
| Archived to | `openspec/changes/archive/2026-07-09-97-jwt-signing-key-secure/` |
| Artifact store | hybrid (OpenSpec + Engram) |
| Verdict from verify | **PASS** (0 CRITICAL, 2 WARNING, 3 SUGGESTION no bloqueantes) |
| Mode | strict-TDD, single PR (delivery strategy `size-exception`, ~300 líneas) |
| Override | Ninguno — el verify reporte ya autorizó archive sin bloqueos |

## SDD Cycle Overview

Issue de seguridad #97 cerrado de extremo a extremo en 7 fases SDD:

1. **sdd-explore** → `exploration.md` mapea blast radius del default hardcodeado
2. **sdd-propose** → `proposal.md` define scope + criterios de éxito + no-goals (incluyendo out-of-scope explícito de #59)
3. **sdd-spec** → `specs/jwt-signing-key-validation/spec.md` con 5 requirements y 11 scenarios Given/When/Then
4. **sdd-design** → `design.md` con decisiones arquitectónicas (IPostConfigureOptions, no-IAsyncLifetime, siembra idempotente con PersonaEntity previa)
5. **sdd-tasks** → `tasks.md` con 10 tasks T-01..T-10, todas completadas
6. **sdd-apply** → 8 commits conventional en `develop` (ver observación #779)
7. **sdd-verify** → `verify-report.md` con verdict PASS, 7/7 tests nuevos verdes
8. **sdd-archive** → este reporte + sync de spec

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `jwt-signing-key-validation` | **Created** | Capability nueva. Copiada desde `openspec/changes/97-jwt-signing-key-secure/specs/jwt-signing-key-validation/spec.md` y normalizada al formato del repositorio (`# Especificación de ...`, `## Purpose`, `## Requirements`, body en español con palabras clave RFC 2119). 5 requirements agregados, 11 scenarios preservados. |

> Nota de normalización: el delta del change usaba `# Delta para ...` y `## ADDED Requirements`. Como el spec destino se crea desde cero (capability no existente), se removieron los marcadores de delta para que el archivo represente la fuente de verdad canónica del dominio. El `## ADDED Requirements` queda registrado en `tasks.md` con trazabilidad implícita por change-id.

## Archive Contents

| Artifact | State |
|----------|-------|
| `proposal.md` | ✅ Preservado |
| `exploration.md` | ✅ Preservado |
| `design.md` | ✅ Preservado |
| `specs/jwt-signing-key-validation/spec.md` | ✅ Preservado como delta + copiado a `openspec/specs/jwt-signing-key-validation/spec.md` |
| `tasks.md` | ✅ Preservado con T-01..T-10 (formato sin checkboxes `- [x]`, intencional — ver nota de task gate) |
| `verify-report.md` | ✅ Preservado (PASS) |
| `apply-progress.md` | ➖ No existía en la carpeta del change; la evidencia aplica vive en Engram (obs #779) y en los 8 commits del git log |
| `archive-report.md` | ✅ Este archivo |

## Task Completion Gate

El archivo `tasks.md` archivado **no contiene checkboxes `- [x]`/`- [ ]`**; usa encabezados `### T-01..T-10` estilo sección. Esto es consistente con el formato que produjo `sdd-tasks` para este change. La completitud se prueba desde múltiples fuentes:

- **verify-report.md** declara explícitamente: `Tasks totales: 10 (T-01..T-10)` / `Tasks completas: 10` / `Tasks incompletas: 0`
- **apply-progress (Engram #779)** describe RED→GREEN por task y lista los 8 commits `bd6f4577..64961dc2` mapeando T-01+T-02 (atómico), T-03, T-04, T-05, T-06, T-07, T-08, T-09. T-10 es verificación sin commit.
- **git log** confirma los 8 commits firmados en `develop` con mensajes que mapean 1:1 a las tasks del plan.
- **dotnet test** result: 7/7 tests del change PASS (5 `JwtOptionsTests` + 2 `JwtRealAuthTests`).

Por lo tanto, **no fue necesaria reconciliación mecánica**: el audit trail es íntegro y el formato sin checkboxes no representa incompletitud sino una variante sintáctica del template de tasks.

## Source of Truth Updated

- `openspec/specs/jwt-signing-key-validation/spec.md` ← nueva capability, source of truth vigente

## Verification Status

| Check | Status |
|-------|--------|
| Main spec creado correctamente | ✅ 5 requirements, 11 scenarios, formato repo-respaldado |
| Change folder movido al archive con prefijo `YYYY-MM-DD-` | ✅ `2026-07-09-97-jwt-signing-key-secure/` |
| Archive contiene todos los artefactos del change | ✅ proposal, exploration, design, specs/, tasks, verify-report |
| Active changes directory ya no contiene este change | ✅ `openspec/changes/97-jwt-signing-key-secure` no existe |
| Verify verdict PASS sin CRITICAL | ✅ |
| Engram archive-report persistido | ✅ (observación de topic_key `sdd/97-jwt-signing-key-secure/archive-report`) |

## Engram Observation Reference

Este change se respaldó completamente en Engram. IDs relevantes:

| Artifact / Event | Observation ID |
|------------------|----------------|
| Preflight SDD #97 | #755 |
| sdd-explore | #756 |
| Scope decisions (pre-propose) | #757 |
| sdd-spec (final) | #761 |
| Model preference sdd-spec | #760 |
| Model preference sdd-design | #762 |
| JD Round 1 verdict | #767 |
| JD Round 1 fixes | #768 |
| JD Round 2 verdict | #769 |
| JD Round 2 fixes | #770 |
| JD Round 3 verdict (implícito) | (no persistido como obs dedicada) |
| JD Round 4 approve | #772 |
| JD APPROVED design | #774 |
| Model preference sdd-tasks | #775 |
| sdd-tasks | #776 |
| Apply preflight (single PR) | #777 |
| Model preference sdd-apply | #778 |
| sdd-apply (#779) | #779 |
| Model preference sdd-verify | #780 |
| sdd-verify | #781 |
| Model preference sdd-archive | #782 |
| sdd-archive (este reporte) | (próximo ID disponible) |

## Desviaciones y Notas

1. **Formato de `tasks.md` sin checkboxes**: ya documentado arriba. No es una desviación del proceso sino una variante sintáctica que `sdd-tasks` eligió para este change.
2. **`apply-progress.md` ausente en filesystem**: la evidencia de apply vive en Engram (#779) y en git log (8 commits). Esto es válido porque el mode es `hybrid` y la fase `sdd-apply` cumplió su contrato persistiendo a Engram.
3. **SUGGESTION/WARNING del verify (no bloqueantes)**:
   - W-1: `HostBuild_SigningKey31Bytes_Lanza` no valida mensaje; cubre solo el lanzamiento. Asimetría menor vs los otros fail-loud tests que sí validan mensaje. Informativo.
   - S-1: bound 32 bytes UTF-8 (debe pasar) solo cubierto por indirecta (placeholder 51 bytes). No bloquea porque el validador usa `>=` y `HostBuild_SigningKey31Bytes_Lanza` ya cubre el lado izquierdo.
   - S-2: spec documental sin test automatizado de contenido de docs.
   - S-3: `Issuer`/`Audience` permanecen como defaults literales en `JwtOptions.cs:7,9`. Out of scope explícito en proposal.
4. **Pre-existing issue #59**: los 12 fallos de `OcupacionRepositoryTests` son del bug `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en la migración inicial. Ocurren antes y después del change, están documentados en `AGENTS.md:181-186` y son out-of-scope explícito según `proposal.md:107` y `design.md:230`. **No bloquea este change**.

## SDD Cycle Complete

El change #97 fue planificado, implementado, verificado y archivado. La capability `jwt-signing-key-validation` ya forma parte del source of truth del repo y queda disponible para PR de los próximos cambios que quieran extender el contrato JWT.

Próximos pasos posibles (no parte de este archive):
- Cerrar issue #59 en un change aparte (`ActivePuestoIdUnique`).
- Considerar las 3 SUGGESTION en futuros cambios relacionados (especialmente S-1 si surgen nuevos umbrales de tamaño).
