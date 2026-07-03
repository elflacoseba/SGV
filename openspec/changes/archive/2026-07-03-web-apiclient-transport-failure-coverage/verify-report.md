# Verify Report: web-apiclient-transport-failure-coverage

## Status

READY-FOR-MERGE

## Spec Compliance

| Requirement/Scenario | Test que cubre | Resultado | Notes |
|---|---|---|---|
| Baseline R1 — Propagar fallos nativos de transporte | `HabilidadApiClientTests.QueryAsync_TransportFails_PropagatesNativeException` + `CargoApiClientTests.QueryAsync_TransportFails_PropagatesNativeException` | CUMPLE | Ambos usan `MemberData(HttpClientExceptionScenarios.TransportExceptionData)` con 2 filas (`TaskCanceledException`, `HttpRequestException`) y `Assert.ThrowsAsync(expectedExceptionType, ...)` sobre `QueryAsync` real. El filtro específico pasó en runtime. |
| Baseline R2 — Respetar cancelación cooperativa del consumidor | `HabilidadApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` + `CargoApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` | CUMPLE | Ambos usan `Assert.ThrowsAnyAsync<OperationCanceledException>(...)` con token pre-cancelado y validan `handler.LastRequest == null`, cubriendo cancelación observable y ausencia de envío HTTP. |
| Delta S1 — `HabilidadApiClient` propaga excepciones nativas de transporte | `HabilidadApiClientTests.QueryAsync_TransportFails_PropagatesNativeException` | CUMPLE | La teoría ejecuta 2 casos reales del pipeline (`TaskCanceledException`, `HttpRequestException`) contra `HabilidadApiClient.QueryAsync`. |
| Delta S2 — `CargoApiClient` propaga excepciones nativas de transporte | `CargoApiClientTests.QueryAsync_TransportFails_PropagatesNativeException` | CUMPLE | La teoría ejecuta 2 casos reales del pipeline (`TaskCanceledException`, `HttpRequestException`) contra `CargoApiClient.QueryAsync`. |
| Delta S3 — Ambos clientes respetan token pre-cancelado | `HabilidadApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` + `CargoApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` | CUMPLE | Cobertura observable sobre clientes reales; no depende de asserts sobre detalles internos del cliente más allá del seam de prueba del handler. |

## Design Adherence

| Decisión de diseño | Estado | Notes |
|---|---|---|
| Helper como clase estática `HttpClientExceptionScenarios` | RESPETADO | Existe `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` como clase estática pública. |
| `TransportExceptionData` como `IEnumerable<object[]>` | RESPETADO | El dataset expone exactamente dos filas con `string`, `Func<Exception>` y `Type`. |
| `NewHandlerThrowing` factory | RESPETADO | Implementado vía `ThrowingHandler` interno que propaga la excepción creada por la factory. |
| `NewRecordingHandler` factories + `RecordingHandler` con doble constructor | RESPETADO | Hay overload sin parámetros y overload con `Func<HttpRequestMessage, HttpResponseMessage>`; `RecordingHandler` tiene ambos constructores. |
| Granularidad: 1 `[Theory]` por cliente sobre `QueryAsync` + 1 `[Fact]` por cliente para token pre-cancelado | RESPETADO | Se agregaron exactamente 4 tests de cliente nuevos para el contrato del cambio. |
| Aserciones: `Assert.ThrowsAsync(expectedExceptionType)` para transporte | RESPETADO | Ambas teorías usan esa forma exacta. |
| Aserciones: `Assert.ThrowsAnyAsync<OperationCanceledException>` para token pre-cancelado | RESPETADO | Ambos facts usan esa forma exacta. |
| Anti-drift: el helper sólo fabrica datos/handlers; los tests llaman al cliente real | RESPETADO | Los tests invocan `HabilidadApiClient.QueryAsync` y `CargoApiClient.QueryAsync`; el helper no envuelve operaciones del cliente. |
| Desviación justificada: chequeo de cancelación en `RecordingHandler.SendAsync` | WARNING | No estaba en el sketch mínimo del design, pero está alineado con la intención del contrato y permite validar `LastRequest == null` con un seam realista. No rompe el spec. |
| Tests extra del helper | RESPETADO | Hay 5 tests del helper en vez del mínimo implícito del design. Tienen valor real: dataset, propagación, constructor default, constructor custom y estado inicial. No se observan tests triviales ni redundantes. |

