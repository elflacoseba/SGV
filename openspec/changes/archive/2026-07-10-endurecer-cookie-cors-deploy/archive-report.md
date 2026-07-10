# Archive Report: `2026-07-10-endurecer-cookie-cors-deploy`

## Archive Metadata

| Field | Value |
|-------|-------|
| Change | `2026-07-10-endurecer-cookie-cors-deploy` |
| Issue | #101 — [Security] Endurecer cookie Web y CORS API para deploy real |
| PR | #106 mergeado a `develop` en `dca76669` |
| Archived on | 2026-07-10 |
| Archived to | `openspec/changes/archive/2026-07-10-endurecer-cookie-cors-deploy/` |
| Artifact store | hybrid (OpenSpec filesystem + Engram) |
| Verdict from verify | **PASS** (0 CRITICAL, 0 WARNING, 3 SUGGESTION no bloqueantes) |
| Mode | strict-TDD, single PR (delivery strategy `size-exception`, ~350 líneas) |
| Override | Ninguno — el verify reporte ya autorizó archive sin bloqueos |

## SDD Cycle Overview

Issue de seguridad #101 cerrado de extremo a extremo en 7 fases SDD:

1. **sdd-explore** → no se generó `exploration.md` (issue con scope acotado, sin ambigüedad de discovery; el blast radius está descrito en `proposal.md`)
2. **sdd-propose** → `proposal.md` define scope + criterios de éxito + no-goals (incluyendo out-of-scope explícito de #59, `ApiBearerTokenHandler`, rate limiting, JWT format)
3. **sdd-spec** → 2 specs: delta sobre `sgv-web-authentication` (1 requisito, 3 escenarios) + spec nueva `api-cors-allowed-origins-validation` (2 requisitos, 5 escenarios)
4. **sdd-design** → `design.md` con decisiones arquitectónicas (throw directo vs `IValidateOptions<CorsOptions>`; rama `Development` sin `AllowCredentials()`; ternario inline vs opciones tipadas; inspección vía `IOptionsMonitor<CookieAuthenticationOptions>`)
5. **sdd-tasks** → `tasks.md` con 7 tasks T-01..T-07, todas completadas con `✅`
6. **sdd-apply** → 5 work-unit commits cohesivos squash-mergeados en `dca76669` (ver observación Engram #822 `sdd/2026-07-10-endurecer-cookie-cors-deploy/apply-progress`)
7. **sdd-verify** → `verify-report.md` con verdict PASS, 6/6 tests nuevos verdes, 8/8 escenarios de spec cubiertos
8. **sdd-archive** → este reporte + sync de specs

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `sgv-web-authentication` | **Updated (delta aditiva)** | Sección `Requisitos AÑADIDOS` anexada al final del spec canónico. 1 requisito nuevo agregado (`Atributos de la cookie de autenticación por ambiente`) con 3 escenarios. Spec canónico previo: 4 requisitos / 7 escenarios. Total vigente: **5 requisitos / 10 escenarios**. |
| `api-cors-allowed-origins-validation` | **Created** | Spec nueva, capability transversal CORS API. Copiada tal cual desde `openspec/changes/2026-07-10-endurecer-cookie-cors-deploy/specs/api-cors-allowed-origins-validation/spec.md`. 2 requisitos con 5 escenarios. |

> **Decisión de normalización**: la delta sobre `sgv-web-authentication` se anexa con su título original (`Requisitos AÑADIDOS`) y conserva el estilo en español del delta (`DADO/CUANDO/ENTONCES/Y`), distinto del estilo del spec canónico previo (`GIVEN/WHEN/THEN/AND`). Esto preserva la trazabilidad histórica del change y deja explícito que el bloque agregado proviene de una delta verificada, sin alterar el contrato de los 4 requisitos originales. El spec queda con un híbrido estilístico documentado por la propia cabecera `Requisitos AÑADIDOS`.

## Archive Contents

| Artifact | State |
|----------|-------|
| `proposal.md` | ✅ Preservado (108 líneas) |
| `design.md` | ✅ Preservado (54 líneas) |
| `specs/sgv-web-authentication/spec.md` | ✅ Delta preservado y copiada al catálogo principal |
| `specs/api-cors-allowed-origins-validation/spec.md` | ✅ Spec nueva preservada y copiada al catálogo principal |
| `tasks.md` | ✅ Preservado (T-01..T-07, formato con `✅` por task, sin checkboxes `- [x]`/`- [ ]`, intencional) |
| `verify-report.md` | ✅ Preservado (249 líneas, verdict PASS) |
| `apply-progress.md` | ➖ No existía en la carpeta del change; la evidencia aplica vive en Engram (obs #822) y en los 5 commits cohesivos del git log (`dca76669` squash-merge) |
| `archive-report.md` | ✅ Este archivo |

## Task Completion Gate

El archivo `tasks.md` archivado **no contiene checkboxes `- [x]`/`- [ ]`**; usa encabezados `### T-01..T-07` con marcador `✅` por task. Esto es consistente con el formato que produjo `sdd-tasks` para este change. La completitud se prueba desde múltiples fuentes:

- **verify-report.md** declara explícitamente: `Tasks totales: 7` / `Tasks completas: 7` / `Tasks incompletas: 0`
- **apply-progress (Engram #822)** describe RED→GREEN por task y mapea los 5 commits cohesivos a T-01..T-06; T-07 es el PR/squash-merge
- **git log** confirma los commits firmados en `develop` con mensajes que mapean 1:1 a las tasks del plan
- **dotnet test** result: 6/6 tests del change PASS (4 `CorsAllowedOriginsValidationTests` + 2 `WebCookieAuthenticationOptionsTests`)

Por lo tanto, **no fue necesaria reconciliación mecánica**: el audit trail es íntegro y el formato sin checkboxes no representa incompletitud sino una variante sintáctica del template de tasks.

## Source of Truth Updated

- `openspec/specs/sgv-web-authentication/spec.md` ← source of truth vigente (5 requisitos, 10 escenarios)
- `openspec/specs/api-cors-allowed-origins-validation/spec.md` ← source of truth vigente (2 requisitos, 5 escenarios)

## Cambios aplicados en develop

| Commit | Descripción |
|--------|-------------|
| `dca76669` | `fix(security): harden cookie SecurePolicy and CORS AllowedOrigins (#106)` (squash-merge de los 5 work-unit commits del feature branch) |
| `6caf9235` | `docs(sdd): add planning artifacts for issue #101 hardening` (preserva proposal/design/tasks/verify-report/specs) |

## Verification Status

| Check | Status |
|-------|--------|
| Main spec `sgv-web-authentication` actualizado correctamente (delta aditiva) | ✅ 4 requisitos previos preservados + 1 nuevo, 7 escenarios previos + 3 nuevos |
| Main spec `api-cors-allowed-origins-validation` creado | ✅ 2 requisitos, 5 escenarios, contenido byte-a-byte idéntico al delta |
| Change folder movido al archive con prefijo `YYYY-MM-DD-` | ✅ `2026-07-10-endurecer-cookie-cors-deploy/` |
| Archive contiene todos los artefactos del change | ✅ proposal, design, specs/, tasks, verify-report |
| Active changes directory ya no contiene este change | ✅ `openspec/changes/2026-07-10-endurecer-cookie-cors-deploy` no existe |
| Archived `tasks.md` sin tareas de implementación sin completar | ✅ 7/7 tasks completas (marcadas con `✅`) |
| Verify verdict PASS sin CRITICAL | ✅ |
| `git diff` confirma que la delta sobre `sgv-web-authentication` es puramente aditiva | ✅ Sin modificaciones a requisitos previos |
| Engram archive-report persistido | ✅ (topic_key `sdd/2026-07-10-endurecer-cookie-cors-deploy/archive-report`) |

## Engram Observation Reference

Este change se respaldó completamente en Engram. IDs relevantes:

| Artifact / Event | Observation ID |
|------------------|----------------|
| sdd-apply (`sdd/2026-07-10-endurecer-cookie-cors-deploy/apply-progress`) | #822 |
| sdd-archive (este reporte) | topic_key `sdd/2026-07-10-endurecer-cookie-cors-deploy/archive-report` (próximo ID disponible) |

> Nota: a diferencia del change #97, este change no generó observaciones dedicadas por fase SDD (preflight/spec/design/tasks). El apply-progress consolidado (obs #822) cubre los descubrimientos no triviales (lambda lazy resolution dentro de `AddDefaultPolicy`, `ConfigureAppConfiguration` discovery post-`Build()`, fallback sin credenciales, `WebApplicationFactory` stack trace offset).

## Tests aplicados

- `tests/SGV.Tests/Api/CorsAllowedOriginsValidationTests.cs` (nuevo, 129 líneas, 4 tests `[Fact]`)
  - `HostBuild_Production_SinAllowedOrigins_LanzaInvalidOperationException`
  - `HostBuild_Production_AllowedOriginsPoblado_Arranca`
  - `HostBuild_Development_AllowedOriginsVacio_Arranca`
  - `ProgramCs_Api_NoContieneAllowAnyOrigin` (guard estructural)
- `tests/SGV.Tests/Web/WebCookieAuthenticationOptionsTests.cs` (nuevo, 88 líneas, 2 tests `[Fact]`)
  - `WebCookieAuthOptions_Production_SecurePolicyAlways`
  - `WebCookieAuthOptions_Development_SecurePolicySameAsRequest`

**Cobertura verificada**: 6/6 tests nuevos verdes. Suite completa: 1608 pass / 12 fail pre-existentes (issue #59, bug `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en migración inicial), 0 nuevos fallos.

## Outstanding items

Tres SUGGESTION documentadas en `verify-report.md` (ninguna bloqueante):

1. **SUGGESTION-1**: `design.md` quedó desfasado respecto del mecanismo real de lectura de `AllowedOrigins` (lee dentro del callback `AddDefaultPolicy`, no antes de `AddCors`). El comportamiento runtime cumple el intent (fail-loud al host build), pero el texto del diseño no refleja el mecanismo real. Recomendación: commit de follow-up que actualice `design.md`.
2. **SUGGESTION-2**: guard estructural `ProgramCs_Api_NoContieneAllowAnyOrigin` solo cubre `src/SGV.Api/Program.cs`. Si en el futuro se introduce configuración CORS en otro archivo, el guard no la cubre. Recomendación: ampliar a `grep -R "AllowAnyOrigin" src/SGV.Api/` si se decide partir la config CORS en un helper.
3. **SUGGESTION-3**: escenario B5 de `api-cors-allowed-origins-validation` se cubre estructuralmente, no por inspección runtime de la `CorsPolicy`. Hoy no hay riesgo real (la combinación prohibida es estructuralmente imposible por B4), pero un refactor podría relajar la garantía sin romper B3 ni B4. Recomendación: agregar test que resuelva `ICorsPolicyProvider` desde el container en Development y verifique `CorsPolicy.SupportsCredentials == false`.

## Desviaciones y Notas

1. **Formato de `tasks.md` sin checkboxes**: ya documentado arriba. No es una desviación del proceso sino una variante sintáctica que `sdd-tasks` eligió para este change (igual que #97).
2. **`apply-progress.md` ausente en filesystem**: la evidencia de apply vive en Engram (#822) y en git log. Válido porque el mode es `hybrid` y la fase `sdd-apply` cumplió su contrato persistiendo a Engram.
3. **Híbrido estilístico en `sgv-web-authentication/spec.md`**: el spec canónico previo está en inglés (`GIVEN/WHEN/THEN` + `MUST`); la delta anexada está en español (`DADO/CUANDO/ENTONCES` + `DEBE`). Se preserva tal cual para mantener la trazabilidad del change y dejar explícita la frontera delta/spec-canónico. Es decisión consciente del orchestrator.
4. **SUGGESTION del verify (no bloqueantes)**: ver sección "Outstanding items" arriba.
5. **Pre-existing issue #59**: los 12 fallos de `OcupacionRepositoryTests` son del bug `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en la migración inicial. Ocurren antes y después del change, documentados en `AGENTS.md:181-186` y son out-of-scope explícito según `proposal.md`. **No bloquea este archive**.

## SDD Cycle Complete

El change #101 fue planificado, implementado, verificado y archivado. Las capabilities `sgv-web-authentication` (con delta de atributos de cookie por ambiente) y `api-cors-allowed-origins-validation` (spec nueva transversal de CORS API) ya forman parte del source of truth del repo y quedan disponibles para PR de los próximos cambios que quieran extender el contrato de seguridad perimetral.

Próximos pasos posibles (no parte de este archive):

- Cerrar las 3 SUGGESTION en un change aparte o como follow-up commit.
- Cerrar issue #59 en un change aparte (`ActivePuestoIdUnique`).
- Implementar `UseForwardedHeaders` con `KnownProxies`/`KnownNetworks` para deployments detrás de reverse proxy (actualmente solo documentado).