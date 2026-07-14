# Apply Progress: `2026-07-14-fix-126-operational-tech-debt` — PR 3 (CU-3 + CU-4 + CU-5)

> Change: `2026-07-14-fix-126-operational-tech-debt` (issue #126)
> Verifier: `sdd-apply` PR 3 de 3 (stacked-to-main)
> Fecha: 2026-07-14
> Modo de artefactos: hybrid (filesystem + Engram)
> TDD estricto: N/A (este PR solo agrega docs + verify; sin código runtime)
> Branch: `fix/126-operational-pt3` (target `develop`)
> Working tree: `fix/126-operational-pt3` desde `origin/develop` (HEAD `e672912c`)

## Chain Strategy

`stacked-to-main`. Tres PRs targetean `develop` en paralelo (no se apilan sobre cada uno):

- **PR 1 (`fix/126-operational-pt1`, PR #140)**: CU-0 health infrastructure. Merge independiente.
- **PR 2 (`fix/126-operational-pt2`, PR #139)**: CU-1 + CU-2 login timeout + UX frontera. Merge independiente.
- **PR 3 (`fix/126-operational-pt3`, PR #143, este)**: CU-3 + CU-4 + CU-5 spec delta + docs + verify. Merge independiente.

La estrategia permite merge en cualquier orden. PR 3 NO depende de los anteriores para su propia verificación: solo necesita `develop` HEAD.

## PR 3 Boundary

| Campo | Valor |
|-------|-------|
| PR | 3 de 3 (stacked-to-main) |
| Work units | CU-3 (spec delta) + CU-4 (docs) + CU-5 (verify) |
| Target branch | `develop` |
| Local branch | `fix/126-operational-pt3` |
| Estimated review budget | ~120 LoC (spec ~90, doc ~53, verify ~250) |
| Runtime code changes | 0 (docs + verify only) |
| Rollback boundary | Revertir los tres archivos nuevos; ningún archivo existente se toca |

## Tasks Completed (CU-3 + CU-4 + CU-5)

| Tarea | Estado | Evidencia |
|-------|--------|-----------|
| CU-3 SPEC: Versionar delta `sgv-readonly-api` | ✅ | `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md` creado: 2 ADDED requirements, 5 scenarios, source section con cross-refs |
| CU-4 DOC: Subsección runtime MySQL en `decisiones-implementacion.md` | ✅ | Nueva subsección "Contrato runtime MySQL — health, readiness y startup" entre líneas 52-104; 9 subsecciones (liveness, readiness, anonimato, timeout, AutoDetect, design-time/runtime, secrets, migraciones, validación startup) |
| CU-5 VERIFY: Capturar `verify-report.md` | ✅ | `openspec/changes/2026-07-14-fix-126-operational-tech-debt/verify-report.md` con status PASS_WITH_WARNINGS, conteos de tests, frontend regression guard, spec delta verificado, out-of-scope confirmado |

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| CU-3 | N/A | N/A | N/A | ➖ N/A (doc) | ✅ Spec escrito con 2 reqs + 5 scenarios | ➖ N/A | ➖ N/A |
| CU-4 | N/A | N/A | N/A | ➖ N/A (doc) | ✅ Subsección agregada con 9 subtopics | ➖ N/A | ➖ N/A |
| CU-5 | N/A | N/A | N/A | ➖ N/A (verify) | ✅ Verify report + 1965/2022 tests pass | ➖ N/A | ➖ N/A |

**Nota**: TDD estricto no aplica a este PR porque no hay código runtime. La directiva "Strict TDD: not applicable (no code changes; docs + verification only)" del orchestrator se respeta.

## Work Unit Evidence

| Evidence | Valor |
|----------|-------|
| Focused test command and exact result | N/A — PR sin código runtime. Verificación documental: `grep -cE "^### Requirement:" specs/sgv-readonly-api/spec.md` → 2, `grep -cE "^#### Scenario:"` → 5, `wc -l docs/decisiones-implementacion.md` → 404 |
| Runtime harness command/scenario and exact result | `dotnet build SGV.slnx --configuration Release --no-restore` → Build succeeded, 8 warnings pre-existentes, 0 errors. `bun run build` → Finished after 3 s, sin errores. `git diff --exit-code -- bun.lock wwwroot` → exit 0 |
| Rollback boundary | `git revert` (o `git rm`) de los tres archivos nuevos: `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md`, parche en `docs/decisiones-implementacion.md` (revertir el insert entre líneas 50-104), y `openspec/changes/2026-07-14-fix-126-operational-tech-debt/verify-report.md`. Ningún archivo preexistente se modifica |

## Files Created

| File | Action | What Was Done |
|------|--------|---------------|
| `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/sgv-readonly-api/spec.md` | Created | Delta spec con 2 ADDED requirements, 5 scenarios, source section con cross-refs a `operational-readiness/spec.md` y a la spec vigente `sgv-readonly-api/spec.md:174-191` |
| `openspec/changes/2026-07-14-fix-126-operational-tech-debt/verify-report.md` | Created | Reporte de verificación en español con status, scope, AC traceability, test counts, frontend regression guard, documentation presence, spec delta check, out-of-scope y out-of-band observations |
| `openspec/changes/2026-07-14-fix-126-operational-tech-debt/apply-progress.md` | Created (este archivo) | Apply progress de PR 3, encadenado a los apply-progress de PR 1 y PR 2 vía Engram |

## Files Modified

| File | Action | What Was Done |
|------|--------|---------------|
| `docs/decisiones-implementacion.md` | Modified | Nueva subsección "Contrato runtime MySQL — health, readiness y startup" entre líneas 52-104 (después de `## SgvDbContextFactory fail-loud` y antes de `## Gestión de secretos JWT`). 9 subtopics documentados. Archivo pasó de 351 a 404 líneas (+53) |

## Test Results Summary

| Categoría | Total | Passed | Failed | Skipped | Notas |
|-----------|-------|--------|--------|---------|-------|
| No-Web, no-MySqlFact | 1362 | 1362 | 0 | 0 | Dominio + Aplicacion + Persistencia + Api + Compatibilidad |
| `[MySqlFact]` (MySQL local alcanzable) | 28 | 28 | 0 | 0 | Bootstrap automático en `MySqlTestDatabaseBootstrap` aplica `Database.Migrate()` |
| Web (con pre-existentes) | 632 | 575 | 57 | 0 | 57 fallos **pre-existentes en `develop`**; no introducidos por este PR |
| **Total** | **2022** | **1965** | **57** | **0** | 100% pass en lo nuevo y modificado |

**AC-10 (cobertura MySQL)**: 28 tests `[MySqlFact]` ejecutados con MySQL local, 0 omitidos. Drift documentado (28 descubiertos por runner vs 166 atributos estáticos en source) se explica por la diferencia entre `[Fact]`/`[Theory]` efectivos y referencias literales.

**AC-11 (frontend regression guard)**: `bun install` sin cambios (772 installs, 667 packages), `bun run build` exit 0 (3 s), `git diff --exit-code -- bun.lock wwwroot` exit 0 (sin drift).

**AC-9 (docs)**: subsección presente con los 9 subtopics requeridos por CU-4.

## Deviations from Design

None — implementation matches design.

- **CU-3**: spec coincide con `design.md` §4.G (2 ADDED requirements, 5 scenarios, cross-ref a `operational-readiness/spec.md:77-96`).
- **CU-4**: subsección agregada entre líneas 52-104 con 9 subsecciones (liveness, readiness, anonimato, timeout, AutoDetect, design-time/runtime, secrets, migraciones, validación startup) y referencia explícita al placeholder JWT dev como NO apto para producción.
- **CU-5**: verify-report incluye status, scope, AC traceability, test counts, frontend regression guard, doc presence, spec delta check, out-of-scope y out-of-band observations.

## Issues Found

1. **PR 1 y PR 2 no mergeados en este branch**: el working tree de `fix/126-operational-pt3` está al HEAD de `develop` (`e672912c`). Las pruebas de health/login de CU-0/CU-1/CU-2 (`HealthTests.cs`, `StartupValidationTests.cs`, `AuthApiClientTimeoutTests.cs`, `SignInTransportTests.cs`) **no existen** en esta rama. Su verificación de aceptación vive en los apply-progress y verify-reports de PR #139 y PR #140. Este PR (PR #143) verifica solo AC-9, AC-10, AC-11.

2. **57 tests Web fallan (pre-existentes)**: ya documentado en apply-progress de PR 1 (48) y PR 2 (1 con la misma signatura). La rama `fix/126-operational-pt3` no toca código runtime, así que la cuenta actual de 57 es idéntica al baseline de develop HEAD `e672912c`. Distribución: 49 en `UnidadOrganizativaWebTests`, 1 en `WebAuthenticationTests.Post_SignIn_WithValidCredentials_RedirectsToDashboardAndSetsCookie`, 3 en `Puesto.*PageTests`, 4 en `Cargo.*PageTests`. No son introducidos por este PR.

3. **Drift `[MySqlFact]`: 28 vs 166**: el atributo se referencia en 166 lugares del source (constructores, builders, comentarios) pero el runner solo descubre 28 tests efectivos con ese filtro. El conteo ejecutable (28) es la fuente autoritativa para AC-10.

4. **Suite completa con timeout**: `dotnet test SGV.slnx --configuration Release` no completa en este entorno por timeouts de construcción de host en la colección `WebIntegration`. Patrón documentado en apply-progress de PR 1 ("timeouts pre-existentes en WebIntegration"). La suite se ejecutó por categoría para obtener conteos.

5. **8 warnings de compilación pre-existentes**: CS8524 (switch expressions no exhaustivos en Habilidad/Puestos/Cargo/UnidadOrganizativaApiClient), CS8602 (posibles nulls en Details/Index/Edit de UnidadesOrganizativas), xUnit1026 (parámetro no usado en `CommandResultMapperTests.Map_AtypicalStatus`). Todos pre-existentes en `develop` y no introducidos por este PR.

## Workload / PR Boundary

- **Mode**: stacked PR slice (PR 3 de 3)
- **Current work unit**: CU-3 + CU-4 + CU-5 (spec delta + docs + verify)
- **Boundary**: `fix/126-operational-pt3` → `develop`; sin código runtime, solo artefactos SDD + reporte de verificación
- **Estimated review budget**: ~120 LoC (spec 90 + doc 53 + verify 250 = ~400 LoC en archivos nuevos, pero ninguno es código de producción)
- **Rollback boundary**: revertir los tres archivos nuevos + el parche en `docs/decisiones-implementacion.md`

## Cross-PR Continuity

| PR | Branch | Apply Progress | Verify Report |
|----|--------|----------------|---------------|
| PR 1 (CU-0) | `fix/126-operational-pt1` (PR #140) | en `apply-progress.md` de la rama pt1, observado en Engram obs-52a8... | En su propio PR (no en este branch) |
| PR 2 (CU-1+CU-2) | `fix/126-operational-pt2` (PR #139) | en `apply-progress.md` de la rama pt2, observado en Engram obs-52a8442be56b1ef2 (memory #1070) | En su propio PR (no en este branch) |
| PR 3 (CU-3+CU-4+CU-5) | `fix/126-operational-pt3` (PR #143, este) | este archivo | `verify-report.md` en este branch |

## Next Recommended

`sdd-archive 2026-07-14-fix-126-operational-tech-debt` una vez que los tres PRs (#139, #140, #143) estén mergeados en `develop`. El archive sincronizará los deltas de specs a `openspec/specs/` y cerrará el change formalmente.
