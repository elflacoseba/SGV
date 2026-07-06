# Especificación de contrato de transporte para API clients web

## Purpose

Definir el contrato transversal de propagación de fallos de transporte y cancelación cooperativa para los clientes HTTP tipados de `SGV.Web`.

## Requirements

### Requirement: Propagar fallos nativos de transporte

Los clientes HTTP tipados de `SGV.Web` MUST propagar `TaskCanceledException` y `HttpRequestException` originadas por `HttpClient` o su pipeline sin traducirlas a resultados funcionales.

#### Scenario: Cancelación o timeout del transporte

- GIVEN una operación de un cliente HTTP tipado en ejecución
- WHEN el pipeline HTTP finaliza con `TaskCanceledException`
- THEN el consumidor MUST recibir esa excepción nativa
- AND la operación MUST NOT devolverse como un resultado de negocio.

#### Scenario: Falla de conectividad

- GIVEN una operación de un cliente HTTP tipado en ejecución
- WHEN el pipeline HTTP finaliza con `HttpRequestException`
- THEN el consumidor MUST recibir esa excepción nativa.

### Requirement: Respetar cancelación cooperativa del consumidor

Los clientes HTTP tipados de `SGV.Web` MUST respetar un `CancellationToken` pre-cancelado y MUST NOT iniciar el envío HTTP cuando la cancelación ya fue solicitada.

#### Scenario: Token pre-cancelado

- GIVEN un consumidor entrega un `CancellationToken` ya cancelado
- WHEN invoca una operación de un cliente HTTP tipado
- THEN la operación MUST finalizar como cancelada
- AND el envío HTTP MUST NOT iniciarse.

### Requirement: IPuestosApiClient propaga fallos nativos de transporte

`IPuestosApiClient` (cliente HTTP tipado de `SGV.Web` para `Puestos`) MUST propagar `TaskCanceledException` y `HttpRequestException` originadas por `HttpClient` o su pipeline sin traducirlas a resultados funcionales.

#### Scenario: Cancelación o timeout del transporte en Puestos

- GIVEN una operación de `IPuestosApiClient` en ejecución (`GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ReactivateAsync`)
- WHEN el pipeline HTTP finaliza con `TaskCanceledException`
- THEN el consumidor MUST recibir esa excepción nativa
- AND la operación MUST NOT devolverse como un resultado de negocio (`PuestoCommandResult`/`PuestoDeleteResult`).

#### Scenario: Falla de conectividad en Puestos

- GIVEN una operación de `IPuestosApiClient` en ejecución
- WHEN el pipeline HTTP finaliza con `HttpRequestException`
- THEN el consumidor MUST recibir esa excepción nativa.

### Requirement: IPuestosApiClient respeta cancelación cooperativa

`IPuestosApiClient` MUST respetar un `CancellationToken` pre-cancelado y MUST NOT iniciar el envío HTTP cuando la cancelación ya fue solicitada.

#### Scenario: Token pre-cancelado en Puestos

- GIVEN un consumidor entrega un `CancellationToken` ya cancelado
- WHEN invoca una operación de `IPuestosApiClient`
- THEN la operación MUST finalizar como cancelada
- AND el envío HTTP MUST NOT iniciarse.

### Requirement: IPuestosApiClient traduce ProblemDetails a resultados tipados

`PuestosApiClient` MUST traducir `ValidationProblemDetails` (HTTP 400) a `PuestoCommandResult.Failure` con `FieldErrors` por input, y `ProblemDetails` o códigos de error (HTTP 409) a `PuestoCommandResult.Failure(Code=...)` con códigos tales como `CodigoDuplicado`, `UnidadOrganizativaNoExiste`, `CargoNoExiste`, `PuestoSuperiorNoExiste`, `PuestoSuperiorInvalido`. `DeleteAsync` MUST traducir errores HTTP a `PuestoDeleteResult.Failure(Code=...)` (no `PuestoCommandResult`).

#### Scenario: 400 con FieldErrors en Create o Update

- GIVEN un POST contra `PuestosApiClient.CreateAsync` o `UpdateAsync`
- WHEN el backend responde 400 con `ValidationProblemDetails`
- THEN el resultado MUST ser `PuestoCommandResult.Failure(FieldErrors)` con claves en camelCase (`codigo`, `nombre`, `unidadOrganizativaId`, `cargoId`, `puestoSuperiorId`)
- AND MUST preservar los códigos `CodigoDuplicado`, `UnidadOrganizativaNoExiste`, `CargoNoExiste`, `PuestoSuperiorNoExiste`, `PuestoSuperiorInvalido` en respuestas 409.

#### Scenario: 409 por Codigo duplicado o Puesto superior inválido

- GIVEN un POST contra `CreateAsync`, `UpdateAsync` o `ReactivateAsync`
- WHEN el backend responde 409 con `ProblemDetails`
- THEN MUST mapear a `PuestoCommandResult.Failure(Code=...)` con `Code` igual al emitido por el backend (`CodigoDuplicado`, `PuestoSuperiorInvalido`, etc.).

#### Scenario: Delete mapea a PuestoDeleteResult

- GIVEN un DELETE contra `DeleteAsync`
- WHEN el backend responde 204, 404 o 409
- THEN MUST traducir a `PuestoDeleteResult` (no a `PuestoCommandResult`)
- AND `Succeeded` MUST ser `true` solo cuando el código HTTP sea 204.