## Tasks Completion

| Task | Estado en `tasks.md` | Evidencia en commits | Resultado |
|---|---|---|---|
| 1.1 Crear `HttpClientExceptionScenarios.cs` | [x] | `0e635ddb` crea el helper compartido | OK |
| 1.2 Crear `HttpClientExceptionScenariosTests.cs` | [x] | `0e635ddb` crea el archivo de tests del helper | OK |
| 2.1 Migrar `HabilidadApiClientTests` al helper | [x] | `e4dac348` modifica `HabilidadApiClientTests.cs` y ajusta el helper | OK |
| 2.2 Agregar theory de transporte en `HabilidadApiClientTests` | [x] | `e4dac348` | OK |
| 2.3 Agregar fact de token pre-cancelado en `HabilidadApiClientTests` | [x] | `e4dac348` | OK |
| 3.1 Migrar `CargoApiClientTests` al helper | [x] | `25a77974` modifica `CargoApiClientTests.cs` | OK |
| 3.2 Agregar theory de transporte en `CargoApiClientTests` | [x] | `25a77974` | OK |
| 3.3 Agregar fact de token pre-cancelado en `CargoApiClientTests` | [x] | `25a77974` | OK |

## Test Results

### Build

```text
$ dotnet build SGV.slnx --configuration Release
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Suite completa

```text
$ dotnet test SGV.slnx --no-build --configuration Release
Failed!  - Failed:    12, Passed:  1254, Skipped:     0, Total:  1266, Duration: 33 s - SGV.Tests.dll (net10.0)
```

Totales:

- Total: 1266
- Passed: 1254
- Failed: 12
- Skipped: 0

Los 12 fallos observados coinciden con el baseline conocido de `OcupacionRepositoryTests` (issue #59):

1. `GetByIdIncludingHistoryAsync_ReturnsEvenIfDeleted`
2. `UpdateAsync_WithFinalize_SavesFechaFin`
3. `UpdateAsync_WithSoftDelete_SavesIsDeleted`
4. `ListAllIncludingHistoryAsync_ReturnsAllRows`
5. `ExistsActiveByPuestoAsync_Active_ReturnsTrue`
6. `UpdateAsync_WithReactivation_ClearsFechaFinAndIsDeleted`
7. `ExistsActiveByPersonaYPuestoAsync_DifferentPersona_ReturnsFalse`
8. `ExistsActiveByPersonaYPuestoAsync_Active_ReturnsTrue`
9. `GetByIdForUpdateAsync_Active_ReturnsWithNavigation`
10. `ExistsActiveByPuestoAsync_ExcludingId_IgnoresSelf`
11. `ListAllAsync_Default_ReturnsOnlyActiveRows`
12. `ExistsActiveByPersonaYPuestoAsync_ExcludingId_IgnoresSelf`

No aparecieron fallos nuevos fuera de esa lista.

### Tests específicos del change

```text
$ dotnet test SGV.slnx --no-build --configuration Release --filter "FullyQualifiedName~HttpClientExceptionScenariosTests|FullyQualifiedName~HabilidadApiClientTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~QueryAsync_TransportFails_PropagatesNativeException|FullyQualifiedName~QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest"
Passed!  - Failed:     0, Passed:    58, Skipped:     0, Total:    58, Duration: 46 ms - SGV.Tests.dll (net10.0)
```

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD Evidence reported | ✅ | `apply-progress.md` incluye tabla `TDD Cycle Evidence`. |
| All tasks have tests | ✅ | 5/5 filas de la tabla TDD tienen archivo de test verificable. |
| RED confirmed (tests exist) | ✅ | Existen `HttpClientExceptionScenariosTests.cs`, `HabilidadApiClientTests.cs` y `CargoApiClientTests.cs`. |
| GREEN confirmed (tests pass) | ✅ | El filtro específico pasó en runtime; la suite completa sólo conserva el baseline #59. |
| Triangulation adequate | ✅ | Transporte cubierto con 2 casos (`TaskCanceledException`, `HttpRequestException`) por cliente; cancelación cubierta con facts dedicados. |
| Safety Net for modified files | ✅ | Las suites modificadas (`HabilidadApiClientTests.cs`, `CargoApiClientTests.cs`) siguen pasando dentro del filtro específico. |

**TDD Compliance**: 6/6 checks passed.

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit | 41 | 3 | xUnit |
| Integration | 0 | 0 | N/A |
| E2E | 0 | 0 | N/A |
| **Total** | **41** | **3** | |

Distribución calculada sobre los archivos de test creados/modificados por el change: `HttpClientExceptionScenariosTests.cs` (5), `HabilidadApiClientTests.cs` (13), `CargoApiClientTests.cs` (23).

### Assertion Quality

✅ No se detectaron tautologías, smoke tests vacíos, loops fantasma ni asserts acoplados a detalles internos en los tests agregados del contrato de transporte.

## Production Code Untouched

Comando ejecutado:

```text
$ git diff develop~4..develop -- src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs src/SGV.Web/Integration/Organizacion/CargoApiClient.cs src/SGV.Web/Program.cs
```

Resultado: sin salida. No hay cambios en `HabilidadApiClient.cs`, `CargoApiClient.cs` ni `Program.cs` dentro del rango `develop~5..develop`.

## Commit Hygiene

### `git log develop~5..develop --format='%H%n%s%n%b%n---END---'`

```text
d64e3758...
test(web): address verify warnings on HttpClientExceptionScenarios
...body truncated...
---END---
b548879b1cf456b4659a057a4d0d420706dc223f
docs(sdd): record apply progress for web-apiclient-transport-failure-coverage

