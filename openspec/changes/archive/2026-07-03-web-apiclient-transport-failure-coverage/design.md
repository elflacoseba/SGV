# Design: Cobertura de fallos de transporte en API clients web

## 1. Resumen / contexto

Issue: #78. Proposal: `openspec/changes/web-apiclient-transport-failure-coverage/proposal.md`. Baseline: `openspec/specs/web-apiclient-transport-contract/spec.md`. Delta: `openspec/changes/web-apiclient-transport-failure-coverage/specs/web-apiclient-transport-contract/spec.md`.

El gap actual es claro: `HabilidadApiClientTests` y `CargoApiClientTests` cubren rutas felices y status codes, pero no fijan por tests la propagación de `TaskCanceledException`, `HttpRequestException` ni el respeto de un `CancellationToken` pre-cancelado. El diseño agrega esa cobertura sin tocar producción y sin expandir scope a otros clientes.

## 2. Enfoque técnico

Se mantendrá el seam real `HttpClient` + `HttpMessageHandler` ya usado en ambas suites. El cambio crea un helper compartido mínimo en `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` y agrega tests nuevos sobre métodos reales de `HabilidadApiClient` y `CargoApiClient`, evitando wrappers que oculten el contrato observado.

## 3. Decisiones de diseño

| Decisión | Elección | Rationale |
|---|---|---|
| Forma del helper | Clase estática `HttpClientExceptionScenarios` | Reusa infraestructura entre suites sin introducir herencia/base classes en tests. El helper expone datos y handlers; los tests siguen llamando al cliente real. |
| Dataset parametrizado | `IEnumerable<object[]> TransportExceptionData` con `string scenario`, `Func<Exception> exceptionFactory`, `Type expectedExceptionType` | En xUnit 2.9.2 `MemberData` con `IEnumerable<object[]>` es estable; evita depender de `Activator.CreateInstance(type)` y sus fallos para excepciones sin ctor vacío. |
| Granularidad | Un `[Theory]` por cliente sobre `QueryAsync` + un `[Fact]` por cliente para token pre-cancelado | Sigue la filosofía del repo: pocos tests de alto valor. Hoy los 8 métodos delegan al mismo `HttpClient` sin capturar excepciones de transporte antes del response. Repetir 16 casos sería ruido sin señal extra. |
| Naming | Mantener `MethodName_Condition_ExpectedResult` | Preserva consistencia con ambas suites y reduce costo cognitivo de review. |
| Timeout | Documentar el desacople con `Program.cs` | El timeout de 10s vive en DI (`Program.cs`) pero estos tests construyen `HttpClient` manualmente; por eso se simula `TaskCanceledException` en el handler en vez de esperar timeouts reales. |
| Aserciones | `Assert.ThrowsAsync(expectedExceptionType, ...)` para transporte; `Assert.ThrowsAnyAsync<OperationCanceledException>(...)` para token pre-cancelado | La spec exige cancelación observable; `TaskCanceledException` es una subclase válida de `OperationCanceledException`. |
| Anti-drift | El helper solo fabrica handlers/datos; nunca ejecuta operaciones del cliente | Si el contrato cambia, fallan tests sobre `HabilidadApiClient.QueryAsync` y `CargoApiClient.QueryAsync`, no sobre abstracciones artificiales. |

## 4. Flujo de prueba

```text
Theory/Fact -> shared handler factory -> HttpClient(BaseAddress=https://api.test)
            -> HabilidadApiClient / CargoApiClient -> HttpClient pipeline
            -> excepción nativa o cancelación observable
```

## 5. Diseño del helper compartido

Archivo nuevo: `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs`

```csharp
namespace SGV.Tests.Web._Shared;

public static class HttpClientExceptionScenarios
{
    public static IEnumerable<object[]> TransportExceptionData =>
    [
        ["TaskCanceled", () => new TaskCanceledException("Simulated timeout"), typeof(TaskCanceledException)],
        ["HttpRequest", () => new HttpRequestException("Simulated transport failure"), typeof(HttpRequestException)]
    ];

    public static HttpMessageHandler NewHandlerThrowing(Func<Exception> exceptionFactory);

    public static RecordingHandler NewRecordingHandler();

    public sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; }
    }
}
```

Uso previsto:

```csharp
[Theory]
[MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
public async Task QueryAsync_TransportFails_PropagatesNativeException(
    string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
```

## 6. Diseño de tests por cliente

### `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs`
- `QueryAsync_TransportFails_PropagatesNativeException`
- `QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest`

### `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs`
- `QueryAsync_TransportFails_PropagatesNativeException`
- `QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest`

Propósito: cubrir los 3 caminos requeridos con 4 tests nuevos totales. `QueryAsync` se elige como método representativo porque ambos clientes construyen URI, llaman `GetAsync`, hacen `EnsureSuccessStatusCode` y deserializan sin capturar excepciones de transporte en ese tramo.

## 7. Cambios de archivos

| Archivo | Acción | Descripción |
|---|---|---|
| `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` | Crear | Dataset `MemberData`, handler lanzador y handler recorder. |
| `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` | Modificar | Reusar helper y agregar cobertura de transporte/cancelación. |
| `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` | Modificar | Cobertura equivalente para el cliente hermano. |

## 8. Estimación de líneas

- `_Shared/HttpClientExceptionScenarios.cs`: +45 a +65
- `HabilidadApiClientTests.cs`: +20 a +30, -15 a -20 (remover `StubHandler` local)
- `CargoApiClientTests.cs`: +20 a +30, -15 a -20 (remover `StubHandler` local)

Estimado total del diff: **90 a 130 líneas**, por debajo del presupuesto de 400; no requiere chained PR.

## 9. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| `InlineData(typeof(...))` obliga a instanciación frágil | Usar `MemberData` con `Func<Exception>`. |
| Confusión `TaskCanceledException` vs `OperationCanceledException` | Aserción exacta para transporte simulado y `ThrowsAnyAsync<OperationCanceledException>` para token pre-cancelado. |
| Falso verde por testear el helper en vez del cliente | Los tests invocan `QueryAsync` real; el helper no envuelve operaciones. |

## 10. Estrategia de verificación

- Ejecutar `dotnet test SGV.slnx`.
- Verificar específicamente los 4 tests nuevos en `HabilidadApiClientTests` y `CargoApiClientTests`.
- Confirmar que el caso pre-cancelado deja `handler.LastRequest` en `null`.

## 11. Compatibilidad con baseline conocido

Este change no toca persistencia ni MySQL. Si `dotnet test SGV.slnx` sigue mostrando los 12 failures conocidos de `OcupacionRepositoryTests` (issue #59), se consideran baseline no bloqueante para esta cobertura web.

## 12. Open questions

- Ninguna bloqueante para pasar a `sdd-tasks`.
