# Verification Report — Slice 2

**Change**: `2026-07-13-taxonomia-errores-commandresult`  
**Issue**: #125  
**Slice verificado**: Slice 2, tasks T-2.1..T-2.14  
**Modo de verificación**: Strict TDD Verify  
**Persistencia**: híbrida (OpenSpec + Engram)  
**Branch**: `fix/125-s2-mapper-clients`  
**PR**: #133, base `develop` — **MERGEADO** (SHA `6ab94c83`)  
**Commit verificado ronda 1**: `1e15178` (FAIL)  
**Commit verificado ronda 2 (correctiva)**: `7df3248f` (PASS WITH WARNINGS)

> Este reporte sustituye al verify-report de Slice 2 ronda 1 (FAIL) en
> `openspec/changes/2026-07-13-taxonomia-errores-commandresult/verify-report.md`.
> La FAIL de ronda 1 permanece como revisión histórica en Engram.

## Veredicto final: **PASS WITH WARNINGS**

La implementación compila, los 357 tests acumulados (253 Slice 2 + 104 contracts) están verdes y el contrato de propagación nativa no fue roto.

### CRITICAL — 0 (tres corregidos en ronda 2)

| CRITICAL ronda 1 | Estado | Test |
|---|---|---|
| REQ-4: HabilidadApiClient.UpdateAsync 403→Forbidden | ✅ **CERRADO** | `UpdateAsync_Http403WithNonJsonBody_FallsBackToForbiddenDefaults` (16ms) |
| REQ-9: CargoSkillApiClient 408→Transport | ✅ **CERRADO** | `UpsertSkillAsync_Http408_ReturnsFailureWithTransportCategoria` (6ms) + `DeleteSkillAsync_Http408_ReturnsFailureWithTransportCategoria` (13ms) |
| §11.5: Timeout vs Cancel externa | ✅ **CERRADO** | 3 tests: HttpClient.Timeout=1ms (timer interno) vs CancellationTokenSource(5ms) (token externo) vs HabilidadApiClient.UpdateAsync propagación |

### WARNING — 3 (heredados, no bloquean merge)

1. **FieldErrors default**: `CommandResultMapper.ResolveCategoria(400/422)` no inspecciona `FieldErrors`. Desviación aceptada de design §5.4.
2. **UnidadOrganizativaApiClient coverage**: 58.9% lines / 42.1% branches. Bajo umbral informativo 80%.
3. **Apply-progress conteos**: la discrepancia cuantitativa entre el reporte y el runtime fue corregida en la actualización del apply-progress (ronda 2).

### SUGGESTION — 2 (mejora no obligatoria)

1. xUnit1026: parámetro `expectedTitleFragment` no usado en `Map_AtypicalStatus_MapToUnexpectedPreservingStatus`.
2. §11.5: la distinción causal vive en el setup del test, no en asserciones explícitas de `CancellationToken`/`InnerException`.

### Strict TDD

| Dimensión | Ronda 1 | Ronda 2 |
|---|---|---|
| Evidence table | PRESENT | PRESENT |
| RED before GREEN | 6/6 | 6/6 (commit correctivo 100% test-only: +236 LoC tests, 0 production) |
| Assertion quality | PASS | PASS |
| Triangulation | PARTIAL | PASS |
| **Verdict** | **PARTIAL** | **PASS** |

### Evidencia runtime

| Suite | Resultado |
|---|---|
| Build | 0 errors, 521 warnings (510 CS0618, 10 CS8524, 1 xUnit1026) |
| Slice 2 core | 253/253 passed (+7 vs ronda 1) |
| Slice 1+2 cumulative | 357/357 passed (+7 vs ronda 1) |
| Regresión Dominio + Api.Infrastructure | 180/180 passed (sin cambios) |
| Contracts | 104/104 passed (sin cambios) |

## Resolución de issues

Los 3 CRITICAL del verify FAIL de ronda 1 fueron cerertos con un commit correctivo 100% test-only (`7df3248f`, +236 LoC tests) que implementa:

1. **REQ-4**: `UpdateAsync_Http403WithNonJsonBody_FallsBackToForbiddenDefaults` — mockea HTTP 403, assert `ErrorCategoria.Forbidden`, `Code="Forbidden"`, `Message="Acceso denegado."`, `StatusCode=403`. Traza verificada: `UpdateAsync` → `ToCommandResultAsync` → `CommandResultMapper.Map` → `ResolveCategoria(403)`.

2. **REQ-9**: `UpsertSkillAsync_Http408_ReturnsFailureWithTransportCategoria` + `DeleteSkillAsync_Http408_ReturnsFailureWithTransportCategoria` — cubren HTTP 408 → `ErrorCategoria.Transport` para las dos operaciones principales de `CargoSkillApiClient`.

3. **§11.5**: `HttpClientExceptionScenariosTests` con 3 tests + helper `TaskDelayedHandler`:
   - HttpClient.Timeout=1ms → TaskCanceledException (timer interno de HttpClient)
   - CancellationTokenSource(5ms) → OperationCanceledException (token externo del caller)
   - HabilidadApiClient.UpdateAsync propaga ambas nativamente (no las convierte en Transport)

### Persistencia

- Engram: topic_key `sdd/2026-07-13-taxonomia-errores-commandresult/verify-report` actualizado con veredicto PASS WITH WARNINGS.
- OpenSpec: este archivo.
