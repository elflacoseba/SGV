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
