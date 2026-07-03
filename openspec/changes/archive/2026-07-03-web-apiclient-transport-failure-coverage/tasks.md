# Tasks: Cobertura de fallos de transporte en API clients web

> Issue: #78. Modo: openspec. Strict TDD: true (los tests son la verificación; no se toca producción).

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas estimadas cambiadas | ~90-130 |
| Riesgo presupuesto 400 líneas | Low |
| PR encadenados recomendados | No |
| Estrategia de delivery | ask-on-risk |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Unidades de trabajo sugeridas

| Unidad | Objetivo | PR probable | Notas |
|------|----------|-------------|-------|
| 1 | Helper compartido + suite Habilidad completa | PR único A | Pre-requisito del helper; añade 2 tests sobre Habilidad |
| 2 | Migración y cobertura equivalente para Cargo | PR único B (mismo PR físico) | Replica contra `CargoApiClient` |

## Fase 1: Helper compartido

- [x] 1.1 Crear `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` con clase estática pública que expone:
  - `TransportExceptionData` (`IEnumerable<object[]>`) con filas `TaskCanceledException` y `HttpRequestException` → `(string scenario, Func<Exception> factory, Type expectedType)`.
  - `NewHandlerThrowing(Func<Exception>)` cuyo `SendAsync` propaga la excepción.
  - `RecordingHandler : HttpMessageHandler` con `LastRequest` y un constructor que acepte `Func<HttpRequestMessage, HttpResponseMessage>` (compatibilidad con rutas existentes); más `NewRecordingHandler(...)` simétrica.
  - **Archivos**: `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` (nuevo). **Estimación**: +45/+65. **Criterio**: compila sin warnings nullable/xUnit; data itera 2 filas. **Verificación**: `dotnet build tests/SGV.Tests/SGV.Tests.csproj`.

- [x] 1.2 RED — Test del helper en `HttpClientExceptionScenariosTests.cs`: confirma 2 filas con tipos esperados, `NewHandlerThrowing` invoca la factory en `SendAsync`, y `RecordingHandler` captura `LastRequest`.
  - **Archivos**: `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenariosTests.cs` (nuevo). **Estimación**: +25/+35. **Depende de**: 1.1. **Verificación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~HttpClientExceptionScenariosTests"`.

## Fase 2: HabilidadApiClientTests (RED)

- [x] 2.1 REFACTOR — Reemplazar el `StubHandler` privado por `HttpClientExceptionScenarios.NewRecordingHandler(...)`; agregar `using SGV.Tests.Web._Shared;`.
  - **Archivos**: `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs`. **Estimación**: +5/+10, −15/−20. **Criterio**: suite existente verde sin cambios semánticos. **Depende de**: 1.1. **Verificación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadApiClientTests"`.

- [x] 2.2 RED — `[Theory] QueryAsync_TransportFails_PropagatesNativeException` con `[MemberData(nameof(...), MemberType = typeof(...))]`. Arma `HabilidadApiClient` con `NewHandlerThrowing(...)` y `BaseAddress=https://api.test`, llama `QueryAsync(new HabilidadListQuery(1,20,null,null,null))` y asserta `await Assert.ThrowsAsync(expectedExceptionType, ...)`.
  - **Archivos**: `HabilidadApiClientTests.cs`. **Estimación**: +15/+25. **Criterio**: verde contra el cliente actual; falla si el cliente capturara la excepción. **Depende de**: 2.1. **Verificación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadApiClientTests.QueryAsync_TransportFails_PropagatesNativeException"`.

- [x] 2.3 RED — `[Fact] QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest`. Usa `NewRecordingHandler`, pasa `new CancellationToken(canceled: true)`, asserta `await Assert.ThrowsAnyAsync<OperationCanceledException>(...)` y `Assert.Null(handler.LastRequest)`.
  - **Archivos**: `HabilidadApiClientTests.cs`. **Estimación**: +12/+18. **Criterio**: `LastRequest == null`. **Depende de**: 2.2. **Verificación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest"`.

## Fase 3: CargoApiClientTests (RED)

- [x] 3.1 REFACTOR — Reemplazar el `StubHandler` privado en `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` por el helper compartido.
  - **Archivos**: `CargoApiClientTests.cs`. **Estimación**: +5/+10, −15/−20. **Depende de**: 2.3. **Verificación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoApiClientTests"`.

- [x] 3.2 RED — `[Theory] QueryAsync_TransportFails_PropagatesNativeException` sobre `CargoApiClient.QueryAsync(new CargoListQuery(1,20,null,null,null))`.
  - **Archivos**: `CargoApiClientTests.cs`. **Estimación**: +15/+25. **Depende de**: 3.1. **Verificación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoApiClientTests.QueryAsync_TransportFails_PropagatesNativeException"`.

- [x] 3.3 RED — `[Fact] QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` sobre `CargoApiClient`.
  - **Archivos**: `CargoApiClientTests.cs`. **Estimación**: +12/+18. **Depende de**: 3.2. **Verificación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~CargoApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest"`.

## Fase 4: Verificación

- [x] 4.1 `dotnet build SGV.slnx` sin warnings nuevos.
- [x] 4.2 `dotnet test SGV.slnx`: los 4 tests nuevos verdes; los 12 fallos de `OcupacionRepositoryTests` (issue #59) siguen como baseline conocido no bloqueante.
