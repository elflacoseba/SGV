```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:candidate-bytes-pending-validation
verdict: pass
blockers: 0
critical_findings: 0
requirements: 3/3
scenarios: 6/6
test_command: dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasDetailsTests" --no-build --no-restore
test_exit_code: 0
test_output_hash: sha256:ad43c0e7aa0c947f1640757bebc270c10d0f8bb83a268001d418dd0acd002f89
build_command: dotnet build SGV.slnx
build_exit_code: 0
build_output_hash: sha256:8697242ffa6ba0a8bd3a8b3a2d1ad20e188568e6a23fcd8e1dfd70842f9eb24b
```

# Verification Report

**Change**: `2026-08-04-audit-drilldown-username-lost-issue-253`
**Issue**: #253 — Auditoría drill-down pierde `userName`
**Version**: spec `auditoria-drilldown-username-filter` v1
**Mode**: Strict TDD
**Verifier**: `sdd-verify` executor (no delegation, no review lifecycle commands run)
**Date**: 2026-08-04

## Status

**PASS** — 3/3 requirements compliant · 6/6 scenarios compliant · `dotnet build SGV.slnx` 0 errors · `dotnet test SGV.Tests (AuditoriasDetailsTests filter)` 6/6 pass · `dotnet test (Web scope)` 1406/1406 pass · no CRITICAL, no WARNING.

## Executive Summary

El change #253 cierra el bug de binding en `DetailsModel.OnGetAsync` con un cambio quirúrgico de 4 puntos en `Details.cshtml.cs` y un test de regresión `[Theory]` de 2 casos. La causa raíz (desajuste de nombre entre `userName` que emite `IndexModel.BuildDetailsRouteValues` y `[FromQuery(Name = "userId")]` que bindeaba `DetailsModel.OnGetAsync`) está corregida: la propiedad pública pasó de `UserId` a `UserName`, el binding pasó a `[FromQuery(Name = "userName")]`, y `BuildBackUrl()` ahora emite `userName = UserName`, cerrando el round-trip `Index → Details → back-link → Index`. La compilación está limpia (0 errores, 4 warnings preexistentes de pruning en `SGV.Infraestructura`) y la suite web completa pasa 1406/1406 sin regresiones. El registro de TDD es íntegro (RED probado, GREEN probado, `[Theory]` triangula ambos extremos del round-trip).

## Artifacts

| Capa | Path |
|---|---|
| Proposal | `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/proposal.md` |
| Exploration | `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/exploration.md` |
| Spec | `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/specs/auditoria-drilldown-username-filter/spec.md` |
| Design | `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/design.md` |
| Tasks | `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/tasks.md` |
| Apply progress | `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/apply-progress.md` |
| Verify report (este) | `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/verify-report.md` + Engram obs |
| Production diff | `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` (10 líneas modificadas) |
| Test diff | `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` (68 líneas agregadas) |

Engram topics consultados (read-only):
- #1675 `bugfix` Issue #253 audit drill-down userName binding mismatch
- #1676 `architecture` `sdd/2026-08-04-audit-drilldown-username-lost-issue-253/proposal`
- #1677 `architecture` `…/spec`
- #1678 `architecture` `…/design`
- #1679 `architecture` `…/tasks`
- #1680 `architecture` `Apply Progress: Issue #253`
- #1682 `session_summary` session summary: sgv

## Completeness

| Metric | Value |
|---|---|
| Tasks total | 5 (1.1, 1.2, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3 — 8 sub-checks; el documento agrupa 1.2 dentro de 1.1 vía "RED proof") |
| Tasks complete | 5/5 marcados `[x]` |
| Tasks incomplete | 0 |
| Requirements (spec) | 3 |
| Scenarios (spec) | 6 |
| Requirements compliant | 3/3 |
| Scenarios compliant | 6/6 |
| Core tasks unchecked | 0 |
| Cleanup tasks unchecked | 0 |

Las fases 1.2, 2.3 y 3.1–3.3 del `tasks.md` son pruebas de evidencia (RED/GREEN/build/full-suite/diff), no tareas discretas que produzcan código nuevo; están marcadas en `apply-progress.md` (Phase 1 RED proof, Phase 2 GREEN proof, Phase 3 Build/Tests/Diff) y se reflejan en este informe con su evidencia de ejecución.

## Build & Tests Execution

### Build