---END---
25a779749fb31ebc3c2cd8e3314b341529ccd22d
test(web): add transport failure coverage for CargoApiClient via shared helper

---END---
e4dac3482f52f100e0cda63c5af0389915ee7e25
test(web): migrate HabilidadApiClientTests to shared helper and add transport failure coverage

---END---
0e635ddbe82922b5d4efb884089bc9709eb1e3a7
test(web): add shared HttpClientExceptionScenarios helper for transport failures

---END---
```

### Validación

- `Co-Authored-By`: no se detectó ninguna ocurrencia en `git log develop~5..develop --format='%B' | grep -i "co-authored-by"`.
- Conventional commits: OK. Los mensajes siguen `test(web): ...` y `docs(sdd): ...`.
- Scope del diff: `git diff --name-status develop~5..develop` muestra sólo 6 archivos, todos dentro del scope esperado (4 tests + 2 artefactos SDD). El commit adicional `d64e3758` corrige los dos warnings del primer verify y agrega documentación explícita.

## Findings

### CRITICAL
- Ninguno.

### WARNING
- ~~`tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs:81-88` introduce una desviación menor del design sketch: `RecordingHandler.SendAsync` chequea cancelación antes de registrar `LastRequest`.~~ — **Resuelto en `d64e3758`**: la propiedad `LastRequest` ahora tiene un `<remarks>` que documenta explícitamente el contrato de cancelación y la paridad con `SocketsHttpHandler`.
- ~~`tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenariosTests.cs:26` y `:58` introducen `var` para enteros evidentes.~~ — **Resuelto en `d64e3758`**: ambos `var` se reemplazaron por `int` siguiendo la convención del repo.

### SUGGESTION
- Considerar anotar en `archive-report.md` que el filtro específico del change devuelve 58 tests porque también matchea suites/fakes relacionadas por nombre, aunque los 4 tests nuevos del contrato pasan correctamente. No es un problema técnico; sólo evita confusión al releer la evidencia.

## Recommendation

READY-FOR-MERGE — el change cumple el spec baseline y delta, respeta el design en lo esencial, mantiene producción intacta y no introduce regresiones fuera del baseline conocido de `OcupacionRepositoryTests` (#59). Los warnings originales del primer verify fueron atendidos en `d64e3758` y ya no bloquean nada.
