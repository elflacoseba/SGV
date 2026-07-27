# Delta para Web ApiClient Transport Contract

> Delta introducida por el change `2026-07-27-cambiar-contrasena-usuario-logueado`
> (issue #204). Esta delta agrega el método autenticado
> `IAuthApiClient.ChangePasswordAsync` y su implementación en
> `AuthApiClient`. Los requisitos previos (transporte nativo,
> `CommandResultMapper` excepto para `AuthApiClient`, anónimos para
> `ForgotPasswordAsync`/`ResetPasswordAsync`) NO se modifican.

## ADDED Requirements

### Requirement: `IAuthApiClient.ChangePasswordAsync` es autenticado y delega en `httpClient` (no en `anonymousHttpClient`)

`AuthApiClient.ChangePasswordAsync` MUST ser invocado a través del pipeline
HTTP **autenticado** (con `ApiBearerTokenHandler`). El cliente MUST añadir
el header `Authorization: Bearer <jwt>` provisto por el handler cuando el
ticket de cookie vigente expone el access_token, y MUST propagar
`HttpRequestException` con `StatusCode == 401` cuando el contexto no trae
bearer o la cookie venció. La operación MUST NO usar
`CommandResultMapper.Map` (la familia `AuthApiClient` permanece exceptuada
del mapper común, en línea con el requisito vigente
"Clientes HTTP administrativos usan `CommandResultMapper`" — escenario
"`AuthApiClient` queda exceptuado").

#### Scenario: CambioPasswordAsync envía Authorization Bearer al endpoint

- **GIVEN** `AuthApiClient.ChangePasswordAsync` configurado para usar el
  `httpClient` autenticado
- **AND** el usuario tiene una cookie vigente con access_token
- **WHEN** el PageModel invoca el método con un `ChangePasswordRequest`
- **THEN** el request saliente MUST contener `Authorization: Bearer <jwt>`
  provisto por `ApiBearerTokenHandler`
- **AND** MUST apuntar a `POST AuthApiRoutes.ChangePassword`.

#### Scenario: CambioPasswordAsync propaga 401 cuando la cookie venció

- **GIVEN** `AuthApiClient.ChangePasswordAsync` invocado desde un contexto
  sin bearer (cookie vencida o ausente)
- **WHEN** el pipeline HTTP retorna `401 Unauthorized` desde la API
- **THEN** la operación MUST propagar `HttpRequestException` con
  `StatusCode == 401` para que el PageModel distinga "sesión vencida"
  del resto de los outcomes.

#### Scenario: CambioPasswordAsync delega en `httpClient` autenticado, no en `anonymousHttpClient`

- **GIVEN** la implementación de `AuthApiClient`
- **WHEN** un test inspecciona el campo o factory usado por
  `ChangePasswordAsync`
- **THEN** MUST estar construido sobre el `httpClient` autenticado
- **AND** MUST NO usar `anonymousHttpClient`.

### Requirement: `ChangePasswordAsync` traduce respuestas HTTP a `ChangePasswordOutcome`

`AuthApiClient.ChangePasswordAsync` MUST traducir las respuestas HTTP del
endpoint `POST /api/v1/auth/change-password` a
`ChangePasswordOutcome` con la siguiente matriz:

| Status HTTP | `ChangePasswordOutcome` |
|-------------|--------------------------|
| `200 OK` (2xx) | `Success` |
| `400 Bad Request` | `InvalidCurrentPassword` |
| `429 Too Many Requests` | `RateLimited` |
| Otro no 2xx | `HttpRequestException` propagada con `StatusCode` preservado |

La implementación MUST preservar el `StatusCode` en `HttpRequestException`
cuando esté disponible (en particular `429` ya está mapeado pero un
`5xx` debe propagarse como `HttpRequestException(5xx)`). MUST propagar
`TaskCanceledException` nativa si el `CancellationToken` no fue cancelado
por el caller. MUST respetar un `CancellationToken` pre-cancelado y
MUST NO iniciar el envío HTTP cuando la cancelación ya fue solicitada.

#### Scenario: `ChangePasswordAsync` 200 → `Success`

- **GIVEN** la API responde `200 OK` al POST
- **WHEN** `ChangePasswordAsync` procesa la respuesta
- **THEN** MUST retornar `ChangePasswordOutcome.Success`.

#### Scenario: `ChangePasswordAsync` 400 → `InvalidCurrentPassword`

- **GIVEN** la API responde `400 Bad Request` al POST (sea por
  `CurrentPassword` incorrecta, por `NewPassword` que no cumple la
  política, o por `ConfirmPassword != NewPassword`)
- **WHEN** `ChangePasswordAsync` procesa la respuesta
- **THEN** MUST retornar `ChangePasswordOutcome.InvalidCurrentPassword`.

#### Scenario: `ChangePasswordAsync` 429 → `RateLimited`

- **GIVEN** la API responde `429 Too Many Requests` al POST
- **WHEN** `ChangePasswordAsync` procesa la respuesta
- **THEN** MUST retornar `ChangePasswordOutcome.RateLimited`
- **AND** MUST NO lanzar `HttpRequestException` para `429` (el
  PageModel lo distingue vía `RateLimited`).

#### Scenario: `ChangePasswordAsync` 5xx propaga `HttpRequestException` con `StatusCode`

- **GIVEN** la API responde `5xx` (por ejemplo `500 Internal Server Error`)
- **WHEN** `ChangePasswordAsync` procesa la respuesta
- **THEN** MUST lanzar `HttpRequestException` con `StatusCode == 5xx`
- **AND** la excepción MUST NO swallowearse en el cliente.

#### Scenario: `ChangePasswordAsync` propaga `TaskCanceledException` no-cancelled

- **GIVEN** el pipeline HTTP finaliza con `TaskCanceledException`
- **AND** el `CancellationToken` NO fue cancelado por el caller
- **WHEN** `ChangePasswordAsync` procesa la falla
- **THEN** la excepción MUST propagarse al PageModel sin swallow.

#### Scenario: `ChangePasswordAsync` respeta `CancellationToken` pre-cancelado

- **GIVEN** el caller pasa un `CancellationToken` ya cancelado
- **WHEN** se invoca `ChangePasswordAsync`
- **THEN** la operación MUST finalizar como cancelada
- **AND** el envío HTTP MUST NOT iniciarse.

### Requirement: `ChangePasswordAsync` mantiene la firma del contrato de transporte

`AuthApiClient.ChangePasswordAsync` MUST ser un método asíncrono
`Task<ChangePasswordOutcome>` con la firma
`ChangePasswordAsync(ChangePasswordRequest request,
CancellationToken cancellationToken = default)`. La firma MUST residir en
`SGV.Web/Integration/Auth/IAuthApiClient.cs` (interfaz) y
`SGV.Web/Integration/Auth/AuthApiClient.cs` (implementación). MUST NO
introducir un nuevo cliente HTTP paralelo ni un nuevo factory. La
excepción `HttpRequestException` propagada MUST preservar `StatusCode`
cuando esté disponible, en línea con el requisito vigente
"`ForgotPassword` / `ResetPassword` mantienen propagación de fallos
nativos de transporte" de esta misma spec.

#### Scenario: Firma pública expone solo `ChangePasswordAsync`

- **GIVEN** `IAuthApiClient`
- **WHEN** un test inspecciona sus métodos públicos
- **THEN** MUST contener `ChangePasswordAsync(ChangePasswordRequest,
  CancellationToken)`
- **AND** MUST NOT exponer overloads con tipos primitivos sueltos
  (no `ChangePasswordAsync(string, string, string)`).