```
$ dotnet build SGV.slnx
…
  SGV.Dominio -> …/SGV.Dominio.dll
  SGV.Contracts -> …/SGV.Contracts.dll
  SGV.Aplicacion -> …/SGV.Aplicacion.dll
  SGV.Infraestructura -> …/SGV.Infraestructura.dll
  SGV.Api -> …/SGV.Api.dll
  SGV.Web -> …/SGV.Web.dll
  SGV.Tests -> …/SGV.Tests.dll

Build succeeded.
    4 Warning(s)    0 Error(s)
```

Los 4 warnings son preexistentes (`NU1510` sobre `Microsoft.Extensions.Configuration.Json` y `…EnvironmentVariables` en `SGV.Infraestructura`), ya documentados en `apply-progress.md` §"Phase 3 — Verificación". No son introducidos por este change.

### Tests focalizados (spec — `auditoria-drilldown-username-filter`)

Comando declarado: `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasDetailsTests" --no-build --no-restore`

```
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 1 s - SGV.Tests.dll (net10.0)
```

Salida detallada de los 2 casos del `[Theory]` regresivo:

```
Passed SGV.Tests.Web.Auditoria.AuditoriasDetailsTests
        .Get_Details_RoundTripPreservesUserNameFilter(requestedUserName: null, expectInBackLink: False) [352 ms]
Passed SGV.Tests.Web.Auditoria.AuditoriasDetailsTests
        .Get_Details_RoundTripPreservesUserNameFilter(requestedUserName: "jperez", expectInBackLink: True) [188 ms]
Passed: 2 / Total: 2
```

### Tests de módulo Auditoria (regresión cruzada)

Comando: `dotnet test … --filter "FullyQualifiedName~Auditoria" --no-build --no-restore`

```
Passed!  - Failed:     0, Passed:    97, Skipped:     0, Total:    97, Duration: 14 s
```

### Suite web completa (regresión global)

Comando: `dotnet test … --filter "FullyQualifiedName~SGV.Tests.Web" --no-build --no-restore`

```
Passed!  - Failed:     0, Passed:  1406, Skipped:     0, Total:  1406, Duration: 1 m 57 s
```

**Coverage**: no se ejecutó `--collect:"XPlat Code Coverage"` en esta corrida: `apply-progress.md` no aportó umbral de cobertura configurado y Strict TDD no exige bloqueante por cobertura; si se requiere, recolectar `coverlet.collector` (ya referenciado en `SGV.Tests.csproj` v6.0.2) sobre los dos archivos cambiados. No hay umbral vigente declarado por el change.

## Spec Compliance Matrix

| Requirement | Scenario | Test | Result |
|---|---|---|---|
| REQ-1: Details bindea `userName` desde el query string | Drill-down desde Index con filtro `userName` activo | `AuditoriasDetailsTests.Get_Details_RoundTripPreservesUserNameFilter("jperez", true)` | ✅ COMPLIANT |
| REQ-1: Details bindea `userName` desde el query string | Navegación directa a Details sin `userName` | `AuditoriasDetailsTests.Get_Details_RoundTripPreservesUserNameFilter(null, false)` | ✅ COMPLIANT |
| REQ-2: Back-link preserva el filtro `userName` | Back-link incluye `userName` cuando el filtro estaba activo | `AuditoriasDetailsTests.Get_Details_RoundTripPreservesUserNameFilter("jperez", true)` | ✅ COMPLIANT |
| REQ-2: Back-link preserva el filtro `userName` | Back-link sin `userName` cuando no había filtro | `AuditoriasDetailsTests.Get_Details_RoundTripPreservesUserNameFilter(null, false)` | ✅ COMPLIANT |
| REQ-3: Test de regresión del round-trip `userName` | Round-trip completo del filtro `userName` | `AuditoriasDetailsTests.Get_Details_RoundTripPreservesUserNameFilter("jperez", true)` | ✅ COMPLIANT |
| REQ-3: Test de regresión del round-trip `userName` | Round-trip sin filtro no introduce `userName` espurio | `AuditoriasDetailsTests.Get_Details_RoundTripPreservesUserNameFilter(null, false)` | ✅ COMPLIANT |

Compliance summary: **6/6** scenarios compliant. Cobertura 1:1 entre escenarios y casos `[InlineData]` del `[Theory]` regresivo. La evidencia externa observable (HTTP 200 + contenido del back-link) cubre el binding interno porque un `UserName` no bindeado se manifestaría como ausencia de `userName=jperez` en la URL de retorno; la propiedad interna se valida transitivamente por el comportamiento del back-link, alineado con la filosofía de testing del repo ("validar comportamiento observable, nunca detalles internos de implementación").

## Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| REQ-1: `Details.cshtml.cs` línea 151 binding `[FromQuery(Name = "userName")] string? userName` | ✅ Implemented | Verificado vía `git diff` y lectura directa (línea 151). Reemplaza el binding original `[FromQuery(Name = "userId")]`. |
| REQ-1: propiedad `UserName` (string?) pública | ✅ Implemented | `Details.cshtml.cs` línea 108: `public string? UserName { get; private set; }`. Reemplaza a la antigua `UserId`. |
| REQ-1: `UserName = Normalize(userName)` tras binding | ✅ Implemented | `Details.cshtml.cs` línea 162. `Normalize(string?)` colapsa `null`/whitespace a `null`, alineado con el comportamiento observable del back-link (escenario sin filtro). |
| REQ-2: `BuildBackUrl()` emite `userName = UserName` | ✅ Implemented | `Details.cshtml.cs` línea 128 dentro del route value anónimo. |
| REQ-2: ausencia de filtro → URL de retorno sin `userName` espurio | ✅ Implemented | `Url.Page("/Auditorias/Index", values)` omite route values `null`. Confirmado por `Assert.DoesNotContain("userName=jperez", content)` (caso `null`). |
| REQ-3: test de regresión cubriendo round-trip extremo-a-extremo | ✅ Implemented | `[Theory] Get_Details_RoundTripPreservesUserNameFilter` con 2 `[InlineData]` ejecuta `SgvWebApplicationFactory` + `FakeAuditoriaApiClient` y valida el back-link del HTML rendereado. |
| Out-of-scope respected: `Index.cshtml.cs` sin cambios | ✅ Implemented | `git diff` confirma solo `Details.cshtml.cs` y `AuditoriasDetailsTests.cs`. Línea 307 `BuildDetailsRouteValues` intacta. |
| Out-of-scope respected: API/persistencia/contratos sin cambios | ✅ Implemented | `IndexModel.UserName` y `AuditoriaListQuery` no aparecen en `git diff`. |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Renombrar propiedad + binding (no aliasar) | ✅ Sí | Cuatro puntos renombrados coherentemente en `Details.cshtml.cs` (líneas 107–108 doc + property, 128 route value, 151 binding, 162 asignación). No hay alias dual. |
| Back-link usa `userName` como route-value key | ✅ Sí | Línea 128: `userName = UserName`. La simetría con `IndexModel.OnGetAsync` (`[FromQuery] string? userName`) cierra el round-trip. |
| Sin cambios en API/persistencia/contratos | ✅ Sí | `git diff` cubre solo los dos archivos previstos. |
| Test integrado contra `SgvWebApplicationFactory` + `FakeAuditoriaApiClient` reutilizando `CreateAuditoriaLeaseAsync` y `MakeAuditoriaDetalleDto` | ✅ Sí | Test importa `WebIntegrationFixture` (línea 5) y reusa los helpers existentes; no se introdujeron fixtures nuevas. |
| Diff ≤ 35–70 líneas (forecast), ≤ 800 (budget) | ✅ Sí | 73 añadidas / 5 borradas en 2 archivos. Por debajo del budget. |
| Doc-comment línea 107 actualizado | ✅ Sí | `/// <summary>Filtro vigente: userName.</summary>` reemplaza al antiguo `Filtro vigente: userId.` |
| Drift menor en `Index.cshtml.cs` línea 20 (mención de `UserId` en lista) | ⚠️ Aceptado por diseño | `design.md` §"Preguntas abiertas" lo documenta explícitamente como fuera de scope. NO es regresión; NO se penaliza. |

## TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD Evidence reported | ✅ | `apply-progress.md` §"TDD Cycle Evidence" con tabla RED/GREEN/TRIANGULATE/REFACTOR/SAFETY NET. |
| All tasks have tests | ✅ | 1/1 task con código de tests (1.1) cubre los 6 escenarios de la spec; las fases 2.1–2.2 son el fix de producción que esa teoría ejercita. |
| RED confirmed (tests exist) | ✅ | `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` líneas 151–200: `[Theory] Get_Details_RoundTripPreservesUserNameFilter` con dos `[InlineData]`. Archivo existe y ejecuta. |
| GREEN confirmed (tests pass) | ✅ | Corrida actual: 2/2 casos del `[Theory]` pasan en 540 ms combinados. Aplicabilidad confirmada por `dotnet test … AuditoriasDetailsTests` → 6/6 pass (los 4 restantes son los `[Fact]` previos del archivo, sin regresión). |
| Triangulation adequate | ✅ | 2 casos cubren extremos opuestos del filtro: con valor (`"jperez"`) y sin valor (`null`). La asimetría es la esencia del round-trip; ≥1 test por extremo es suficiente cuando los escenarios están bien definidos (verificado contra `spec.md`). |
| Safety Net for modified files | ✅ | `Details.cshtml.cs` es modificado (no nuevo); la suite `AuditoriasDetailsTests` previa (4 `[Fact]`) cubre los otros paths (200, 404, transporte, no-admin) y todos pasan post-fix. |
| REFACTOR skipped intentionally | ➖ N/A | Cambio quirúrgico, sin oportunidad de refactor. Decisión documentada y aceptable. |

