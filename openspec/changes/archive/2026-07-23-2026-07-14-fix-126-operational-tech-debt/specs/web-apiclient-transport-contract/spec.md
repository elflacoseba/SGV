# Delta para `web-apiclient-transport-contract`

Delta del change `2026-07-14-fix-126-operational-tech-debt` (issue
#126): agrega el presupuesto de timeout de los typed clients de
autenticación y unidades organizativas, y la frontera de UX de
`SignInModel` cuando esos clientes propagan excepciones. No modifica
los requirements existentes; solo agrega obligaciones explícitas.

Trazabilidad: AC-1, AC-2, AC-3 del proposal.

## ADDED Requirements

### Requirement: `AuthApiClient` y `UnidadOrganizativaApiClient` usan `Timeout = 10s`

`SGV.Web` MUST registrar los typed clients `IAuthApiClient` y
`IUnidadOrganizativaApiClient` con `HttpClient.Timeout = TimeSpan.FromSeconds(10)`,
alineándolos con `CargoApiClient`, `PuestosApiClient` y `HabilidadApiClient`.
El override equivalente en `SgvWebApplicationFactory` MUST reflejar el
mismo presupuesto.

#### Scenario: Timeout efectivo de 10 s en los typed clients

- **GIVEN** la composición de `SGV.Web` (`src/SGV.Web/Program.cs:72-84`)
- **WHEN** se resuelve cada `HttpClient` del contenedor de DI
- **THEN** la propiedad `Timeout` MUST ser `TimeSpan.FromSeconds(10)`
- **AND** MUST NO ser el default de plataforma (100 s).

#### Scenario: Upstream lento dispara `TaskCanceledException` antes de 10 s

- **GIVEN** un upstream que demora más de 10 s
- **WHEN** se invoca `AuthApiClient.LoginAsync`
- **THEN** la operación MUST finalizar con `TaskCanceledException` antes de los 10 s ± tolerancia
- **AND** MUST NOT esperar el presupuesto de 100 s.

### Requirement: `SignInModel` traduce `HttpRequestException` a error de UI en español

Cuando `AuthApiClient.LoginAsync` propaga `HttpRequestException`,
`SignInModel.OnPostAsync` MUST capturarla, agregar un único mensaje en
español a `ModelState` con clave `string.Empty`, retornar la misma
página y MUST NOT propagar al pipeline `UseExceptionHandler`. Esto es
coherente con la regla vigente "Propagar fallos nativos de transporte":
el cliente propaga y el consumidor define la UX.

#### Scenario: Error de transporte se traduce a `ModelState`

- **GIVEN** que `AuthApiClient.LoginAsync` lanza `HttpRequestException`
- **WHEN** `SignInModel.OnPostAsync` procesa el POST
- **THEN** `ModelState` MUST contener un error en español con clave `string.Empty`
- **AND** la página MUST permanecer `/auth/sign-in` con `validation-summary ModelOnly`
- **AND** la excepción MUST NO propagarse al pipeline global.

#### Scenario: 401 sigue siendo `null` y error de credenciales inválidas

- **GIVEN** que la API responde `401 Unauthorized`
- **WHEN** `SignInModel.OnPostAsync` procesa el POST
- **THEN** `ModelState` MUST contener el mensaje "Credenciales inválidas."
- **AND** la rama `HttpRequestException` MUST NOT activarse para 401.

### Requirement: `SignInModel` traduce `TaskCanceledException` preservando cancelación cooperativa

Cuando `AuthApiClient.LoginAsync` propaga `TaskCanceledException` y el
`CancellationToken` del request NO está cancelado, `SignInModel.OnPostAsync`
MUST capturarla, agregar un mensaje en español a `ModelState` indicando
timeout, retornar la página y MUST NOT propagar. Cuando el token SÍ
está cancelado, la excepción MUST propagarse.

#### Scenario: Timeout de upstream se traduce a error de UI

- **GIVEN** que la API demora más de 10 s y `LoginAsync` lanza `TaskCanceledException`
- **AND** el `CancellationToken` del request NO está cancelado
- **WHEN** `SignInModel.OnPostAsync` procesa el POST
- **THEN** `ModelState` MUST contener un error en español indicando timeout
- **AND** la página MUST permanecer `/auth/sign-in`
- **AND** la excepción MUST NO propagarse al pipeline global.

#### Scenario: Cancelación del cliente se respeta

- **GIVEN** un `CancellationToken` pre-cancelado
- **WHEN** `AuthApiClient.LoginAsync` lanza `TaskCanceledException` por cancelación del token
- **THEN** la excepción MUST propagarse al pipeline
- **AND** MUST NO agregarse ningún mensaje a `ModelState`
- **AND** MUST NO capturarse como timeout.