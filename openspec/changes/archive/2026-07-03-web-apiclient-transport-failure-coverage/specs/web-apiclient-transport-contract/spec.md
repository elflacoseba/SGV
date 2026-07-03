# Delta for web-apiclient-transport-contract

## ADDED Requirements

### Requirement: Aplicar el contrato de transporte a HabilidadApiClient y CargoApiClient

El sistema MUST garantizar este contrato de transporte en `HabilidadApiClient` y `CargoApiClient`, cubriendo propagación de `TaskCanceledException`, propagación de `HttpRequestException` y respeto del `CancellationToken` pre-cancelado.

#### Scenario: HabilidadApiClient propaga excepciones nativas de transporte

- GIVEN `HabilidadApiClient` ejecuta una operación HTTP
- WHEN el transporte falla con `TaskCanceledException` o `HttpRequestException`
- THEN la operación MUST propagar la excepción nativa sin traducción.

#### Scenario: CargoApiClient propaga excepciones nativas de transporte

- GIVEN `CargoApiClient` ejecuta una operación HTTP
- WHEN el transporte falla con `TaskCanceledException` o `HttpRequestException`
- THEN la operación MUST propagar la excepción nativa sin traducción.

#### Scenario: Ambos clientes respetan token pre-cancelado

- GIVEN un `CancellationToken` ya cancelado
- WHEN se invoca una operación de `HabilidadApiClient` o `CargoApiClient`
- THEN la operación MUST finalizar como cancelada
- AND el envío HTTP MUST NOT iniciarse.