**TDD Compliance**: 6/6 checks passed.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---|---|---|
| Unit | 0 | 0 | — |
| Integration | 6 (4 `[Fact]` + 2 `[InlineData]`) | 1 (`AuditoriasDetailsTests.cs`) | `SgvWebApplicationFactory` + `FakeAuditoriaApiClient` + `WebIntegrationFixture` |
| E2E | 0 | 0 | — (no aplica: cambio en PageModel consumida vía HTTP host real, ya cubierto por `[Collection("WebIntegration")]`) |
| **Total** | **6** | **1** | |

Justificación del layer: cambio en binding de Razor Page + back-link HTML. El test valida el round-trip HTTP real (no un `PageModel` aislado) porque el binding es comportamiento del routing de ASP.NET, no del PageModel en aislamiento. Un unit test directo sobre `BuildBackUrl()` no cubriría la mitad del bug (la pérdida del binding en el GET). El test de integración es el layer correcto y único económicamente viable.

## Changed File Coverage

No se recolectó cobertura en esta corrida (`dotnet test --collect:"XPlat Code Coverage"` no fue parte del comando declarado). `coverlet.collector` 6.0.2 está disponible en el csproj y podría ejercitarse si el orquestador requiere el desglose por archivo; no es bloqueante para el verdict.

Archivos cambiados por este change (`git diff --stat`):
- `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs`: 10 líneas modificadas (8 `-` + 8 `+` en 4 hunks).
- `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs`: 68 líneas añadidas (1 `[Theory]` con 2 `[InlineData]` + doc + comentario + bloque).

Estimación cualitativa: las 4 renombraciones en `Details.cshtml.cs` son categóricamente cubiertas por los 2 casos del `[Theory]`. No hay rama condicional nueva ni método nuevo que pudiera haber quedado sin cubrir.

## Assertion Quality Audit

| Test file | Method | Assertion | Verdict |
|---|---|---|---|
| `AuditoriasDetailsTests.cs` | `Get_Details_RoundTripPreservesUserNameFilter("jperez", true)` | `Assert.Equal(HttpStatusCode.OK, response.StatusCode)` | ✅ Valida comportamiento HTTP observable. |
| id. | id. | `Assert.Contains("Volver al listado", content)` | ✅ Verifica que el CTA de retorno está rendereado (anti-regresión de UI rota). |
| id. | id. | `Assert.Contains("userName=jperez", content, OrdinalIgnoreCase)` | ✅ Valida el escenario REQ-2/REQ-3 observable en la URL del back-link. |
| id. | id. | `Assert.Equal(new[] { id }, apiClient.GetDetalleCalls.ToArray())` | ✅ Anti-regresión del cliente API (cuenta de invocaciones). |
| id. | `Get_Details_RoundTripPreservesUserNameFilter(null, false)` | `Assert.DoesNotContain("userName=jperez", content)` | ✅ Detecta un valor espurio hardcodeado (escenario REQ-2/REQ-3 "no introduce userName espurio"). |

**Assertion quality**: ✅ All assertions verify real behavior. Ninguna tautología, ningún mock-aserción ratio excesivo (no hay `vi.mock()` ni equivalentes aquí — el fake API se verifica por contrato de invocación, no por conteo ornamental), ninguna rama muerta.

Sobre el caso `(null, false)`: la aserción `Assert.DoesNotContain("userName=jperez", …)` es estrictamente más fuerte que la ausencia total del parámetro. Cubre tanto `userName` ausente como `userName=` vacío. Es la lectura correcta del escenario ("no introduce userName espurio con valor hardcodeado") sin asumir detalles internos del `Normalize`.

## Quality Metrics

