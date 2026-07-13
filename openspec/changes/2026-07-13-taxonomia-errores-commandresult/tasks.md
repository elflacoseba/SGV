# Tasks: Taxonomía única de errores para `CommandResult` y clientes HTTP de Web

Change: `2026-07-13-taxonomia-errores-commandresult` · Issue: #125 · Modo: híbrido (Engram + filesystem).
Idioma: español (descripción), inglés (identificadores y paths).

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines (total) | ~2050 LoC |
| Estimated changed lines (per PR) | 350 / 750 / 700 / 250 |
| 400-line budget risk (single-PR) | **High** |
| 400-line budget risk (chained) | **Low–Medium** (cada PR cae en 250–750) |
| Chained PRs recommended | Yes |
| Suggested split | PR #1 → PR #2 → {PR #3, PR #4} |
| Delivery strategy cacheada | auto-forecast (resuelve a auto-chain) |
| Chain strategy | **stacked-to-main** (confirmado por usuario) |
| Decision needed before apply | **No** (chain strategy ya decidida) |
| Branch base | `develop` (cada PR mergea a develop; siguiente PR se rebasa desde develop tras merge) |

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

### Suggested Work Units (PRs)

| Unit | Goal | Branch | Likely PR | Notes |
|------|------|--------|-----------|-------|
| 1 | Contratos `ErrorCategoria` + mappers + 6 `*Error.Categoria` + 5 `*DeleteResult.Categoria/StatusCode?` + `[Obsolete]` enums + tests contract | `fix/125-s1-contracts-error-taxonomy` | PR #1 | Base: develop. Tests + producción + 1 doc. Sin call sites externos actualizados (los `[Obsolete]` aceptan ambos worlds). |
| 2 | `CommandResultMapper` único + `IsDnsFailure` + 5 clientes HTTP migrados + tests cancel/timeout/DNS | `fix/125-s2-mapper-clients` | PR #2 | Base: develop (rebase después de merge de PR #1). Tests RED parametrizados sobre RecordingHandler + HttpClientExceptionScenarios extendido. |
| 3 | `IAuthSessionRedirector` + 14 PageModels exhaustivos + open-redirect guards + copy canónica | `fix/125-s3-redirector-page-models` | PR #3 | Base: develop (rebase después de merge de PR #2). Switch expressions exhaustivos sin `default:`. |
| 4 | `ApiResults.MapCategoria` exhaustivo + tests `DeleteResultContract` para 5 dominios + actualización de `SGV.Aplicacion/*ServicioComandos` para popular `Categoria` | `fix/125-s4-apiresults-delete-results` | PR #4 | Base: develop (rebase después de merge de PR #2). Independiente de PR #3 (PR #3 y PR #4 pueden mergear en cualquier orden entre sí, sólo dependen de PR #1 + PR #2). |

### Diagrama topológico

```
develop
  │
  ├─ PR #1 ──► (merge a develop)
  │     ▲
  │     │
  ├─ PR #2 ──► (merge a develop, rebased desde develop tras PR #1)
  │     ▲           ▲
  │     │           │
  ├─ PR #3 ────────┤ (merge a develop, rebased desde develop tras PR #2)
  │                 │
  └─ PR #4 ────────┘ (merge a develop, rebased desde develop tras PR #2)
                       (PR #3 y PR #4 NO se bloquean entre sí)
```

`chain_strategy: stacked-to-main` confirmado: cada PR mergea directo a `develop`; el branch de la PR subsiguiente se rebasa desde `develop` una vez mergeada la previa. Si la PR #2 observa el diff de la PR #1 en su branch, la base es incorrecta — rebase antes de review.

### Decisión sobre divergencia de forecast

El design §12 declara ~1850 LoC totales; el desglose operativo por task (T-N) suma ~2050 LoC. La divergencia +200 LoC se explica por:
- **Slice 2** (+120 LoC): las pruebas parametrizadas de cancel/timeout-vs-cancel/timeout en los 5 clientes agregan ~30 LoC por cliente que el forecast agregado del design subestimó.
- **Slice 3** (+80 LoC): el helper de exhaustividad por PageModel (T-3.5) más los 14 smoke tests suman ~80 LoC que el design fusionó con la migración de los PageModels.

Riesgo **LOW** documentado en §Riesgos del plan.

## Convenciones de tasks

- Numeración global `T-N.M` donde `N` = slice (1..4) y `M` = ordinal dentro del slice.
- Cada task incluye:
  - **Slice** y **Files** (path absoluto o relativo a la raíz del repo).
  - **RED**: nombre de test, archivo, patrón Given/When/Then sucinto.
  - **GREEN**: cambio mínimo de producción que hace pasar el test.
  - **REFACTOR**: limpieza ya agendada (si aplica).
  - **Verification**: comandos exactos.
  - **Rollback**: comando o acción de revertir.
  - **Dependencies**: tasks previas que deben estar mergeadas en `develop`.
  - **Estimación**: LoC ± incertidumbre (incluye tests + producción cuando aplica).
  - **Commit guidance**: número de commits y título conventional de cada uno.
- TDD estricto: el primer commit de cada feature va a ser el archivo de test SOLAMENTE (compila pero falla). El segundo commit es el código de producción que lo hace verde. NO invertir el orden.
- Los archivos de tests nuevos viven en `tests/SGV.Tests/Contracts/` (directorio nuevo) o `tests/SGV.Tests/Web/Common/` (ya existente) según corresponda. El test de contrato `CargoSkillDeleteResultContractTests.cs` preexistente en `tests/SGV.Tests/Aplicacion/Organizacion/` se extiende in-place (no se mueve) para evitar churn de paths.

## Fase 1 — Slice 1: Contratos (`ErrorCategoria` + mappers + 6 `*Error` + 5 `*DeleteResult`)

### [x] T-1.1 — RED ErrorCategoriaTests (shape + leaf)
- **Slice**: 1
- **Files**: `tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs` (new); `tests/SGV.Tests/Contracts/SGV.Contracts.csproj.Inspector.cs` (new helper, opcional).
- **RED**: Dos tests. `Enum_HasSevenVariantsInOrder` (GIVEN el assembly `SGV.Contracts` compilado; WHEN `Enum.GetValues<ErrorCategoria>()`; THEN 7 valores en orden 0..6, exactos a `NotFound`, `Conflict`, `Validation`, `Unauthorized`, `Forbidden`, `Transport`, `Unexpected`). `ContractsProject_HasNoProjectReferences_AndStaysLeaf` (GIVEN el XML de `src/SGV.Contracts/SGV.Contracts.csproj`; WHEN inspecciono los `ProjectReference`; THEN cero entradas). Patrón verbatim del design §11.1.
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~ErrorCategoriaTests` falla por símbolo ausente (`ErrorCategoria` no existe).
- **Rollback**: `rm tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs`.
- **Dependencies**: ninguna.
- **Estimación**: 30 ± 5 LoC (tests).
- **Commit guidance**: 1 commit `test(contracts): add ErrorCategoria shape and leaf invariant tests`.

### [x] T-1.2 — GREEN ErrorCategoria.cs
- **Slice**: 1
- **Files**: `src/SGV.Contracts/Comun/ErrorCategoria.cs` (new).
- **RED**: —
- **GREEN**: Crear `public enum ErrorCategoria { NotFound = 0, Conflict = 1, Validation = 2, Unauthorized = 3, Forbidden = 4, Transport = 5, Unexpected = 6 }` en namespace `SGV.Contracts.Comun`. XML doc con la matriz status→categoría y la regla append-only "NO reordenar ni reasignar ordinales" (verbatim design §2.1).
- **Verification**: `dotnet test --filter FullyQualifiedName~ErrorCategoriaTests` verde. `dotnet build src/SGV.Contracts/SGV.Contracts.csproj` sin warnings.
- **Rollback**: `rm src/SGV.Contracts/Comun/ErrorCategoria.cs` (los tests vuelven a fallar, esperado).
- **Dependencies**: T-1.1 mergeada.
- **Estimación**: 20 ± 3 LoC (producción).
- **Commit guidance**: 1 commit `feat(contracts): add ErrorCategoria enum with append-only ordinals`.

### [x] T-1.3 — RED ErrorCategoriaMappersTests (round-trip)
- **Slice**: 1
- **Files**: `tests/SGV.Tests/Contracts/ErrorCategoriaMappersTests.cs` (new).
- **RED**: Suite `[Theory]` parametrizada para los 6 enums:
  - `ToCategoria_RoundTripPreservesSemanticName` × 6 enums (cada valor de cada enum debe mapear al `ErrorCategoria` con el mismo nombre semántico cuando existe equivalente; `HabilidadErrorType.Infrastructure` → `Transport`; las 3 categorías sin equivalente en cada enum → `NotSupportedException` con mensaje claro).
  - `ToCategoria_CargoSkillValidation_MapsToValidationNotConflict` (regression explícito del ordinal invertido: ordinal `Validation = 1` en `CargoSkillErrorType` debe caer en `ErrorCategoria.Validation`, NO `ErrorCategoria.Conflict`).
  - `ToCategoria_UnknownEnumValue_ThrowsArgumentOutOfRange` para cada enum.
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~ErrorCategoriaMappersTests` falla por símbolo ausente (`ErrorCategoriaMappers` no existe).
- **Rollback**: `rm tests/SGV.Tests/Contracts/ErrorCategoriaMappersTests.cs`.
- **Dependencies**: T-1.2 mergeada.
- **Estimación**: 90 ± 10 LoC (tests; ~15 por enum × 6).
- **Commit guidance**: 1 commit `test(contracts): add ErrorCategoriaMappers round-trip tests for six enums`.

### [x] T-1.4 — GREEN ErrorCategoriaMappers.cs + [Obsolete] sobre 6 enums
- **Slice**: 1
- **Files**: `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs` (new); `src/SGV.Contracts/Habilidades/Comandos/HabilidadCommandResult.cs` (mod); `src/SGV.Contracts/Organizacion/Comandos/CargoCommandResult.cs` (mod); `src/SGV.Contracts/Organizacion/Comandos/PuestoCommandResult.cs` (mod); `src/SGV.Contracts/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` (mod); `src/SGV.Contracts/Organizacion/Comandos/CargoSkillCommandResult.cs` (mod); `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` (mod).
- **RED**: —
- **GREEN**: Crear `static class ErrorCategoriaMappers` con 12 métodos (`ToCategoria`/`ToTipo` × 6 enums), todos como `switch expression` exhaustivos sin `default:` (CS8524 como warning aceptable). Cada enums existente (`HabilidadErrorType`, `CargoErrorType`, `PuestoErrorType`, `UnidadOrganizativaErrorType`, `CargoSkillErrorType`, `UsuarioErrorType`) se marca `[Obsolete("Use SGV.Contracts.Comun.ErrorCategoria. Will be removed in the archive of change 2026-07-13.")]`. **Los ordinales de `CargoSkillErrorType` NO se tocan** (regla append-only explícita en el design §2.1, F1).
- **REFACTOR**: ninguno en este commit; las matrices se ajustan en Slice 4 vía `ApiResults.MapCategoria`.
- **Verification**: `dotnet test --filter FullyQualifiedName~ErrorCategoriaMappersTests` verde. `dotnet build SGV.slnx` sin warnings nuevos de `CS0618` en código de producción (los tests pueden usar los `[Obsolete]` con `pragma warning disable CS0618`).
- **Rollback**: `git revert -n <commit-sha>`; los enums vuelven a no estar `[Obsolete]` y `ErrorCategoriaMappers.cs` desaparece.
- **Dependencies**: T-1.3 mergeada.
- **Estimación**: 80 ± 10 LoC producción (`ErrorCategoriaMappers.cs` ~60 + 6 atributos `[Obsolete]` ~20).
- **Commit guidance**: 2 commits:
  1. `feat(contracts): add ErrorCategoriaMappers with six enum round-trips`
  2. `chore(contracts): mark six ErrorType enums as obsolete pending archive`

