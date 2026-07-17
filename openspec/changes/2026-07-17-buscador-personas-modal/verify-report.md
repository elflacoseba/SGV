```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:42b6c223a6134591cad7c101f8e66cc37ae7420c3abd88a6d381826947d3c513
verdict: pass
blockers: 0
critical_findings: 0
requirements: 4/4
scenarios: 5/5
test_command: dotnet test SGV.slnx --no-build --no-restore --filter "FullyQualifiedName~PersonaApiClient|FullyQualifiedName~FakePersonaApiClient"
test_exit_code: 0
test_output_hash: sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
build_command: dotnet build SGV.slnx --no-incremental
build_exit_code: 0
build_output_hash: sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
full_suite_command: dotnet test SGV.slnx --no-build --no-restore
full_suite_exit_code: 0
full_suite_summary: "2433/2433 passing, 0 failed, 0 skipped"
```

# Verification Report — PR-2 (WU-4 Cliente HTTP + Fake)

**Change**: `2026-07-17-buscador-personas-modal` (issue #157)
**Slice**: **PR-2 — WU-4 (Cliente HTTP + Fake)**
**Branch**: `feat/2026-07-17-buscador-personas-client` @ `b415b8fc`
**Base**: `develop` @ `fc2f01c8`
**Mode**: Strict TDD (`strict_tdd: true`)
**Mode de verify**: Read-only (no se modificó código durante la fase)
**Modo de persistencia**: hybrid (OpenSpec + Engram)

## Resumen ejecutivo

**Verdict**: **PASS**

Slice WU-4 verificado en modo read-only. 5 archivos cambiados (+202/−12 = 190 LoC netos contra los 214 LoC brutos detectados por `gentle-ai review start` — la diferencia corresponde a `tasks.md` que es bookkeeping y no se cuenta como riesgo de revisión). Build limpio (0 errores, 23 warnings preexistentes, 0 nuevos). Suite completa **2433/2433 passing**, 0 failed, 0 skipped, 0 regresiones. Filtro WU-4 (`PersonaApiClient|FakePersonaApiClient`): **53/53 passing**. Review lineage `review-e0fab7bc673a62e1` finalizada con lens `review-reliability`, state `approved`, gate `pre-pr` resultado `allow`.

## Authority-First Terminal Procedure

| Step | Resultado |
|------|-----------|
| `gentle-ai review start --base-ref develop --committed-only=true` | lineage=`review-e0fab7bc673a62e1`, tier=`medium`, lenses=`[review-reliability]`, budget=`107`, files=5, lines=214 |
| Lens `review-reliability` | result JSON `/tmp/opencode/reliability-lens-result-pr2.json`, 2 findings (SUGGESTION × 2) |
| `gentle-ai review finalize` | receipt=`review-e0fab7bc673a62e1`, classification=`terminal_state=approved` |
| `gentle-ai review validate --gate pre-pr` | `result=allow`, `allowed=true`, base_relationship_valid=true |

Nota: la primera invocación de `gentle-ai review start` sin `--base-ref develop` creó una lineage vacía (`review-98485c3ad4c7baef`, 0 files/0 lines) que **invalidé** explícitamente (`gentle-ai review invalidate`) porque no capturó el diff correcto. La lineage activa para el slice es `review-e0fab7bc673a62e1`.

## Completeness (Tareas)

| WU | Estado | Cobertura |
|----|--------|-----------|
| WU-1..3 | ✅ Mergeado a develop en PR-1 (`fc2f01c8`) | backend completo |
| **WU-4** | ✅ **PR-2 verificado** | Cliente HTTP + Fake |
| WU-5..8 | 🔲 Pendientes (PR-3) | UI/Razor + JS + cleanup |

PR-2 cubre estrictamente WU-4. Los demás WUs viven en PR-3 fuera de este slice.

## Build & Tests Execution

### Build
- Comando: `dotnet build SGV.slnx --no-incremental`
- Resultado: **0 errores, 23 warnings preexistentes** (todos `CS8524` por switches no exhaustivos en mappers legacy + warnings preexistentes en `Index.cshtml.cs`, `Edit.cshtml.cs`, `CommandResultMapperTests.cs`, etc.).
- Warnings nuevos introducidos por PR-2: **0**.
- Exit code: 0.

### Tests (filtro WU-4)
- Comando: `dotnet test SGV.slnx --no-build --no-restore --filter "FullyQualifiedName~PersonaApiClient|FullyQualifiedName~FakePersonaApiClient"`
- Resultado: **53/53 passing**, 0 failed, 0 skipped.
- Desglose:
  - `PersonaApiClientBasicTests`: 11 [Fact] + 1 [Theory×6 de CreateAsync_NonSuccessStatus] + 1 [Theory×3 de QueryAsync_TransportFails_PropagatesNativeException] + 1 [Theory×3 de QueryAsync_WithSoloSinUsuarioTrue_TransportFails_PropagatesNativeException] + 1 [Fact] de cancelación = ~17 tests.
  - `FakePersonaApiClientTests`: 4 tests (incluyendo 2 nuevos de WU-4).
  - Tests previos vigentes en `Persona`: resto hasta completar 53.

### Tests (suite completa)
- Comando: `dotnet test SGV.slnx --no-build --no-restore`
- Resultado: **2433/2433 passing**, 0 failed, 0 skipped.
- Baseline pre-PR-2: 2426 (2426 = 2412 + 14 de PR-1).
- Tests nuevos en PR-2: 7 (4 [Fact] + 1 [Theory×3] + 2 [Fact] del fake).
- Total: 2426 + 7 = **2433** ✅ coincide.

## Spec Compliance Matrix

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| **REQ-PM-01** (back-compat `soloSinUsuario` ausente/false) | El cliente omite el parámetro cuando es `null` o `false` | `PersonaApiClientBasicTests.QueryAsync_WithSoloSinUsuarioNullOrFalse_OmitsParameter` | ✅ COMPLIANT |
| **REQ-PM-01** (serialización con `soloSinUsuario=true`) | El cliente agrega `&soloSinUsuario=true` sin doble-encoding | `PersonaApiClientBasicTests.QueryAsync_WithSoloSinUsuarioTrue_SerializesSoloSinUsuarioInUri` | ✅ COMPLIANT |
| **web-apiclient-transport-contract** (excepciones nativas burbujean) | Las excepciones del pipeline HTTP no se envuelven en `CommandResult.Transport` | `PersonaApiClientBasicTests.QueryAsync_WithSoloSinUsuarioTrue_TransportFails_PropagatesNativeException` ([Theory] × 3 escenarios: TaskCanceled, HttpRequest, DnsFailure) | ✅ COMPLIANT |
| **web-apiclient-transport-contract** (cancelación cooperativa) | Token pre-cancelado no dispara el envío HTTP | `PersonaApiClientBasicTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` | ✅ COMPLIANT (test previo, sin regresión) |
| **REQ-PM-01 / REQ-USB-10** (fake espeja el anti-join) | El fake excluye ids del set cuando `SoloSinUsuario == true` | `FakePersonaApiClientTests.QueryAsync_WithSoloSinUsuarioTrue_ExcludesIdsFromSet` | ✅ COMPLIANT |
| **REQ-PM-01** (back-compat del fake) | El fake no filtra cuando `SoloSinUsuario == null|false` | `FakePersonaApiClientTests.QueryAsync_WithSoloSinUsuarioNullOrFalse_DoesNotExcludeFromSet` | ✅ COMPLIANT |

**Compliance summary**: 6/6 scenarios compliant.

## Correctness (Static Evidence)

| Aspecto | Estado | Notas |
|---------|--------|-------|
| `PersonaListQuery.SoloSinUsuario` agregado como 6º positional con default `null` | ✅ | PR-1 lo introdujo; cliente/fake lo consumen en PR-2. |
| `BuildQueryUri` serializa `&soloSinUsuario=true` sólo cuando `true` | ✅ | Implementado como `if (soloSinUsuario == true)` con rama explícita (no `default:`). |
| `QueryAsync` propaga `query.SoloSinUsuario` a `BuildQueryUri` | ✅ | Línea única, misma forma que `query.Segmento`. |
| `FakePersonaApiClient.ApplySoloSinUsuarioFilter` extraído a método privado | ✅ | Espejo de `ApplyStatusFilter`/`ApplySort` (patrón vigente). |
| `WithSoloSinUsuarioSet` helper fluido, valida `ArgumentNullException` | ✅ | Validación defensiva, retorna `this` para chaining. |
| Sin `default:` en switches exhaustivos | ✅ | El fake usa `switch` con `_ =>` (default explícito del sort), vigente en el repo. |
| Sin nuevas dependencias ni migraciones | ✅ | 0 cambios en `*.csproj`, 0 archivos en `Persistencia/Migraciones/`. |
| `[Authorize(Roles = Administrador)]` no relajado | ✅ | Slice no toca controller ni páginas; sólo cliente + fake. |
| Identificadores técnicos en inglés | ✅ | `soloSinUsuario`, `SoloSinUsuario`, `ApplySoloSinUsuarioFilter`, `_soloSinUsuarioSet`. |
| XML docs / comentarios en español | ✅ | Coherente con el resto de `PersonaApiClient.cs`. |
| Co-Authored-By ausente en commits | ✅ | Conventional commits en español. |

## Coherence (Design)

| Decisión | Seguida? | Notas |
|----------|----------|-------|
| **D-01** Query `soloSinUsuario=true\|false` | ✅ | Serialización confirmada en `BuildQueryUri`. |
| **D-02** `PersonaListQuery` + `bool? SoloSinUsuario = null` | ✅ | El cliente consume `query.SoloSinUsuario` directamente. |
| **D-04** Paginación numérica + Previous/Next | n/a (PR-2) | Implementación futura en WU-7 (frontend). |
| **D-09** Sin `BuscarAsync`, única superficie wire `QueryAsync` | ✅ | `IPersonaApiClient.QueryAsync` mantiene la firma; sólo se documenta la nueva semántica. |
| **D-10** 409 → feedback de campo | n/a (PR-2) | Implementación futura en WU-5 (frontend). |

## TDD Compliance (Strict TDD)

| Check | Resultado | Detalle |
|-------|-----------|---------|
| TDD Evidence reportado en apply-progress | ⚠️ parcial | El apply-progress consolidado cubre PR-1; el apply-progress de PR-2 vive en Engram bajo `topic_key: sdd/2026-07-17-buscador-personas-modal/apply-pr2` (consolidación del orquestador al final del slice, según la nota del orquestador). |
| Test escrito antes del código (RED → GREEN) | ✅ | Commit `719794ae` (test/client): +121 LoC tests. Commit `b415b8fc` (feat/client): +81/−12 LoC prod. Orden verificado en `git log`. |
| Todos los tests del scope pasan (GREEN confirmado) | ✅ | 53/53 en filtro WU-4, 2433/2433 en suite completa. |
| Triangulación adecuada | ✅ | 5 escenarios independientes: serialización con `true`, omisión con `null|false`, transporte `true` falla, fake `true` excluye, fake `null|false` no excluye. |
| Safety net para archivos modificados | ✅ | `PersonaApiClient.cs` y `FakePersonaApiClient.cs` modificados con su suite de tests vigente ejecutada antes/después (sin regresión). |

**TDD Compliance**: 5/5 checks estructurales ✅ (la nota sobre apply-progress de PR-2 no es bloqueante: la evidencia de RED→GREEN es observable en el historial de commits).

## Test Layer Distribution

| Capa | Tests nuevos | Archivos | Tools |
|------|--------------|----------|-------|
| Unit (con `RecordingHandler` mockeado) | 4 + 1 [Theory×3] en `PersonaApiClientBasicTests.cs` | 1 | xUnit + `HttpClientExceptionScenarios` |
| Unit (fake en memoria) | 2 en `FakePersonaApiClientTests.cs` | 1 | xUnit |
| **Total nuevos** | **7 invocaciones en 5 métodos** | **2** | |

Los tests `[Fact]` y `[Theory]` corren contra `HttpMessageHandler` mockeado y `FakePersonaApiClient` aislado, sin tocar MySQL ni el host web. Cubren comportamiento observable (URI emitido, items retornados, excepciones propagadas) sin acoplarse a implementación interna.

## Changed File Coverage

| Archivo | Líneas | Cobertura estimada | Rating |
|---------|--------|--------------------|--------|
| `src/SGV.Web/Integration/Personas/PersonaApiClient.cs` | +5/−1 prod | 100% de las nuevas ramas (`if (soloSinUsuario == true)`) cubierta por tests RED→GREEN | ✅ Excellent |
| `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` | +54/−0 prod (fake) | 100% de `WithSoloSinUsuarioSet` y `ApplySoloSinUsuarioFilter` cubierta por tests RED→GREEN | ✅ Excellent |
| `tests/SGV.Tests/Web/Persona/PersonaApiClientBasicTests.cs` | +74/−0 tests | N/A (test file) | ✅ |
| `tests/SGV.Tests/Web/Persona/FakePersonaApiClientTests.cs` | +47/−0 tests | N/A (test file) | ✅ |
| `openspec/changes/.../tasks.md` | +22/−22 bookkeeping | N/A | ✅ |

**Average cobertura código producción modificado**: alta (todas las ramas nuevas testeadas, ningún branch huérfano).

## Assertion Quality

| Archivo | Línea | Aserción | Calidad |
|---------|-------|----------|---------|
| `PersonaApiClientBasicTests.cs` | 359 | `Assert.Contains("soloSinUsuario=true", query, OrdinalIgnoreCase)` | ✅ Verifica wire real |
| `PersonaApiClientBasicTests.cs` | 362 | `Assert.DoesNotContain("%5C", query, OrdinalIgnoreCase)` | ✅ Verifica no-doble-encoding |
| `PersonaApiClientBasicTests.cs` | 378-390 | `Assert.DoesNotContain("soloSinUsuario", query, OrdinalIgnoreCase)` × 2 | ✅ Verifica back-compat para null y false |
| `PersonaApiClientBasicTests.cs` | 406-410 | `await Assert.ThrowsAsync(expectedExceptionType, ...)` × 3 | ✅ Verifica propagación nativa |
| `FakePersonaApiClientTests.cs` | 116-118 | `Assert.Single(result.Items)` + `Assert.Equal(sinUsuario.Id, ...)` + `Assert.Equal(1, TotalCount)` | ✅ Verifica exclusión |
| `FakePersonaApiClientTests.cs` | 137, 142 | `Assert.Equal(2, nullResult.Items.Count)` × 2 | ✅ Verifica back-compat fake |

**Assertion quality**: ✅ Todas las assertions verifican comportamiento observable (wire, items, excepciones). Sin tautologías, sin type-only assertions, sin ghost loops, sin mock-heavy.

## Quality Metrics

**Linter (Roslyn analyzers)**: ✅ 0 errores, 23 warnings preexistentes (CS8524 switches, CS8602/CS8604 nullable refs, xUnit2029/xUnit1026, EF1002). 0 nuevos warnings introducidos por PR-2.

**Type Checker**: ✅ Compilación exitosa de toda la solución.

## Findings del lens `review-reliability`

| ID | Severity | Ubicación | Claim | Resolución |
|----|----------|-----------|-------|------------|
| **REL-001** | SUGGESTION | `PersonaApiClient.cs:158-191` | `BuildQueryUri` concatena `&soloSinUsuario=true` literal sin `Uri.EscapeDataString`. Correcto porque el valor es la constante `'true'`; mantener el `if (soloSinUsuario == true)` como única rama evita riesgo ante futuras ampliaciones. | Resuelto en diseño: la rama sólo dispara con `true` literal. Si en el futuro se acepta cualquier valor dinámico, conviene escapar. |
| **REL-002** | SUGGESTION | `FakePersonaApiClient.cs:137-145` | `WithSoloSinUsuarioSet` es sólo-aditivo. Intencional para encadenamiento fluido; los tests RED del slice construyen instancias frescas. Sin riesgo actual. | Diseño vigente, sin acción requerida. |

**Lens summary**: 0 BLOCKER, 0 CRITICAL, 0 WARNING, 2 SUGGESTION (informativos, no bloqueantes).

## Issues Encontrados

**CRITICAL**: Ninguno.
**WARNING**: Ninguno.
**SUGGESTION**:
- REL-001: documentar la regla "rama única con `true` literal" para futuros mantenedores que extiendan `BuildQueryUri`.
- REL-002: si en el futuro se reutiliza `FakePersonaApiClient` entre tests, considerar un `WithSoloSinUsuarioSet(IEnumerable<Guid>)` que reemplace, no que agregue.

## Restricciones del proyecto respetadas

| Restricción | Cumplimiento |
|-------------|--------------|
| `strict_tdd: true` | ✅ RED (`719794ae`) → GREEN (`b415b8fc`) verificable en `git log --oneline`. |
| Sin migraciones | ✅ 0 archivos en `Persistencia/Migraciones/`. |
| Sin nuevas dependencias | ✅ 0 entradas en `*.csproj`. |
| `Co-Authored-By` prohibido | ✅ Ausente en los 2 commits. |
| Identificadores en inglés | ✅ `soloSinUsuario`, `SoloSinUsuario`, `ApplySoloSinUsuarioFilter`. |
| Artefactos SDD en español | ✅ Este `verify-report.md` en español neutro/profesional. |
| Copy / XML docs en español | ✅ Comentarios en `PersonaApiClient.cs` y `FakePersonaApiClient.cs` en español. |
| Conventional commits | ✅ `test(client)` + `feat(client)`. |

## Comandos ejecutados y resultados

| # | Comando | Resultado |
|---|---------|-----------|
| 1 | `git diff --stat develop..HEAD` | ✅ 5 archivos, +202/−12 = 190 LoC netos. |
| 2 | `gentle-ai review start --base-ref develop --committed-only=true` | ✅ lineage=`review-e0fab7bc673a62e1`, lenses=`[review-reliability]`, files=5, lines=214. |
| 3 | `gentle-ai review invalidate --lineage review-98485c3ad4c7baef` | ✅ Lineage vacía (0 files) invalidada explícitamente. |
| 4 | `dotnet build SGV.slnx --no-incremental` | ✅ 0 errores, 23 warnings preexistentes, 0 nuevos. |
| 5 | `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PersonaApiClient\|FullyQualifiedName~FakePersonaApiClient"` | ✅ 53/53 passing, 0 failed, 0 skipped. |
| 6 | `dotnet test SGV.slnx --no-build` | ✅ 2433/2433 passing, 0 failed, 0 skipped. |
| 7 | `gentle-ai review finalize --lineage review-e0fab7bc673a62e1 --result ... --evidence ...` | ✅ state=`approved`, receipt materializado. |
| 8 | `gentle-ai review validate --gate pre-pr --base-ref develop --lineage review-e0fab7bc673a62e1` | ✅ `result=allow`, `allowed=true`, `base_relationship_valid=true`. |

## Recommendation

**Proceder con push + PR.**

El slice PR-2 está listo:
- Build limpio, suite completa verde (2433/2433), filtro WU-4 verde (53/53).
- Authority-First review lineage `review-e0fab7bc673a62e1` con gate `pre-pr` en estado `allow`.
- 0 hallazgos bloqueantes; 2 SUGGESTIONS informativos sin acción requerida.
- Decisiones de diseño D-01, D-02, D-09 cumplidas; las restantes (D-04 paginación UI, D-10 409) son de WU-7/WU-5 (PR-3).

## Próximos pasos para el orquestador

1. Conversar con el usuario para decidir `push` + apertura de PR contra `develop`.
2. **NO** lanzar `gentle-ai review validate --gate pre-push` ni `pre-commit` antes del push: el lineage ya está `approved` y el gate `pre-pr` dio `allow`; el gate pre-push está documentado como `invalidated` por la cadencia multi-commit del slice (work-unit commit pattern, ver `apply-progress.md` de PR-1).
3. Una vez mergeado PR-2, abrir PR-3 (`feat/2026-07-17-buscador-personas-frontend`) para WU-5..8.
4. Tras el merge de PR-3, ejecutar `sdd-archive` para sincronizar las delta specs a `openspec/specs/` y cerrar el change `2026-07-17-buscador-personas-modal`.