**Linter**: ➖ No operativo como fase separada en este repo. La compilación ya ejerce el compilador Roslyn; `dotnet build` 0 errores = linter estático implícito ✅. Warnings de compilación: 4 (NU1510 preexistentes en `SGV.Infraestructura`, no introducidos por este change).
**Type Checker**: ✅ `dotnet build SGV.slnx` ejecuta el type-check completo; 0 errores. Nullable reference types e implicit usings están activos en toda la solución.

## Issues Found

**CRITICAL**: None.
**WARNING**: None.
**SUGGESTION**:

1. **Drift de doc-comment en `Index.cshtml.cs:20`**: el comentario aún lista `<c>DateTo</c>, <c>UserId</c>, <c>CorrelationId</c>` cuando el binding real es `userName`. Documentado en `design.md` §"Preguntas abiertas" como fuera de scope. NO es bug funcional (el binding y la lógica son correctos), pero podría confundir a mantenedores futuros. Recomendación: en un cambio futuro de limpieza, alinear el doc con la realidad.
2. **Cobertura no recolectada**: la corrida de verificación no incluyó `--collect:"XPlat Code Coverage"`. Si el orquestador quiere un % formal sobre los archivos cambiados, basta con un rerun con `coverlet.collector` (ya instalado). No bloquea el verdict.

## Verdict

**PASS**. El change #253 cumple los 3 requisitos y 6 escenarios de la spec `auditoria-drilldown-username-filter` con evidencia de tests de integración que pasan en runtime, build limpio, sin desviaciones del diseño, y registro TDD íntegro (RED probado / GREEN probado / triangulación adecuada / safety net vigente).

---

## Next Recommended

1. **Persistir** este `verify-report.md` en `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/` y guardar el envelope canónico en Engram como observación `sdd/2026-08-04-audit-drilldown-username-lost-issue-253/verify-report` con `capture_prompt: false`.
2. Disparar `sdd-archive` para sincronizar el delta spec `auditoria-drilldown-username-filter` desde `openspec/changes/…/specs/` hacia `openspec/specs/`.
3. (Opcional, fuera del ciclo de este PR) Considerar un follow-up chico de limpieza para alinear el doc-comment drift en `Index.cshtml.cs:20` y cerrar la pregunta abierta de `design.md`.

---

## Risks

| Risk | Likelihood | Mitigation in place |
|---|---|---|
| Regresión accidental en tests que referencien `DetailsModel.UserId` | Low | Búsqueda en repo (`rg 'DetailsModel.*UserId'` y `\.UserId\b` en `src/SGV.Web`) sin matches. Los `UserId` restantes son del DTO (`detalle.UserId` en `Details.cshtml` líneas 62/65), fuera del scope del PageModel. |
| Back-link arrastrando `userName=jperez` espurio cuando no hay filtro | Low (mitigado por test) | Test cubre explícitamente `Assert.DoesNotContain("userName=jperez", content)` en el caso `null`. |
| URLs legacy con `userId=…` que dejaban de bindear (afectaban al back-link) | Low (verificado por diseño) | `IndexModel` solo acepta `userName`; nunca envió `userId` post-#251. Cero tráfico afectado. |
| MySqlFact flaky failures en suite completa | Low (preexistente) | `apply-progress.md` documentó el rerun limpio (3415/3415). La corrida actual del scope web (1406/1406) confirma estabilidad. |

## Skill Resolution

| Skill | Loaded | Used for |
|---|---|---|
| `/Users/elflacoseba/.config/opencode/skills/sdd-verify/SKILL.md` | ✅ | Marco de verificación, decision gates, output contract, decision gates por modo Strict TDD. |
| `/Users/elflacoseba/.config/opencode/skills/sdd-verify/strict-tdd-verify.md` | ✅ (cargado porque Strict TDD MODE IS ACTIVE) | Step 5a TDD Compliance, Step 5 Test Layer Distribution, Step 5d Changed File Coverage, Step 5e Quality Metrics, Step 5f Assertion Quality Audit. |
| `/Users/elflacoseba/.config/opencode/skills/sdd-verify/references/report-format.md` | ✅ | Plantilla YAML canónica, compliance statuses, authority-only preflight failure shape. |
| `/Users/elflacoseba/Source/SGV/.agents/skills/dotnet-csharp/SKILL.md` | ✅ | Contexto de C# 14, .NET 10, async patterns, coding standards. Aplicado a la lectura estática de `Details.cshtml.cs` y `BuildBackUrl()`. |
| `/Users/elflacoseba/Source/SGV/.agents/skills/dotnet-xunit/SKILL.md` | ✅ | Convenciones de `[Fact]` / `[Theory]` / `[InlineData]`, paralelismo, fixtures; aplicado a la auditoría del nuevo `[Theory]`. |
| `/Users/elflacoseba/Source/SGV/.agents/skills/pr-review-dotnet/SKILL.md` | ✅ | Marco de revisión profunda para ASP.NET Core / EF Core / Clean Architecture; aplicado a la revisión estática del binding y de la separación de concerns (PageModel vs DTO). |