### [x] T-1.5 — RED contract tests: 6 `*Error.Categoria` + 5 `*DeleteResult.Categoria/StatusCode?`
- **Slice**: 1
- **Files**: `tests/SGV.Tests/Contracts/ErrorRecordContractTests.cs` (new); `tests/SGV.Tests/Contracts/DeleteResults/HabilidadDeleteResultContractTests.cs` (new); `tests/SGV.Tests/Contracts/DeleteResults/CargoDeleteResultContractTests.cs` (new); `tests/SGV.Tests/Contracts/DeleteResults/PuestoDeleteResultContractTests.cs` (new); `tests/SGV.Tests/Contracts/DeleteResults/UnidadOrganizativaDeleteResultContractTests.cs` (new); `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillDeleteResultContractTests.cs` (mod — extensión in-place).
- **RED**:
  - 6 tests `*Error_ExposesCategoriaOfTypeErrorCategoria`: construyen cada record (HabilidadError, CargoError, PuestoError, UnidadOrganizativaError, CargoSkillError, UsuarioError) con `Categoria = X` y assertean el round-trip.
  - 5 tests `*DeleteResult_ExposesCategoriaAndNullableStatusCode`: por cada `*DeleteResult`, assertean `Categoria: ErrorCategoria`, `StatusCode: HttpStatusCode?`, `Succeeded == true → Categoria == default`, `Succeeded == false → Categoria` poblada según el caso. Para `PuestoDeleteResult`: `StatusCode` pasa de `HttpStatusCode` non-nullable a `HttpStatusCode?`.
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~ErrorRecordContractTests|FullyQualifiedName~DeleteResultContractTests` falla por propiedades ausentes.
- **Rollback**: `rm tests/SGV.Tests/Contracts/ErrorRecordContractTests.cs tests/SGV.Tests/Contracts/DeleteResults/*.cs`; `git checkout HEAD~ -- tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillDeleteResultContractTests.cs`.
- **Dependencies**: T-1.4 mergeada.
- **Estimación**: 110 ± 10 LoC tests (6 `*Error` + 5 `*DeleteResult` × ~10 LoC cada uno).
- **Commit guidance**: 1 commit `test(contracts): add Error and DeleteResult contract tests for Categoria and nullable StatusCode`.

### [x] T-1.6 — GREEN agregar `Categoria` a 6 `*Error` + `Categoria/StatusCode?` a 5 `*DeleteResult`
- **Slice**: 1
- **Files**: los 6 `*CommandResult.cs` (mod); `src/SGV.Contracts/Organizacion/Comandos/CargoSkillDeleteResult.cs` (mod); `src/SGV.Web/Integration/Habilidades/HabilidadListItemViewModel.cs` (mod — `HabilidadDeleteResult`); `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs` (mod — `CargoDeleteResult`); `src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs` (mod — `PuestoDeleteResult` con `StatusCode` non-nullable → nullable); `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs` (mod — `UnidadOrganizativaDeleteResult`).
- **RED**: —
- **GREEN**: 
  - Para los 6 `*Error` records: agregar `Categoria: ErrorCategoria = ErrorCategoria.Unexpected` como último parámetro posicional (default `Unexpected` mantiene source-compat). Para los 5 records que no exponen `StatusCode`, agregar opcionalmente `int? StatusCode = null` (HabilidadError ya lo tiene).
  - Para los 5 `*DeleteResult`: agregar `Categoria: ErrorCategoria` (posicional, requerido) y verificar que `StatusCode` es `HttpStatusCode?` (nullable) — `PuestoDeleteResult` pasa de `HttpStatusCode` a `HttpStatusCode?` (cambio source-compatible para la mayoría de los call sites; `Puestos/Index.cshtml.cs` y `PuestosApiClient.cs` se ajustan en este commit cuando se detecte incompatibilidad).
- **Verification**: `dotnet test --filter FullyQualifiedName~ErrorRecordContractTests|FullyQualifiedName~DeleteResultContractTests` verde. `dotnet build SGV.slnx` sin warnings nuevos.
- **Rollback**: `git revert <commit-sha>`; los records vuelven a su shape previo.
- **Dependencies**: T-1.5 mergeada.
- **Estimación**: 50 ± 10 LoC producción (6 records + 5 DeleteResult, mayormente anotaciones de tipo).
- **Commit guidance**: 1 commit `feat(contracts): add Categoria to Error records and DeleteResult contracts`. Si la línea de PuestoDeleteResult non-nullable→nullable rompe demasiados call sites en compilación, dividir en 2 commits: (a) los 4 `*DeleteResult` que ya tenían `HttpStatusCode?`; (b) `PuestoDeleteResult` + ajustes en `Puestos/Index.cshtml.cs` y `PuestosApiClient.cs`.

### [x] T-1.7 — Documentar taxonomía en `docs/decisiones-implementacion.md`
- **Slice**: 1
- **Files**: `docs/decisiones-implementacion.md` (mod — entrada nueva).
- **RED**: —
- **GREEN**: Agregar sección "Issue #125 — Taxonomía de errores para `CommandResult` y clientes HTTP de Web" que documente: rationale del enum común, regla append-only, mapeo nombre-a-nombre vía `ErrorCategoriaMappers`, política de `[Obsolete]` durante el ciclo del change, y la eliminación al archivar. Cita los archivos clave (`src/SGV.Contracts/Comun/ErrorCategoria.cs`, `ErrorCategoriaMappers.cs`).
- **Verification**: lectura manual; `dotnet build` no afectado.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-1.6 mergeada.
- **Estimación**: 25 ± 5 LoC doc.
- **Commit guidance**: 1 commit `docs: register Issue #125 error taxonomy decision in decisiones-implementacion`.

### Verificación final Slice 1

```bash
dotnet build SGV.slnx                          # sin warnings nuevos
dotnet test --filter FullyQualifiedName~ErrorCategoria
dotnet test --filter FullyQualifiedName~ErrorCategoriaMappers
dotnet test --filter FullyQualifiedName~ErrorRecordContract
dotnet test --filter FullyQualifiedName~DeleteResultContract
dotnet test SGV.slnx                           # suite completa verde
```

**Commit/PR guidance Slice 1**: 1 PR `fix/125-s1-contracts-error-taxonomy`. Branch desde develop. PR target: develop. **Estimación**: ~350 LoC (producción + tests + 1 doc, alineado con design §12).

## Fase 2 — Slice 2: `CommandResultMapper` único + 5 clientes Web + cancel/timeout/DNS

### T-2.1 — RED `TransportFailureClassifier.IsDnsFailure` tests
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/Common/TransportFailureClassifierTests.cs` (mod).
- **RED**: 3 tests: `IsDnsFailure_NameResolutionFailure_ReturnsTrue` (GIVEN `HttpRequestException` cuya `InnerException` es `SocketException((int)SocketError.NameResolutionFailure)`; WHEN `IsDnsFailure(ex)`; THEN `true`). `IsDnsFailure_NonSocketInner_ReturnsFalse` (GIVEN `HttpRequestException` con `InnerException = InvalidOperationException`; WHEN `IsDnsFailure(ex)`; THEN `false`). `IsDnsFailure_NullInner_ReturnsFalse` (GIVEN `HttpRequestException` sin `InnerException`; WHEN `IsDnsFailure(ex)`; THEN `false`).
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~TransportFailureClassifierTests.IsDnsFailure` falla por símbolo ausente.
- **Rollback**: `git checkout HEAD~ -- tests/SGV.Tests/Web/Common/TransportFailureClassifierTests.cs`.
- **Dependencies**: T-1.7 mergeada.
- **Estimación**: 35 ± 5 LoC tests.
- **Commit guidance**: 1 commit `test(web): add IsDnsFailure detection for HttpRequestException inner SocketException`.

### T-2.2 — GREEN `IsDnsFailure` en `TransportFailureClassifier`
- **Slice**: 2
- **Files**: `src/SGV.Web/Integration/Common/TransportFailureClassifier.cs` (mod).
- **RED**: —
- **GREEN**: Agregar método público estático `bool IsDnsFailure(HttpRequestException exception)` con guard `ArgumentNullException.ThrowIfNull`. Implementación: `exception.InnerException is SocketException se && se.SocketErrorCode == SocketError.NameResolutionFailure`. XML doc explicando la semántica verbatim del design §5.3.
- **Verification**: `dotnet test --filter FullyQualifiedName~TransportFailureClassifierTests.IsDnsFailure` verde. `dotnet build SGV.slnx` sin warnings.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-2.1 mergeada.
- **Estimación**: 10 ± 2 LoC producción.
- **Commit guidance**: 1 commit `feat(web): add IsDnsFailure detection to TransportFailureClassifier`.

### T-2.3 — RED extender `HttpClientExceptionScenarios.TransportExceptionData` con fila DNS
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` (mod).
- **RED**: Test RED indirecto: crear 5 tests parametrizados (uno por cliente principal) que consuman la fila `DnsFailure` cuando exista. El test compila pero falla por ausencia de la fila en `TransportExceptionData`. Patrón `*ApiClient_*Method_DnsFailureScenario_PropagatesHttpRequestException` (ver T-2.7/9/11/13).
- **GREEN**: —
- **Verification**: tests RED específicos de T-2.7/9/11/13 fallan por símbolo ausente en el dataset.
- **Rollback**: `git checkout HEAD~ -- tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs`.
- **Dependencies**: T-2.2 mergeada.
- **Estimación**: 5 ± 1 LoC (1 fila nueva).
- **Commit guidance**: 1 commit `test(web): add DnsFailure scenario row to HttpClientExceptionScenarios`. Se fusiona con T-2.4 si se mantiene <50 LoC.

### T-2.4 — GREEN agregar fila DNS al dataset
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` (mod).
- **RED**: —
- **GREEN**: Agregar fila `["DnsFailure", () => new HttpRequestException("name resolution", new SocketException((int)SocketError.NameResolutionFailure)), typeof(HttpRequestException)]` al `TransportExceptionData`. Comentario XML justificando que la fila es la única que cubre el camino `InnerException = SocketException(NameResolutionFailure)` y habilita los tests RED parametrizados de los 5 clientes.
- **Verification**: tests RED específicos de T-2.7/9/11/13 pasan al invocar el helper.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-2.3 mergeada.
- **Estimación**: 5 ± 1 LoC (1 fila + comentario XML).
- **Commit guidance**: fusionar con T-2.3 si total <50 LoC; si no, separar.

### T-2.5 — RED `CommandResultMapperTests` matriz completa + status atípicos
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/Common/CommandResultMapperTests.cs` (new).
- **RED**: `[Theory]` parametrizada con `InlineData` cubriendo cada fila de la matriz REQ-2 + 5 status atípicos (300, 418, 999, 226, 507). Cada fila: `[(HttpStatusCode status, ErrorCategoria expectedCategoria, string expectedCodeFragment, string expectedMessageFragment), ...]`. Mínimo 18 InlineData (status matriz REQ-2 explícita: 200/201/204 fuera, 400, 400-con-FieldErrors, 401, 403, 404, 408, 409, 422, 500, 502, 503, 504 + atípicos 300/418/999/226/507). Tests separados para casos con/sin `FieldErrors`. Para cada caso, instancia `ApiProblemReader.Result` con `Title`/`Detail` poblados o null según se prueba la rama de defaults.
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~CommandResultMapperTests` falla por símbolo ausente.
- **Rollback**: `rm tests/SGV.Tests/Web/Common/CommandResultMapperTests.cs`.
- **Dependencies**: T-2.4 mergeada.
- **Estimación**: 110 ± 10 LoC tests.
- **Commit guidance**: 1 commit `test(web): add CommandResultMapper tests covering full HTTP matrix and atypical statuses`.

### T-2.6 — GREEN `CommandResultMapper.cs`
- **Slice**: 2
- **Files**: `src/SGV.Web/Integration/Common/CommandResultMapper.cs` (new).
- **RED**: —
- **GREEN**: Crear `public static class CommandResultMapper` con método `public static (ErrorCategoria Categoria, string Code, string Message, int? StatusCode) Map(HttpResponseMessage response, ApiProblemReader.Result problem)`. Implementación: `switch` sobre `(int)response.StatusCode` exhaustivo SIN `default:` (CS8524 warning aceptable); filas verbatim del design §5.4. Defaults en español congruentes con la UI vigente ("Intentá nuevamente", "Su sesión expiró", "Acceso denegado"). NO acepta excepciones nativas — sólo `HttpResponseMessage`.
- **Verification**: `dotnet test --filter FullyQualifiedName~CommandResultMapperTests` verde.
- **Rollback**: `rm src/SGV.Web/Integration/Common/CommandResultMapper.cs`.
- **Dependencies**: T-2.5 mergeada.
- **Estimación**: 70 ± 10 LoC producción.
- **Commit guidance**: 1 commit `feat(web): add CommandResultMapper with full HTTP status matrix`.

### T-2.7 — RED tests de HabilidadApiClient: 401/403/5xx/408/DNS/cancel/timeout
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` (mod).
- **RED**: ~7 nuevos tests/InlineData:
  - `[Theory] CreateAsync_Http[401|403|408|500|502|503]_ReturnsFailureWithCategoria[Unauthorized|Forbidden|Transport|...]` (parametrizado). GIVEN `RecordingHandler` que responde status X con ProblemDetails; WHEN `CreateAsync`; THEN `result.Error!.Categoria == ErrorCategoria.X`.
  - `*ApiClient_CreateAsync_PreCanceledToken_PropagatesOperationCanceledException` (token pre-cancelado).
  - `*ApiClient_CreateAsync_HttpClientTimeoutVsCallerCancellation_PropagatesDistinctExceptions` (timeout interno via RecordingHandler que duerme > client.Timeout vs cancelación externa via CancellationToken).
  - `*ApiClient_CreateAsync_DnsFailureScenario_PropagatesHttpRequestException` (reusa la fila DnsFailure de T-2.4).
- **GREEN**: —
- **Verification**: tests nuevos fallan porque `HabilidadApiClient` actual sólo ramifica en 400/404/409.
- **Rollback**: `git checkout HEAD~ -- tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs`.
- **Dependencies**: T-2.6 mergeada.
- **Estimación**: 90 ± 10 LoC tests.
- **Commit guidance**: 1 commit `test(web): extend HabilidadApiClientTests with 401/403/5xx/DNS/cancel/timeout coverage`.

### T-2.8 — GREEN migrar `HabilidadApiClient.ToCommandResultAsync` a `CommandResultMapper`
- **Slice**: 2
- **Files**: `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` (mod).
- **RED**: —
- **GREEN**: Reemplazar el cuerpo de `ToCommandResultAsync` (líneas ~208-255 actuales) por una llamada a `CommandResultMapper.Map(response, parsed)`. El método ahora retorna `HabilidadCommandResult` con `Categoria` poblado por el mapper. El log de status inesperado sólo se ejecuta cuando `Categoria == ErrorCategoria.Unexpected` o `Transport` (mantener la observabilidad actual). El parámetro `HabilidadErrorType` se mantiene con `[Obsolete]` durante el ciclo (design §2.4) y se obtiene vía `ErrorCategoriaMappers.ToTipo(categoria)` cuando se necesita.
- **REFACTOR**: extraer la creación de `HabilidadError` a una pequeña helper local `BuildError(HabilidadErrorType type, ErrorCategoria categoria, string code, string message, int? statusCode)` para no duplicar 5 veces.
- **Verification**: `dotnet test --filter FullyQualifiedName~HabilidadApiClientTests` verde. `dotnet build SGV.slnx` sin warnings nuevos.
- **Rollback**: `git revert <commit-sha>`; el cliente vuelve a su matriz privada.
- **Dependencies**: T-2.7 mergeada.
- **Estimación**: 60 ± 10 LoC producción (sustituye ~50 líneas, agrega ~10 de glue).
- **Commit guidance**: 1 commit `refactor(web): migrate HabilidadApiClient to CommandResultMapper`.

### T-2.9 — RED tests de CargoApiClient + CargoSkillApiClient: 401/403/5xx/408/DNS/cancel/timeout
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/Cargo/CargoApiClientBasicTests.cs` (mod); `tests/SGV.Tests/Web/Cargo/CargoSkillApiClientTests.cs` (mod).
- **RED**: ~10 nuevos tests/InlineData repartidos entre los dos archivos:
  - `[Theory] CreateAsync_Http[401|403|408|500|502|503]_ReturnsFailureWithCategoria[Unauthorized|Forbidden|Transport|...]` en `CargoApiClientBasicTests`.
  - Tests análogos para `UpdateAsync`, `ReactivateAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`.
  - `*ApiClient_*Method_PreCanceledToken_PropagatesOperationCanceledException` y `*ApiClient_*Method_HttpClientTimeoutVsCallerCancellation_PropagatesDistinctExceptions` para los métodos `CreateAsync`, `UpdateAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`.
  - `*ApiClient_*Method_DnsFailureScenario_PropagatesHttpRequestException`.
- **GREEN**: —
- **Verification**: tests nuevos fallan porque el cliente actual colapsa 401/403/5xx en `Validation/Unexpected`.
- **Rollback**: `git checkout HEAD~ -- tests/SGV.Tests/Web/Cargo/CargoApiClientBasicTests.cs tests/SGV.Tests/Web/Cargo/CargoSkillApiClientTests.cs`.
- **Dependencies**: T-2.8 mergeada.
- **Estimación**: 140 ± 15 LoC tests (entre los dos archivos).
- **Commit guidance**: 1 commit `test(web): extend CargoApiClient and CargoSkillApiClient tests with full HTTP matrix and transport scenarios`.

### T-2.10 — GREEN migrar CargoApiClient (incl. CargoSkill) a `CommandResultMapper`
- **Slice**: 2
- **Files**: `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` (mod — elimina `MapSkillError` y `ReadSkillProblemAsync`).
- **RED**: —
- **GREEN**: 
  - Reemplazar `ToCommandResultAsync` por delegación a `CommandResultMapper.Map(response, parsed)`.
  - Reemplazar `ToSkillCommandResultAsync` por el mismo mapper, construyendo `CargoSkillError` con `Categoria` poblado y `CargoSkillErrorType` derivado vía `ErrorCategoriaMappers.ToTipo(categoria)` (mantener `[Obsolete]` durante el ciclo).
  - **Eliminar** `MapSkillError` (matriz privada) y `ReadSkillProblemAsync` (helper de parseo redundante con `ApiProblemReader`).
  - `UpsertSkillAsync` rama "EmptyBody" (línea ~178-185 actual) se mantiene — sigue siendo lógica específica del subrecurso, no del mapper.
- **REFACTOR**: el código eliminado deja dos métodos privados menos; documentar en XML doc que el cliente ya no mantiene matriz privada.
- **Verification**: `dotnet test --filter FullyQualifiedName~CargoApiClientBasic|FullyQualifiedName~CargoSkillApiClientTests` verde. `dotnet build SGV.slnx` sin warnings nuevos.
- **Rollback**: `git revert <commit-sha>`; el cliente vuelve a su estado previo con `MapSkillError`/`ReadSkillProblemAsync`.
- **Dependencies**: T-2.9 mergeada.
- **Estimación**: 80 ± 15 LoC producción (elimina ~60 líneas de matrices privadas, agrega ~20 de glue).
- **Commit guidance**: 1 commit `refactor(web): migrate CargoApiClient and CargoSkill to CommandResultMapper`.

### T-2.11 — RED tests de PuestosApiClient: 401/403/5xx/408/DNS/cancel/timeout
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs` (mod).
- **RED**: ~7 nuevos tests/InlineData análogos a T-2.7 para `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ReactivateAsync`. Incluyen `[Theory]` para 401/403/408/500/502/503, `PreCanceledToken_PropagatesOperationCanceledException`, `HttpClientTimeoutVsCallerCancellation_PropagatesDistinctExceptions`, `DnsFailureScenario_PropagatesHttpRequestException`.
- **GREEN**: —
- **Verification**: tests nuevos fallan porque el cliente actual colapsa 401/403/5xx en `Validation/Unexpected`.
- **Rollback**: `git checkout HEAD~ -- tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs`.
- **Dependencies**: T-2.10 mergeada.
- **Estimación**: 90 ± 10 LoC tests.
- **Commit guidance**: 1 commit `test(web): extend PuestosApiClientTests with 401/403/5xx/DNS/cancel/timeout coverage`.

### T-2.12 — GREEN migrar `PuestosApiClient.ToCommandResultAsync` a `CommandResultMapper`
- **Slice**: 2
- **Files**: `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs` (mod).
- **RED**: —
- **GREEN**: Reemplazar el cuerpo de `ToCommandResultAsync` (líneas ~110-141 actuales) por delegación a `CommandResultMapper.Map(response, parsed)`. El método ahora retorna `PuestoCommandResult` con `Categoria` poblado. El `StatusCode` no-nullable de `PuestoDeleteResult` se ajusta a nullable en este commit cuando la compilación lo exija (cambio source-compatible para la mayoría de los call sites).
- **REFACTOR**: ninguno.
- **Verification**: `dotnet test --filter FullyQualifiedName~PuestosApiClientTests` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-2.11 mergeada.
- **Estimación**: 40 ± 5 LoC producción (sustituye ~30 líneas, agrega ~10 de glue).
- **Commit guidance**: 1 commit `refactor(web): migrate PuestosApiClient to CommandResultMapper`.

### T-2.13 — RED tests de UnidadOrganizativaApiClient: 401/403/5xx/408/DNS/cancel/timeout
- **Slice**: 2
- **Files**: `tests/SGV.Tests/Web/UnidadOrganizativa/UnidadOrganizativaApiClientTests.cs` (mod).
- **RED**: ~7 nuevos tests/InlineData análogos a T-2.7 para `CreateAsync`, `UpdateAsync`, `ChangeParentAsync`, `DeleteAsync`, `ReactivateAsync`. Incluyen `[Theory]` para 401/403/408/500/502/503, `PreCanceledToken`, `TimeoutVsCancellation`, `DnsFailureScenario`.
- **GREEN**: —
- **Verification**: tests nuevos fallan porque el cliente actual colapsa 401/403/5xx en `Validation/Unexpected`.
- **Rollback**: `git checkout HEAD~ -- tests/SGV.Tests/Web/UnidadOrganizativa/UnidadOrganizativaApiClientTests.cs`.
- **Dependencies**: T-2.12 mergeada.
- **Estimación**: 90 ± 10 LoC tests.
- **Commit guidance**: 1 commit `test(web): extend UnidadOrganizativaApiClientTests with 401/403/5xx/DNS/cancel/timeout coverage`.

### T-2.14 — GREEN migrar `UnidadOrganizativaApiClient.ToCommandResultAsync` a `CommandResultMapper`
- **Slice**: 2
- **Files**: `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` (mod).
- **RED**: —
- **GREEN**: Reemplazar el cuerpo de `ToCommandResultAsync` (líneas ~169-208 actuales) por delegación a `CommandResultMapper.Map(response, parsed)`. El método ahora retorna `UnidadOrganizativaCommandResult` con `Categoria` poblado.
- **REFACTOR**: ninguno.
- **Verification**: `dotnet test --filter FullyQualifiedName~UnidadOrganizativaApiClientTests` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-2.13 mergeada.
- **Estimación**: 40 ± 5 LoC producción.
- **Commit guidance**: 1 commit `refactor(web): migrate UnidadOrganizativaApiClient to CommandResultMapper`.

### Verificación final Slice 2

```bash
dotnet build SGV.slnx                                                                  # sin warnings nuevos
dotnet test --filter FullyQualifiedName~CommandResultMapper
dotnet test --filter FullyQualifiedName~TransportFailureClassifier
dotnet test --filter FullyQualifiedName~HabilidadApiClientTests
dotnet test --filter FullyQualifiedName~CargoApiClientBasic
dotnet test --filter FullyQualifiedName~CargoSkillApiClientTests
dotnet test --filter FullyQualifiedName~PuestosApiClientTests
dotnet test --filter FullyQualifiedName~UnidadOrganizativaApiClientTests
dotnet test SGV.slnx                                                                   # suite completa verde
```

**Compromiso de transporte (verificación adicional)**: ningún test nuevo de Slice 2 convierte `HttpRequestException`/`TaskCanceledException` a `Categoria.Transport`. La excepción nativa se sigue propagando. `dotnet test --filter FullyQualifiedName~CommandResultMapper` cubre sólo `HttpResponseMessage`, no excepciones.

**Commit/PR guidance Slice 2**: 1 PR `fix/125-s2-mapper-clients`. Branch desde develop (rebase después de merge de Slice 1). PR target: develop. **Estimación**: ~750 LoC (producción + tests, +120 sobre design §12; ver §Divergencia de forecast).

## Fase 3 — Slice 3: `IAuthSessionRedirector` + 14 PageModels exhaustivos

### [x] T-3.1 — RED `AuthSessionRedirectorTests` (6 casos)
- **Slice**: 3
- **Files**: `tests/SGV.Tests/Web/Common/AuthSessionRedirectorTests.cs` (new).
- **RED**: 6 tests (uno por caso del design §11.3): `TryRedirectToLogin_NoHttpContext_ReturnsNull`, `TryRedirectToLogin_WithLocalPath_PreservesReturnUrl`, `TryRedirectToLogin_WithAbsoluteExternalUrl_DropsReturnUrl_RedirectsToLogin` (F9), `TryRedirectToLogin_WithProtocolRelativeUrl_DropsReturnUrl_RedirectsToLogin` (F9), `TryRedirectToLogin_WithLoopbackAbsoluteUrl_PreservesReturnUrl`, `TryRedirectToLogin_EmptyPath_OmitsReturnUrl`. Cada test instancia un `DefaultHttpContext` con `Request.Host = "localhost"`, inyecta un `IUrlHelperFactory` falso que devuelve paths predecibles, y assertea el `RedirectResult` emitido.
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~AuthSessionRedirectorTests` falla por símbolo ausente.
- **Rollback**: `rm tests/SGV.Tests/Web/Common/AuthSessionRedirectorTests.cs`.
- **Dependencies**: T-2.14 mergeada.
- **Estimación**: 100 ± 15 LoC tests (6 casos × ~15 LoC + setup).
- **Commit guidance**: 1 commit `test(web): add AuthSessionRedirector tests for local/absolute/protocol-relative cases`.

### [x] T-3.2 — GREEN `IAuthSessionRedirector` + `AuthSessionRedirector`
- **Slice**: 3
- **Files**: `src/SGV.Web/Integration/Common/IAuthSessionRedirector.cs` (new); `src/SGV.Web/Integration/Common/AuthSessionRedirector.cs` (new).
- **RED**: —
- **GREEN**: Crear interfaz pública `IAuthSessionRedirector.TryRedirectToLogin(string? returnUrl = null)` con XML doc verbatim del design §6.1. Implementación `internal sealed class AuthSessionRedirector(IHttpContextAccessor, IUrlHelperFactory)` con guard `IsLocalUrl` que rechaza URLs absolutas externas y protocol-relative `//host/path`. Si `returnUrl` no es local, se omite el query string para mitigar open-redirect. Si no hay `HttpContext`, devuelve `null` (tests sin host).
- **REFACTOR**: ninguno.
- **Verification**: `dotnet test --filter FullyQualifiedName~AuthSessionRedirectorTests` verde.
- **Rollback**: `rm src/SGV.Web/Integration/Common/IAuthSessionRedirector.cs src/SGV.Web/Integration/Common/AuthSessionRedirector.cs`.
- **Dependencies**: T-3.1 mergeada.
- **Estimación**: 50 ± 10 LoC producción.
- **Commit guidance**: 1 commit `feat(web): add IAuthSessionRedirector with open-redirect guard`.

### [x] T-3.3 — GREEN registrar `IAuthSessionRedirector` + `IUrlHelperFactory` en DI
- **Slice**: 3
- **Files**: `src/SGV.Web/Program.cs` (mod — agregar 2 líneas de registro scoped).
- **RED**: —
- **GREEN**: Agregar después del bloque `AddHttpContextAccessor()`: `builder.Services.AddScoped<IUrlHelperFactory, UrlHelperFactory>();` y `builder.Services.AddScoped<IAuthSessionRedirector, AuthSessionRedirector>();`. Verificar que `AddHttpContextAccessor` ya está presente (lo está, línea 53 del Program.cs actual).
- **REFACTOR**: ninguno.
- **Verification**: `dotnet build SGV.slnx` sin warnings. Test smoke: `dotnet test --filter SgvWebApplicationFactory` (los factories de tests deben seguir booteando).
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-3.2 mergeada.
- **Estimación**: 3 ± 1 LoC producción.
- **Commit guidance**: 1 commit `chore(web): register IAuthSessionRedirector and IUrlHelperFactory in DI`.

### [x] T-3.4 — RED PageModel exhaustivity tests infra + 14 smoke tests parametrizados
- **Slice**: 3
- **Files**: `tests/SGV.Tests/Web/PageModelExhaustivityTests.cs` (new).
- **RED**: 
  - Helper estático `PageModelExhaustivity.AssertCoversAllCategorias(Func<ErrorCategoria, IActionResult> switchArm)` que itera `Enum.GetValues<ErrorCategoria>()` y assertea que cada variante retorna un `IActionResult` no-null (sin `SwitchExpressionException` ni default silencioso).
  - 14 `[Fact]` smoke tests, uno por PageModel: `Habilidades_CreateModel_CoversAllCategorias`, `Habilidades_EditModel_CoversAllCategorias`, `Habilidades_IndexModel_CoversAllCategorias`, `Cargos_IndexModel_CoversAllCategorias`, `Cargos_CreateModel_CoversAllCategorias`, `Cargos_EditModel_CoversAllCategorias`, `Cargos_HabilidadesModel_CoversAllCategorias`, `Puestos_IndexModel_CoversAllCategorias`, `Puestos_CreateModel_CoversAllCategorias`, `Puestos_EditModel_CoversAllCategorias`, `UnidadesOrganizativas_IndexModel_CoversAllCategorias`, `UnidadesOrganizativas_CreateModel_CoversAllCategorias`, `UnidadesOrganizativas_EditModel_CoversAllCategorias`, `UnidadesOrganizativas_DetailsModel_CoversAllCategorias`. Cada test invoca el `OnPost` correspondiente (con mocks de cliente y `IAuthSessionRedirector`) cubriendo las 7 variantes.
  - GIVEN: el PageModel bajo prueba con mocks; WHEN itero las 7 `ErrorCategoria` vía el switch; THEN cada variante retorna un `IActionResult` (no `null`, no `SwitchExpressionException`).
- **GREEN**: —
- **Verification**: tests fallan porque los PageModels actuales ramifican por `*ErrorType` y NO cubren todas las `ErrorCategoria`.
- **Rollback**: `rm tests/SGV.Tests/Web/PageModelExhaustivityTests.cs`.
- **Dependencies**: T-3.3 mergeada.
- **Estimación**: 220 ± 20 LoC tests (helper + 14 tests parametrizados).
- **Commit guidance**: 1 commit `test(web): add exhaustivity coverage for 14 PageModels against seven ErrorCategoria`. Si >400 LoC, dividir en (a) helper + smoke Habilidades + Cargos; (b) smoke Puestos + UnidadesOrganizativas.

### [x] T-3.5 — GREEN migrar Habilidades/Create + Habilidades/Edit (los más simples)
- **Slice**: 3
- **Files**: `src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml.cs` (mod).
- **RED**: —
- **GREEN**: 
  - Inyectar `IAuthSessionRedirector authRedirector` por constructor primario.
  - Reemplazar el switch sobre `HabilidadErrorType` por un switch exhaustivo sobre `ErrorCategoria` SIN `default:` (CS8524 warning aceptable). Cada rama retorna un mensaje coherente con la copia canónica (`PageFeedback.TransportMessage`, `ForbiddenMessage`, etc.).
  - Cuando `Categoria == ErrorCategoria.Unauthorized`, invocar `authRedirector.TryRedirectToLogin(Request.Path)` antes de mostrar mensaje inline.
  - Reemplazar el filtro manual `catch (Exception ex) when (...)` (líneas 69-79 de Create) por `catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))` para usar el helper centralizado. Mantener `OperationCanceledException` propagation per design §8.4.
- **REFACTOR**: extraer la creación del switch a una helper estática para no duplicarla entre Create/Edit.
- **Verification**: `dotnet test --filter FullyQualifiedName~HabilidadCreatePage|FullyQualifiedName~HabilidadEditPage|FullyQualifiedName~PageModelExhaustivityTests.Habilidades_Create|FullyQualifiedName~PageModelExhaustivityTests.Habilidades_Edit` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-3.4 mergeada.
- **Estimación**: 80 ± 15 LoC producción (2 PageModels).
- **Commit guidance**: 1 commit `refactor(web): migrate Habilidades Create and Edit to Categoria-based exhaustivity`.

### [x] T-3.6 — GREEN migrar Habilidades/Index (reactivar + delete)
- **Slice**: 3
- **Files**: `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` (mod).
- **RED**: —
- **GREEN**: 
  - Inyectar `IAuthSessionRedirector`.
  - Migrar el switch sobre `HabilidadErrorType` (rama `OnPostReactivarAsync`) y sobre `HabilidadDeleteResult.StatusCode` (rama `OnPostDeleteAsync`) a switches sobre `Categoria` y `DeleteResult.Categoria`.
  - Cuando `Categoria == ErrorCategoria.Unauthorized`, invocar `authRedirector.TryRedirectToLogin(Request.Path)`.
  - Usar `PageFeedback` constants para la copia canónica.
- **REFACTOR**: ninguno.
- **Verification**: `dotnet test --filter FullyQualifiedName~HabilidadIndexPage|FullyQualifiedName~PageModelExhaustivityTests.Habilidades_Index` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-3.5 mergeada.
- **Estimación**: 50 ± 10 LoC producción.
- **Commit guidance**: 1 commit `refactor(web): migrate Habilidades Index to Categoria-based exhaustivity`.

### [x] T-3.7 — GREEN migrar Cargos/{Index,Create,Edit,Habilidades} (4 PageModels)
- **Slice**: 3
- **Files**: `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` (mod).
- **RED**: —
- **GREEN**: Patrón análogo a T-3.5/6:
  - Inyectar `IAuthSessionRedirector` en cada constructor primario.
  - Reemplazar switches sobre `CargoErrorType` por switches sobre `ErrorCategoria` exhaustivos.
  - `OnPostDeleteAsync`/`OnPostReactivateAsync` en Index migran de `result.StatusCode == HttpStatusCode.Conflict` a `result.Categoria == ErrorCategoria.Conflict` (y análogos para NotFound/Transport/Unexpected).
  - Reemplazar `catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))` por la versión centralizada (ya está en uso; verificar que se mantiene). Eliminar filtros manuales redundantes.
  - En `Habilidades.cshtml.cs`: eliminar el `IsTransportFailure` privado (citado en exploration §3) y reemplazar por `TransportFailureClassifier.IsTransportFailure(ex)`.
- **REFACTOR**: consolidar el switch de Delete/Reactivate en `Index.cshtml.cs` (líneas 158-167 y 203-208 actuales) a una helper estática `BuildDeleteFailureMessage(DeleteResult)`.
- **Verification**: `dotnet test --filter FullyQualifiedName~CargosIndexPage|FullyQualifiedName~CargoCreatePage|FullyQualifiedName~CargoEditPage|FullyQualifiedName~CargoHabilidadesPage|FullyQualifiedName~PageModelExhaustivityTests.Cargos_` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-3.6 mergeada.
- **Estimación**: 160 ± 25 LoC producción (4 PageModels × ~40 LoC cada uno).
- **Commit guidance**: 1 commit `refactor(web): migrate four Cargos PageModels to Categoria-based exhaustivity`. Si >400 LoC, dividir: (a) Index + Create + Edit; (b) Habilidades + cleanup de IsTransportFailure privado.

### [x] T-3.8 — GREEN migrar Puestos/{Index,Create,Edit} (3 PageModels)
- **Slice**: 3
- **Files**: `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` (mod).
- **RED**: —
- **GREEN**: Patrón análogo a T-3.7. Inyectar `IAuthSessionRedirector` en cada uno. Reemplazar switches sobre `PuestoErrorType` y comparaciones de `PuestoDeleteResult.StatusCode` por switches sobre `ErrorCategoria` y `DeleteResult.Categoria`.
- **REFACTOR**: ninguno específico.
- **Verification**: `dotnet test --filter FullyQualifiedName~PuestosIndexPage|FullyQualifiedName~PuestoCreatePage|FullyQualifiedName~PuestoEditPage|FullyQualifiedName~PageModelExhaustivityTests.Puestos_` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-3.7 mergeada.
- **Estimación**: 120 ± 20 LoC producción (3 PageModels × ~40 LoC).
- **Commit guidance**: 1 commit `refactor(web): migrate three Puestos PageModels to Categoria-based exhaustivity`.

### [x] T-3.9 — GREEN migrar UnidadesOrganizativas/{Index,Create,Edit,Details} (4 PageModels)
- **Slice**: 3
- **Files**: `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` (mod); `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml.cs` (mod).
- **RED**: —
- **GREEN**: Patrón análogo a T-3.7/8. Inyectar `IAuthSessionRedirector` en cada uno. Reemplazar switches sobre `UnidadOrganizativaErrorType` por switches sobre `ErrorCategoria`. Eliminar el filtro manual de `Edit.cshtml.cs` que captura `OperationCanceledException` (citado en exploration §3) y reemplazarlo por el patrón centralizado `catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex, includeOperationCanceled: true))` con opt-in explícito.
- **REFACTOR**: ninguno específico.
- **Verification**: `dotnet test --filter FullyQualifiedName~UnidadOrganizativaIndexPage|FullyQualifiedName~UnidadOrganizativaCreatePage|FullyQualifiedName~UnidadOrganizativaEditPage|FullyQualifiedName~UnidadOrganizativaDetailsPage|FullyQualifiedName~PageModelExhaustivityTests.UnidadesOrganizativas_` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-3.8 mergeada.
- **Estimación**: 160 ± 25 LoC producción (4 PageModels × ~40 LoC).
- **Commit guidance**: 1 commit `refactor(web): migrate four UnidadesOrganizativas PageModels to Categoria-based exhaustivity`. Si >400 LoC, dividir: (a) Index + Create; (b) Edit + Details + cleanup de OperationCanceledException manual.

### [x] T-3.10 — GREEN unificación de copy canónica vía `PageFeedback`
- **Slice**: 3
- **Files**: `src/SGV.Web/Pages/Common/PageFeedback.cs` (mod — agregar constantes).
- **RED**: —
- **GREEN**: Agregar `public const string TransportMessage = "No se pudo contactar al servicio. Intentá nuevamente."`, `UnauthorizedMessage = "Su sesión expiró. Vuelva a iniciar sesión."`, `ForbiddenMessage = "No tiene permisos para realizar esta operación."`, `UnexpectedMessage = "Respuesta inesperada del servidor."`, `NotFoundDeleteMessage = "El recurso ya no está disponible."`. Comentar que estos strings son la fuente de verdad para los 14 PageModels.
- **REFACTOR**: ninguno.
- **Verification**: tests verdes (los PageModels usan las constantes); ningún cambio de copy observable para el usuario.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-3.9 mergeada.
- **Estimación**: 15 ± 3 LoC producción.
- **Commit guidance**: 1 commit `feat(web): add canonical feedback message constants to PageFeedback`. Si los PageModels ya usan `PageFeedback.X` en lugar de strings literales, este commit puede ser opcional; verificar primero.

### Verificación final Slice 3

```bash
dotnet build SGV.slnx                                                              # sin warnings nuevos
bun run build --project src/SGV.Web                                                # bundle frontend verde
dotnet test --filter FullyQualifiedName~AuthSessionRedirectorTests
dotnet test --filter FullyQualifiedName~PageModelExhaustivityTests
dotnet test --filter FullyQualifiedName~Habilidad                                  # Create/Edit/Index pages
dotnet test --filter FullyQualifiedName~Cargo                                      # Index/Create/Edit/Habilidades
dotnet test --filter FullyQualifiedName~Puesto                                     # Index/Create/Edit
dotnet test --filter FullyQualifiedName~UnidadOrganizativa                         # Index/Create/Edit/Details
dotnet test SGV.slnx                                                               # suite completa verde
```

**Compromiso de transporte**: ningún `try/catch` en los 14 PageModels convierte `HttpRequestException`/`TaskCanceledException` a `Categoria.Transport`. Las excepciones nativas siguen propagándose cuando el cliente HTTP las emite (el switch sobre `Categoria` sólo opera cuando el `*CommandResult` se construyó a partir de una `HttpResponseMessage`).

**Commit/PR guidance Slice 3**: 1 PR `fix/125-s3-redirector-page-models`. Branch desde develop (rebase después de merge de Slice 2). PR target: develop. **Estimación**: ~700 LoC (producción + tests, +80 sobre design §12).

## Fase 4 — Slice 4: `ApiResults.MapCategoria` exhaustivo + tests DeleteResultContract

### [x] T-4.1 — RED `ApiResultsTests` [Theory] exhaustivo por `ErrorCategoria`
- **Slice**: 4
- **Files**: `tests/SGV.Tests/Api/Infrastructure/Results/ApiResultsTests.cs` (mod).
- **RED**: `[Theory]` parametrizada contra `Enum.GetValues<ErrorCategoria>()` con 7 InlineData (uno por variante). Cada fila assertea:
  - `ErrorCategoria.Validation → ObjectResult con StatusCode = 400`
  - `ErrorCategoria.NotFound → ObjectResult con StatusCode = 404`
  - `ErrorCategoria.Conflict → ObjectResult con StatusCode = 409`
  - `ErrorCategoria.Unauthorized → ObjectResult con StatusCode = 401`
  - `ErrorCategoria.Forbidden → ObjectResult con StatusCode = 403`
  - `ErrorCategoria.Transport → ObjectResult con StatusCode = 503`
  - `ErrorCategoria.Unexpected → ObjectResult con StatusCode = 500`
  
  Para cada variante, se invoca `ApiResults.ToProblemResult(...)` con un `*Error` que tiene `Categoria = X` y se assertea el `ObjectResult.StatusCode`. Como el código actual sólo inspecciona `Type` (no `Categoria`), el test fallará con status incorrecto hasta que `MapCategoria` exista y `Map*Status` delegue.
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~ApiResultsTests.ToProblemResult_ErrorCategoriaMatrix` falla con status incorrecto.
- **Rollback**: `git checkout HEAD~ -- tests/SGV.Tests/Api/Infrastructure/Results/ApiResultsTests.cs`.
- **Dependencies**: T-3.10 mergeada.
- **Estimación**: 50 ± 5 LoC tests.
- **Commit guidance**: 1 commit `test(api): add ApiResults exhaustive [Theory] over seven ErrorCategoria variants`.

### [x] T-4.2 — GREEN agregar `MapCategoria` a `ApiResults` + refactor `Map*Status`
- **Slice**: 4
- **Files**: `src/SGV.Api/Infrastructure/Results/ApiResults.cs` (mod).
- **RED**: —
- **GREEN**: 
  - Agregar helper privado `private static int MapCategoria(ErrorCategoria categoria) => categoria switch { Validation = 400, NotFound = 404, Conflict = 409, Unauthorized = 401, Forbidden = 403, Transport = 503, Unexpected = 500, /* no default */ _ => throw new SwitchExpressionException(...) }`.
  - Refactorizar `MapCargoStatus`, `MapCargoSkillStatus`, `MapHabilidadStatus`, `MapPuestoStatus`, `MapUnidadOrganizativaStatus`, `MapOcupacionStatus`, `MapPersonaStatus`, `MapPersonaSkillStatus`, `MapUsuarioStatus` para que deleguen vía `MapCategoria(ErrorCategoriaMappers.ToCategoria(type))`.
  - Los métodos públicos `ToProblemResult(*Error)` mantienen su firma actual (compat); sólo el mapping interno cambia.
- **REFACTOR**: extraer `MapCategoria` y `ErrorCategoriaMappers.ToCategoria` como única fuente de verdad.
- **Verification**: `dotnet test --filter FullyQualifiedName~ApiResultsTests` verde. `dotnet build src/SGV.Api/SGV.Api.csproj` sin warnings.
- **Rollback**: `git revert <commit-sha>`; `ApiResults` vuelve a sus `Map*Status` originales.
- **Dependencies**: T-4.1 mergeada.
- **Estimación**: 50 ± 10 LoC producción (1 helper nuevo + 9 refactors de mapeo).
- **Commit guidance**: 1 commit `refactor(api): centralize ApiResults status mapping via MapCategoria helper`.

### [x] T-4.3 — RED `DeleteResultContractTests` para 4 DeleteResults adicionales
- **Slice**: 4
- **Files**: `tests/SGV.Tests/Contracts/DeleteResults/HabilidadDeleteResultContractTests.cs` (new); `tests/SGV.Tests/Contracts/DeleteResults/CargoDeleteResultContractTests.cs` (new); `tests/SGV.Tests/Contracts/DeleteResults/PuestoDeleteResultContractTests.cs` (new); `tests/SGV.Tests/Contracts/DeleteResults/UnidadOrganizativaDeleteResultContractTests.cs` (new).
- **RED**: Cada archivo tiene 4 tests:
  - `Record_ExposesFourPositionalPropertiesWithExpectedNames` (orden alfabético: `Categoria`, `Code`, `Message`, `StatusCode`, `Succeeded`).
  - `Record_PropertiesHaveExpectedClrTypes` (incluye verificación de que `Categoria: ErrorCategoria` y `StatusCode: HttpStatusCode?`).
  - `Record_SucceededTrue_CategoriaDefaultsToExpectedValue` (Succeeded=true → Categoria = `default`).
  - `Record_SucceededFalse_CategoriaPobladaSegunStatus` (Succeeded=false con status 404 → Categoria=NotFound, etc.).
- **GREEN**: —
- **Verification**: `dotnet test --filter FullyQualifiedName~DeleteResultContractTests` falla por propiedades ausentes en tests de succeeded-false (los records en Slice 1 ya tienen `Categoria` poblable, pero este test exige que `Categoria` se compute correctamente según `StatusCode`).
- **Rollback**: `rm tests/SGV.Tests/Contracts/DeleteResults/*.cs`.
- **Dependencies**: T-4.2 mergeada.
- **Estimación**: 110 ± 10 LoC tests (4 archivos × ~28 LoC cada uno).
- **Commit guidance**: 1 commit `test(contracts): add DeleteResultContractTests for four remaining DeleteResults`.

### [x] T-4.4 — GREEN verificar que los 5 clientes HTTP popular `Categoria` en `*DeleteResult`
- **Slice**: 4
- **Files**: `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` (mod — `DeleteAsync`); `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` (mod — `DeleteAsync`, `DeleteSkillAsync`); `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs` (mod — `DeleteAsync`); `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` (mod — `DeleteAsync`).
- **RED**: —
- **GREEN**: En cada `DeleteAsync`/`DeleteSkillAsync` no-success, usar `CommandResultMapper.Map` para derivar `(Categoria, Code, Message, StatusCode)` y construir el `*DeleteResult` con `Categoria` poblado. Para 204 (success), mantener `Categoria = default` y `StatusCode = HttpStatusCode.NoContent`. Validar que los tests de Slice 2 sobre `*DeleteResult` (que existen como parte de `*ApiClientTests.DeleteAsync_*` y los nuevos tests de Slice 4 en `DeleteResultContractTests`) pasen.
- **REFACTOR**: extraer una helper estática `BuildDeleteResult(HttpResponseMessage, ApiProblemReader.Result, HttpStatusCode? successStatus)` que sea común a los 5 clientes; evita duplicación.
- **Verification**: `dotnet test --filter FullyQualifiedName~*ApiClientTests.DeleteAsync|FullyQualifiedName~DeleteResultContractTests` verde.
- **Rollback**: `git revert <commit-sha>`.
- **Dependencies**: T-4.3 mergeada.
- **Estimación**: 50 ± 10 LoC producción.
- **Commit guidance**: 1 commit `refactor(web): populate Categoria on DeleteResult via CommandResultMapper across five clients`.

### [x] T-4.5 — GREEN popular `Categoria` en `SGV.Aplicacion/*ServicioComandos`
- **Slice**: 4
- **Files**: `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs` (mod — 16 sitios); `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs` (mod); `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` (mod); `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` (mod); `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` (mod); `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs` (mod — 2 sitios).
- **RED**: —
- **GREEN**: En cada `new *Error(Type, "Code", "Message")` en los servicios de aplicación, agregar el parámetro `Categoria` explícitamente, mapeando vía `ErrorCategoriaMappers.ToCategoria(type)` cuando es 1:1, o pasando el `Categoria` explícito cuando la semántica difiere (p.ej. `CargoError(CargoErrorType.Validation, "DatosInvalidos", "...", Categoria: ErrorCategoria.Validation)`). Esto preserva la metadata del `Categoria` para logging/downstream.
- **REFACTOR**: para los casos donde el mapeo es 1:1, considerar extraer `static *Error Create(Type type, string code, string message)` factory que centralice el `Categoria = ErrorCategoriaMappers.ToCategoria(type)`.
- **Verification**: `dotnet build SGV.slnx` verde; `dotnet test SGV.slnx` verde.
- **Rollback**: `git revert <commit-sha>`; los servicios vuelven a no setear `Categoria` explícitamente (queda con default `Unexpected`).
- **Dependencies**: T-4.4 mergeada.
- **Estimación**: 60 ± 15 LoC producción (cambio mecánico en ~30 sitios).
- **Commit guidance**: 1 commit `feat(aplicacion): populate Categoria on every Error construction in service commands`.

### [x] T-4.6 — GREEN documentar bloqueo de `[Obsolete]` removal
- **Slice**: 4
- **Files**: `openspec/changes/2026-07-13-taxonomia-errores-commandresult/tasks.md` (este archivo, mod — nota explícita); code review checklist.
- **RED**: —
- **GREEN**: Documentar en code review (vía comentario en `tasks.md` de Slice 4 y en PR description) que la eliminación de los `[Obsolete]` de los 6 `*ErrorType` enums NO entra en Slice 4 — eso ocurre en la fase `sdd-archive` una vez que el change esté cerrado. Bloquear cualquier intento de remover el atributo `[Obsolete]` durante review.
- **Verification**: inspección manual; el archivo `src/SGV.Contracts/*/Comandos/*.cs` sigue conteniendo `[Obsolete]` en los 6 enums.
- **Rollback**: N/A (es documentación, no código).
- **Dependencies**: T-4.5 mergeada.
- **Estimación**: 0 LoC producción (5 ± 2 LoC de comentario).
- **Commit guidance**: fusionar con T-4.5 si total <50 LoC; si no, separar.

### Verificación final Slice 4

```bash
dotnet build SGV.slnx                                                              # sin warnings nuevos
dotnet test --filter FullyQualifiedName~ApiResultsTests
dotnet test --filter FullyQualifiedName~DeleteResultContract
dotnet test --filter FullyQualifiedName~*ApiClientTests
dotnet test SGV.slnx                                                               # suite completa verde
```

**Compromiso de transporte**: Slice 4 no toca el comportamiento de transporte de los clientes HTTP (eso es Slice 2). Sólo cambia la metadata `Categoria` en los `*Error`/`*DeleteResult` y el mapping status HTTP en `ApiResults`.

**Commit/PR guidance Slice 4**: 1 PR `fix/125-s4-apiresults-delete-results`. Branch desde develop (rebase después de merge de Slice 2). PR target: develop. Slice 3 y Slice 4 NO se bloquean entre sí. **Estimación**: ~250 LoC (producción + tests).

## Resumen de work-units (cross-slice)

Lista de los commits individuales recomendados por la skill `work-unit-commits`. Cada commit representa una unidad de revisión coherente. Conteo total: ~32 commits agrupados en 4 PRs.

### Slice 1 (PR #1, `fix/125-s1-contracts-error-taxonomy`, ~350 LoC)

| Commit | Tipo | Conventional | LoC |
|--------|------|--------------|-----|
| 1 | test | `test(contracts): add ErrorCategoria shape and leaf invariant tests` | 30 |
| 2 | feat | `feat(contracts): add ErrorCategoria enum with append-only ordinals` | 20 |
| 3 | test | `test(contracts): add ErrorCategoriaMappers round-trip tests for six enums` | 90 |
| 4 | feat | `feat(contracts): add ErrorCategoriaMappers with six enum round-trips` | 60 |
| 5 | chore | `chore(contracts): mark six ErrorType enums as obsolete pending archive` | 20 |
| 6 | test | `test(contracts): add Error and DeleteResult contract tests for Categoria and nullable StatusCode` | 110 |
| 7 | feat | `feat(contracts): add Categoria to Error records and DeleteResult contracts` | 50 |
| 8 | docs | `docs: register Issue #125 error taxonomy decision in decisiones-implementacion` | 25 |

### Slice 2 (PR #2, `fix/125-s2-mapper-clients`, ~750 LoC)

| Commit | Tipo | Conventional | LoC |
|--------|------|--------------|-----|
| 9 | test | `test(web): add IsDnsFailure detection for HttpRequestException inner SocketException` | 35 |
| 10 | feat | `feat(web): add IsDnsFailure detection to TransportFailureClassifier` | 10 |
| 11 | test | `test(web): add DnsFailure scenario row to HttpClientExceptionScenarios` | 10 |
| 12 | test | `test(web): add CommandResultMapper tests covering full HTTP matrix and atypical statuses` | 110 |
| 13 | feat | `feat(web): add CommandResultMapper with full HTTP status matrix` | 70 |
| 14 | test | `test(web): extend HabilidadApiClientTests with 401/403/5xx/DNS/cancel/timeout coverage` | 90 |
| 15 | refactor | `refactor(web): migrate HabilidadApiClient to CommandResultMapper` | 60 |
| 16 | test | `test(web): extend CargoApiClient and CargoSkillApiClient tests with full HTTP matrix and transport scenarios` | 140 |
| 17 | refactor | `refactor(web): migrate CargoApiClient and CargoSkill to CommandResultMapper` | 80 |
| 18 | test | `test(web): extend PuestosApiClientTests with 401/403/5xx/DNS/cancel/timeout coverage` | 90 |
| 19 | refactor | `refactor(web): migrate PuestosApiClient to CommandResultMapper` | 40 |
| 20 | test | `test(web): extend UnidadOrganizativaApiClientTests with 401/403/5xx/DNS/cancel/timeout coverage` | 90 |
| 21 | refactor | `refactor(web): migrate UnidadOrganizativaApiClient to CommandResultMapper` | 40 |

### Slice 3 (PR #3, `fix/125-s3-redirector-page-models`, ~700 LoC)

| Commit | Tipo | Conventional | LoC |
|--------|------|--------------|-----|
| 22 | test | `test(web): add AuthSessionRedirector tests for local/absolute/protocol-relative cases` | 100 |
| 23 | feat | `feat(web): add IAuthSessionRedirector with open-redirect guard` | 50 |
| 24 | chore | `chore(web): register IAuthSessionRedirector and IUrlHelperFactory in DI` | 3 |
| 25 | test | `test(web): add exhaustivity coverage for 14 PageModels against seven ErrorCategoria` | 220 |
| 26 | refactor | `refactor(web): migrate Habilidades Create and Edit to Categoria-based exhaustivity` | 80 |
| 27 | refactor | `refactor(web): migrate Habilidades Index to Categoria-based exhaustivity` | 50 |
| 28 | refactor | `refactor(web): migrate four Cargos PageModels to Categoria-based exhaustivity` | 160 |
| 29 | refactor | `refactor(web): migrate three Puestos PageModels to Categoria-based exhaustivity` | 120 |
| 30 | refactor | `refactor(web): migrate four UnidadesOrganizativas PageModels to Categoria-based exhaustivity` | 160 |
| 31 | feat | `feat(web): add canonical feedback message constants to PageFeedback` | 15 |

### Slice 4 (PR #4, `fix/125-s4-apiresults-delete-results`, ~250 LoC)

| Commit | Tipo | Conventional | LoC |
|--------|------|--------------|-----|
| 32 | test | `test(api): add ApiResults exhaustive [Theory] over seven ErrorCategoria variants` | 50 |
| 33 | refactor | `refactor(api): centralize ApiResults status mapping via MapCategoria helper` | 50 |
| 34 | test | `test(contracts): add DeleteResultContractTests for four remaining DeleteResults` | 110 |
| 35 | refactor | `refactor(web): populate Categoria on DeleteResult via CommandResultMapper across five clients` | 50 |
| 36 | feat | `feat(aplicacion): populate Categoria on every Error construction in service commands` | 60 |

Total: 36 commits, ~2175 LoC brutos (incluye headers de archivo, usings, comentarios). El conteo efectivo de "líneas modificadas" para el budget de review es ~2050 (excluye duplicación de headers y firmas idénticas).

## Plan de rollback

### Rollback Slice 1

- **Comando**: `git revert -m 1 <merge-sha-de-PR-1>` si la PR #1 ya mergeó; o `git reset --hard <sha-anterior-a-merge>` si todavía no mergeó.
- **Archivos con riesgo de afectar otras features**: ninguno. Los `[Obsolete]` son no-rompedores; el código de producción existente sigue compilando porque los records sólo AGREGAN una propiedad (no quitan nada). El único riesgo: si algún test asume que `HabilidadError` tiene exactamente 4 propiedades posicionales — mitigado por los contract tests en `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillDeleteResultContractTests.cs` que verifican el shape.
- **Feature flag**: N/A — no se introduce feature flag; la taxonomía es una refactorización compatible.
- **Plan B**: si el revert falla, re-aplicar manualmente los `CargoErrorType`, etc. como non-obsolete y eliminar `ErrorCategoria.cs` y `ErrorCategoriaMappers.cs`. `dotnet build SGV.slnx` debería volver al estado previo.

### Rollback Slice 2

- **Comando**: `git revert -m 1 <merge-sha-de-PR-2>` o `git reset --hard <sha-anterior-a-merge>`.
- **Archivos con riesgo de afectar otras features**: los 5 clientes HTTP (`HabilidadApiClient`, `CargoApiClient`, `PuestosApiClient`, `UnidadOrganizativaApiClient`). Si otra feature en desarrollo asume que el cliente expone `ErrorCategoria` en `result.Error.Categoria`, el rollback rompe esa feature.
- **Feature flag**: N/A. El comportamiento observable (HTTP responses) no cambia; sólo cambia el shape interno de los `*Error.Categoria`. La UI NO consume `Categoria` aún (eso es Slice 3).
- **Plan B**: revertir el commit de cada cliente individualmente. Empezar por `HabilidadApiClient` (el más simple) para minimizar blast radius.

### Rollback Slice 3

- **Comando**: `git revert -m 1 <merge-sha-de-PR-3>` o `git reset --hard <sha-anterior-a-merge>`.
- **Archivos con riesgo de afectar otras features**: los 14 PageModels de Organizacion. Si otra feature en desarrollo asume que los PageModels tienen `IAuthSessionRedirector` en su constructor, el rollback rompe esa feature.
- **Feature flag**: N/A. La introducción del redirector es opcional desde el punto de vista de UX (los usuarios siguen viendo el mensaje inline); sólo cambia a redirect cuando `Categoria == Unauthorized`.
- **Plan B**: revertir el commit de cada PageModel individualmente, empezando por los más aislados (`Habilidades/Create`, `Habilidades/Edit`).

### Rollback Slice 4

- **Comando**: `git revert -m 1 <merge-sha-de-PR-4>` o `git reset --hard <sha-anterior-a-merge>`.
- **Archivos con riesgo de afectar otras features**: `src/SGV.Api/Infrastructure/Results/ApiResults.cs` (afecta todas las respuestas de error del API). Si otra feature asume que `Unauthorized` se serializa como `Categoria.Unauthorized` (no como `Validation/Unexpected` degradado), el rollback degrada esa respuesta.
- **Feature flag**: N/A.
- **Plan B**: revertir `ApiResults.MapCategoria` primero; los `Map*Status` originales siguen compilando (tienen `_ => 400` fallback). Después revertir los contract tests.

## Comandos de verificación por developer

### Pre-flight común (todos los slices)

```bash
git fetch origin
git checkout develop
git pull origin develop
git checkout -b fix/125-sN-<slice-slug>    # según slice
```

### Slice 1

```bash
dotnet restore SGV.slnx
dotnet build SGV.slnx
dotnet test SGV.slnx --filter FullyQualifiedName~ErrorCategoria
dotnet test SGV.slnx --filter FullyQualifiedName~ErrorCategoriaMappers
dotnet test SGV.slnx --filter FullyQualifiedName~ErrorRecordContract
dotnet test SGV.slnx --filter FullyQualifiedName~DeleteResultContract
dotnet test SGV.slnx --no-build --configuration Release
```

### Slice 2

```bash
dotnet restore SGV.slnx
dotnet build SGV.slnx
dotnet test SGV.slnx --filter FullyQualifiedName~CommandResultMapper
dotnet test SGV.slnx --filter FullyQualifiedName~TransportFailureClassifier
dotnet test SGV.slnx --filter FullyQualifiedName~HabilidadApiClientTests
dotnet test SGV.slnx --filter FullyQualifiedName~CargoApiClientBasic
dotnet test SGV.slnx --filter FullyQualifiedName~CargoSkillApiClientTests
dotnet test SGV.slnx --filter FullyQualifiedName~PuestosApiClientTests
dotnet test SGV.slnx --filter FullyQualifiedName~UnidadOrganizativaApiClientTests
dotnet test SGV.slnx --no-build --configuration Release
```

Verificación de contrato de transporte: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadApiClientTests.WhenHttpClient|FullyQualifiedName~CargoApiClientBasic.Cancelation|FullyQualifiedName~PuestosApiClientTests.WhenHttpClient|FullyQualifiedName~UnidadOrganizativaApiClientTests.PreCanceled"` debe seguir verde — confirma que las excepciones nativas siguen propagándose.

### Slice 3

```bash
dotnet restore SGV.slnx
dotnet build SGV.slnx
dotnet test SGV.slnx --filter FullyQualifiedName~AuthSessionRedirectorTests
dotnet test SGV.slnx --filter FullyQualifiedName~PageModelExhaustivityTests
dotnet test SGV.slnx --filter FullyQualifiedName~Habilidad
dotnet test SGV.slnx --filter FullyQualifiedName~Cargo
dotnet test SGV.slnx --filter FullyQualifiedName~Puesto
dotnet test SGV.slnx --filter FullyQualifiedName~UnidadOrganizativa
dotnet test SGV.slnx --no-build --configuration Release
# Frontend (SGV.Web usa Bun/Gulp)
cd src/SGV.Web && bun install && bun run build
```

### Slice 4

```bash
dotnet restore SGV.slnx
dotnet build SGV.slnx
dotnet test SGV.slnx --filter FullyQualifiedName~ApiResultsTests
dotnet test SGV.slnx --filter FullyQualifiedName~DeleteResultContract
dotnet test SGV.slnx --filter FullyQualifiedName~*ApiClientTests
dotnet test SGV.slnx --no-build --configuration Release
```

### CI (todos los slices)

El pipeline `.github/workflows/ci.yml` levanta MySQL 8 y ejecuta `dotnet test --no-build --configuration Release`. Todos los slices deben pasar CI de manera independiente — el cambio no introduce migraciones nuevas, por lo que `Database.Migrate()` sigue siendo idempotente.

## Notas de TDD

- **Orden RED → GREEN es no negociable**: el primer commit de cada task RED debe ser el archivo de test SOLAMENTE. Compila pero falla. El segundo commit es el código de producción que lo hace verde. NO invertir el orden — esto rompería el contrato de strict_tdd del repo.
- **`dotnet test --filter FullyQualifiedName~X` por task RED**: cada task RED tiene un filtro `FullyQualifiedName` específico que debe fallar antes del GREEN. Verificar manualmente que el filtro apropiado compila y falla con el mensaje esperado (símbolo ausente, assertion fallida).
- **Tests de contract (records)**: en `SGV`, los contract tests usan reflexión para verificar nombres y tipos CLR de las propiedades. Esto blinda el shape público de los records. Cada `*Error.Categoria` y `*DeleteResult.Categoria/StatusCode?` nuevo tiene su propio test.
- **Tests parametrizados con `[Theory]`**: cada nueva fila `InlineData` debe agregar valor real (no duplicar coverage). Para los 18 status codes de la matriz REQ-2 + 5 atípicos, preferimos un `[Theory]` único con 23 `InlineData` en lugar de 28 `[Fact]` separados.
- **TDD con datos realistas**: usar `RecordingHandler` con `HttpResponseMessage` simuladas con `ProblemDetails` válidos (no strings arbitrarios) para que el `ApiProblemReader` parsee correctamente. Esto evita false-passing tests que validan defaults en lugar del path real.
- **Strict TDD en PageModels**: cada cambio de switch sobre `Categoria` en un PageModel requiere un test que enumere las 7 variantes y assertea que el switch las cubre todas (vía `PageModelExhaustivity.AssertCoversAllCategorias`).

## Riesgos del plan

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| **Divergencia de forecast**: el plan suma ~2050 LoC vs ~1850 del design §12 (+200 LoC). | **LOW** | La divergencia está concentrada en Slice 2 (+120, tests parametrizados no estimados) y Slice 3 (+80, exhaustividad por PageModel). Ambos son deliverables explícitos del design §11.2/11.3. Documentado en §Divergencia de forecast. |
| **Slice 3 contiene 6 commits con >100 LoC cada uno** (T-3.4, T-3.7, T-3.8, T-3.9 con 220/160/120/160 LoC). | **MED** | Cada commit está acotado a un dominio (Habilidades, Cargos, Puestos, UnidadesOrganizativas) y se commitea de manera incremental — el reviewer puede revisar PR parcial y pedir cambios por dominio. Si la PR crece >400 LoC, dividir en sub-PRs por dominio. |
| **Migración de `SGV.Aplicacion/*ServicioComandos` (T-4.5)**: ~30 sitios de construcción de `*Error` records distribuidos en 6 archivos. Si se olvida alguno, el `Categoria` queda con default `Unexpected` (semánticamente incorrecto pero no rompe compilación). | **MED** | El test exhaustivo de ApiResults ([Theory] parametrizada, T-4.1) verifica que `Categoria` por sí sola produce el status correcto — pero el status actual depende de `Type` (vía `Map*Status` refactorizado), no de `Categoria`. Para cerrar el gap, agregar assertion en T-4.5 que verifica que `*Error.Categoria == ErrorCategoriaMappers.ToCategoria(*Error.Type)` para los casos 1:1. |
| **Branch base incorrecta en chained PR**: si una PR #N+1 se construye contra la rama de PR #N en vez de develop, el diff muestra trabajo previo. | **MED** | Documentado en §Diagrama topológico: cada PR se rebasea desde develop tras el merge de la previa. Code review checklist: si el diff de PR #2 muestra cambios de PR #1, la base es incorrecta. |
| **Conflictos en `docs/decisiones-implementacion.md`** entre Slice 1 (entrada inicial) y futuros cambios (slice 4 podría agregar otra entrada). | **LOW** | Slice 1 sólo agrega una entrada; Slice 4 no toca ese archivo. Riesgo mínimo. |
| **`ApiResults` con `TreatWarningsAsErrors`**: si el repo decide activar `TreatWarningsAsErrors` durante el ciclo de este change, los `CS8524` (switch sin default) en `MapCategoria` y `CommandResultMapper` rompen build. | **LOW** | El design §4.1 documenta explícitamente que `TreatWarningsAsErrors` queda fuera del alcance de #125. Si se activa externamente, agregar `default => throw new SwitchExpressionException(...)` a ambos switches — eso rompe la garantía del test exhaustivo, así que se prefiere NO activar `TreatWarningsAsErrors` hasta que el repo decida cómo manejar el warning. |
| **`CargoSkillDeleteResult.StatusCode` no-nullable → nullable** (T-1.6): rompe call sites que asumen non-nullable (`Puestos/Index.cshtml.cs`, otros). | **LOW** | El cambio es source-compatible para la mayoría de los call sites (los `?` se aceptan implícitamente). Si la compilación falla, ajustar los call sites en el mismo commit. |
| **Slice 4 cubre controllers de la API implícitamente**: la actualización de `SGV.Aplicacion/*ServicioComandos` (T-4.5) no toca los controllers (`SGV.Api/Controllers/*`) porque estos últimos NO construyen `*Error` records directamente — los reciben de los servicios de aplicación. | **LOW** | Slice 4 sólo verifica el shape del `*Error` vía `ApiResults`. Si los controllers necesitaran emitir `Categoria` directamente, eso sería una task adicional fuera del scope de #125. |

## Métricas esperadas para `sdd-verify`

Tras el cierre de cada slice, los siguientes escenarios Given/When/Then del spec.md deben pasar:

### Tras Slice 1

- **REQ-1 Scenario: Enum expone siete variantes con ordinales fijos** (`tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs::Enum_HasSevenVariantsInOrder`).
- **REQ-1 Scenario: `SGV.Contracts` permanece leaf** (`ContractsProject_HasNoProjectReferences_AndStaysLeaf`).
- **REQ-3 Scenario: `HabilidadError` expone `Categoria`** (extendido a 6 records en T-1.5/T-1.6).
- **REQ-3 Scenario: `CargoSkillErrorType.Transport` mantiene ordinal 5** (test RED explícito en `ErrorCategoriaMappersTests`).

### Tras Slice 2

- **REQ-2 Scenario: 401 se mapea a `Unauthorized` con status preservado** (T-2.5/T-2.6).
- **REQ-2 Scenario: Status atípico cae en `Unexpected` sin perder status** (T-2.5 con 300, 418, 999, 226, 507).
- **REQ-4 Scenario: Helper centraliza la matriz** (T-2.5 verifica `Map(403)` retorna `Forbidden`/`Acceso denegado`/`403`).
- **REQ-4 Scenario: Cliente usa el helper** (T-2.7/T-2.8 verifica `HabilidadApiClient.UpdateAsync` con mock 403 → `Categoria == Forbidden`).
- **REQ-8 Scenario: `HttpRequestException` se propaga sin conversión** (cubierto por tests preexistentes + T-2.7/9/11/13 `DnsFailureScenario_PropagatesHttpRequestException`).
- **REQ-8 Scenario: `IsDnsFailure` detecta `NameResolutionFailure`** (T-2.1/T-2.2).
- **REQ-9 Scenario: `CommandResultMapperTests` cubre toda la matriz** (T-2.5 con 18 status codes + 5 atípicos).

### Tras Slice 3

- **REQ-5 Scenario: `Unauthorized` redirige** (cubierto por `PageModelExhaustivityTests.Habilidades_Create` que itera 7 ErrorCategoria y verifica que `Unauthorized` invoca `authRedirector.TryRedirectToLogin`).
- **F9 open-redirect guard**: `AuthSessionRedirectorTests.TryRedirectToLogin_WithAbsoluteExternalUrl_DropsReturnUrl` y `TryRedirectToLogin_WithProtocolRelativeUrl_DropsReturnUrl` cubren los dos vectores de open-redirect.

### Tras Slice 4

- **REQ-6 Scenario: Categoría `Transport` produce 503** (T-4.1/T-4.2 con `ErrorCategoria.Transport → 503`).
- **REQ-6 exhaustividad**: `ApiResultsTests.ToProblemResult_ErrorCategoriaMatrix` enumera las 7 variantes y verifica status específico por cada una.
- **REQ-7 Scenario: Delete 409 expone `Categoria=Conflict`** (T-4.3 verifica `DeleteResultContractTests.HabilidadDeleteResult.Record_SucceededFalse_CategoriaPobladaSegunStatus` con `StatusCode=Conflict` → `Categoria=Conflict`).

### Cross-slice

- **REQ-8 Scenario: `LoginAsync` 401 retorna `null`**: este test preexistente (`WebAuthenticationTests.LoginAsync_WhenApiReturnsUnauthorized_ReturnsNull`) sigue verde a través de todos los slices — `AuthApiClient` no se toca en este change.

## Decisiones del agente (registradas por modo auto)

1. **Ubicación de los nuevos contract tests**: en `tests/SGV.Tests/Contracts/` (nuevo directorio) en lugar de `tests/SGV.Tests/Aplicacion/Organizacion/` donde está el `CargoSkillDeleteResultContractTests.cs` preexistente. Razón: seguir la guía del design §11.1 que sugiere `tests/SGV.Tests/Contracts/ErrorCategoriaTests.cs`. La asimetría con `CargoSkillDeleteResultContractTests.cs` se acepta para minimizar churn.
2. **No mover el `CargoSkillDeleteResultContractTests.cs` preexistente**: aunque conceptualmente pertenece a `tests/SGV.Tests/Contracts/`, moverlo agrega churn innecesario. Se mantiene donde está y se extiende in-place.
3. **`Categoria` con default `Unexpected` en los `*Error` records**: esto permite que los call sites existentes en `SGV.Aplicacion/*ServicioComandos` sigan compilando sin modificación en Slice 1. Slice 4 agrega T-4.5 para popular `Categoria` explícitamente en los servicios de aplicación.
4. **`PuestoDeleteResult.StatusCode` pasa de `HttpStatusCode` a `HttpStatusCode?` en Slice 1**: aunque el design §2.5 lo menciona, conviene hacerlo junto con la adición de `Categoria` para que los 5 `*DeleteResult` queden consistentes en un solo commit.
5. **Refactor `Map*Status` en Slice 4**: el design §4.1 dice "Los métodos `MapCargoStatus`, `MapPuestoStatus`, etc. vigentes se conservan y delegan a `MapCategoria`". En la práctica esto es T-4.2. No hay un Slice intermedio que haga esto.
6. **`PageFeedback` constants opcionales en T-3.10**: si los PageModels ya están usando `PageFeedback.X` en lugar de strings literales (verificar en Slice 3 antes de aplicar), T-3.10 puede ser opcional. Se mantiene como task documentado pero su commit puede absorberse en otro si total <50 LoC.
7. **`SGV.Aplicacion/*ServicioComandos` no se toca en Slice 1**: aunque los records obtienen `Categoria` con default `Unexpected`, el comportamiento es semánticamente incorrecto (un `CargoError(CargoErrorType.Conflict, ...)` queda con `Categoria=Unexpected` por default). Esto se corrige en T-4.5 (Slice 4) cuando el resto del sistema está listo para consumir `Categoria` consistentemente.
8. **`HttpStatusCode` no-nullable → nullable en `PuestoDeleteResult`**: cambio source-compatible en la mayoría de los call sites. Si la compilación falla, ajustar los call sites en el mismo commit de T-1.6.
9. **Eliminación de `[Obsolete]` queda en `sdd-archive`**: Slice 4 NO elimina los `[Obsolete]` de los 6 `*ErrorType` enums. Eso ocurre en la fase archive, una vez que todos los call sites estén migrados.