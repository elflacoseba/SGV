# Proposal: Hardening de cobertura para fallos de transporte en clientes HTTP web

## Intent

Cubrir un gap de regresión en `HabilidadApiClient` y `CargoApiClient`: hoy los tests validan rutas felices y traducción de status codes, pero no fijan la propagación de `TaskCanceledException`, `HttpRequestException` ni la cancelación cooperativa que ya forma parte del contrato operativo documentado en `Program.cs`.

## Scope

### In Scope
- Agregar cobertura en `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` para `TaskCanceledException`, `HttpRequestException` y `CancellationToken` pre-cancelado.
- Agregar la misma cobertura en `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs`.
- Crear `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` para reutilizar escenarios de excepciones nativas de `HttpClient` entre ambos clientes.
- Mantener el alcance en los clientes tipados reales (`HttpClient` + `HttpMessageHandler`), no en los fakes de página.

### Out of Scope / Non-goals
- `AuthApiClient` y `UnidadOrganizativaApiClient`.
- Tests de timeout real basados en `Task.Delay` + `Timeout` reducido.
- Cambios funcionales en `src/SGV.Web/Integration/` o en `src/SGV.Web/Program.cs`.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- None. El change endurece cobertura de tests sobre comportamiento ya existente; no introduce requisitos funcionales nuevos.

## Approach

Extender los tests unitarios existentes con `[Theory]` para excepciones de transporte nativas y `[Fact]` para cancelación previa, reutilizando un handler lanzador compartido. La propuesta preserva strict TDD, minimiza duplicación y evita ampliar el scope a otros clientes o a pruebas temporales frágiles.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` | Modified | Nuevos casos de propagación y cancelación. |
| `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` | Modified | Casos equivalentes para el cliente hermano. |
| `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs` | New | Helper reutilizable para handlers que lanzan excepciones. |
| `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs` | Consulted | Evidencia del gap: intercepta antes del `HttpClient`. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Duplicar mecánica entre suites | Media | Centralizar escenarios compartidos en `_Shared/`. |
| Sobre-especificar detalles internos | Media | Validar sólo propagación observable y no wrappers internos. |
| Falsa cobertura si se usa el fake equivocado | Baja | Mantener tests sobre `HttpMessageHandler` real del cliente tipado. |

## Rollback Plan

Revertir los tests nuevos y el helper compartido sin tocar producción; no hay migraciones ni cambios de contrato externos.

## Dependencies

- `src/SGV.Web/Program.cs`
- `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs`
- `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs`
- Issue #78

## Success Criteria

- [ ] `HabilidadApiClientTests` cubre `TaskCanceledException`, `HttpRequestException` y token pre-cancelado sin invocar handler.
- [ ] `CargoApiClientTests` cubre los mismos escenarios.
- [ ] El helper compartido vive en `tests/SGV.Tests/Web/_Shared/HttpClientExceptionScenarios.cs`.
- [ ] `dotnet test SGV.slnx` sigue verde salvo el baseline conocido de `OcupacionRepositoryTests` issue #59.