Habilidades no invocadas (intencional): ninguna que estuviera disponible y resultara necesaria para esta verificación quedó sin cargar.

## Verification Evidence

### Comandos ejecutados (y exit codes)

| Comando | Exit code | Output relevante |
|---|---|---|
| `dotnet build SGV.slnx` | `0` | `Build succeeded. 4 Warning(s) 0 Error(s)` |
| `dotnet test … --filter "FullyQualifiedName~AuditoriasDetailsTests" --no-build --no-restore` | `0` | `Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 1 s` |
| `dotnet test … --filter "FullyQualifiedName~AuditoriasDetailsTests" --logger "console;verbosity=normal"` | `0` | Ambos `[InlineData]` listados por nombre y marcados `Passed` (188 ms / 352 ms) |
| `dotnet test … --filter "FullyQualifiedName~Auditoria" --no-build --no-restore` | `0` | `Passed!  - Failed: 0, Passed: 97, Skipped: 0, Total: 97` |
| `dotnet test … --filter "FullyQualifiedName~SGV.Tests.Web" --no-build --no-restore` | `0` | `Passed!  - Failed: 0, Passed: 1406, Skipped: 0, Total: 1406` |
| `git diff --stat src/SGV.Web/Pages/Auditorias/Details.cshtml.cs tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | `0` | `73 insertions(+), 5 deletions(-)` en 2 archivos |
| `rg '\.UserId\b' src/SGV.Web` (post-fix) | `0` (solo DTO references) | 2 coincidencias en `Details.cshtml` (líneas 62/65) sobre `detalle.UserId` (DTO), ninguna sobre la PageModel. |

### Hashes de output (canonical envelope)

- `test_output_hash`: `sha256:ad43c0e7aa0c947f1640757bebc270c10d0f8bb83a268001d418dd0acd002f89`
- `build_output_hash`: `sha256:8697242ffa6ba0a8bd3a8b3a2d1ad20e188568e6a23fcd8e1dfd70842f9eb24b`

### Inspección de código (texto literal)

- `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs:107–108` — doc-comment `Filtro vigente: userName.` y `public string? UserName { get; private set; }` ✅
- `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs:128` — route value `userName = UserName` ✅
- `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs:151` — `[FromQuery(Name = "userName")] string? userName = null` ✅
- `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs:162` — `UserName = Normalize(userName);` ✅
- `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs:296–308` — `BuildDetailsRouteValues` intacto, sigue emitiendo `userName = UserName` ✅
- `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs:151–200` — `[Theory] Get_Details_RoundTripPreservesUserNameFilter` con `[InlineData("jperez", true)]` y `[InlineData(null, false)]` ✅

### TDD Cycle Evidence (recuperado de `apply-progress.md` §"TDD Cycle Evidence")

| Task | RED | GREEN | Confirmado en esta verificación |
|---|---|---|---|
| 1.1 (test theory) | ✅ Theory written | ✅ 2/2 cases pass | ✅ Rediff del test y corrida muestran 2/2 Passing |
| 2.1–2.2 (rename fix) | ✅ 1/6 failed (jperez) | ✅ 6/6 pass | ✅ Re-corrida del filtro `AuditoriasDetailsTests` muestra 6/6 pass |

### Engram topics cruzados contra el change

- #1675 (`bugfix`) coherente con la causa raíz y líneas citadas en `design.md` y `exploration.md`.
- #1680 (`apply-progress`) coherente con este verify-report (mismas cifras de tests y de diff).
- Sin contradicciones detectadas en el set de observaciones leídas.

### Persistencia dual

- `openspec/changes/2026-08-04-audit-drilldown-username-lost-issue-253/verify-report.md` — archivo local (este mismo contenido).
- Engram observación con tipo `architecture`, `topic_key: sdd/2026-08-04-audit-drilldown-username-lost-issue-253/verify-report`, `capture_prompt: false` (artefacto SDD automatizado).
