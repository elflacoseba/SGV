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

### Requirement: Clientes HTTP administrativos usan `CommandResultMapper`

Los clientes HTTP tipados administrativos de `SGV.Web`
(`HabilidadApiClient`, `CargoApiClient` para Cargo y CargoSkill,
`PuestosApiClient`, `UnidadOrganizativaApiClient`) MUST delegar la
clasificación de respuestas HTTP a `CommandResultMapper.Map` en lugar de
mantener matrices `status→categoría` privadas.

#### Scenario: Cliente administrativo usa el mapper común

- GIVEN cualquier cliente HTTP administrativo
- WHEN procesa una respuesta HTTP no exitosa
- THEN la categoría resultante MUST provenir de `CommandResultMapper.Map`
- AND el cliente MUST NO contener una matriz `switch` privada que duplique la del mapper.

#### Scenario: `AuthApiClient` queda exceptuado

- GIVEN `AuthApiClient.LoginAsync` y un backend que responde 401
- WHEN se procesa la respuesta
- THEN MUST retornar `null` sin pasar por `CommandResultMapper`.

### Requirement: `*DeleteResult` exponen `ErrorCategoria`

Los resultados de baja (`HabilidadDeleteResult`, `CargoDeleteResult`,
`PuestoDeleteResult`, `UnidadOrganizativaDeleteResult`,
`CargoSkillDeleteResult`) MUST exponer `Categoria: ErrorCategoria`
además de preservar `StatusCode` como metadata. `Succeeded` MUST ser
`true` solo cuando el código HTTP sea 204.

#### Scenario: Delete 409 produce `Categoria=Conflict`

- GIVEN un `HabilidadApiClient.DeleteAsync` con backend respondiendo 409
- WHEN se obtiene el `HabilidadDeleteResult`
- THEN MUST tener `Succeeded == false`, `Categoria == ErrorCategoria.Conflict` y `StatusCode == 409`.

#### Scenario: Delete 204 produce `Succeeded=true` sin `Categoria`

- GIVEN un `HabilidadApiClient.DeleteAsync` con backend respondiendo 204
- WHEN se obtiene el `HabilidadDeleteResult`
- THEN MUST tener `Succeeded == true`, `Categoria` igual al valor por defecto documentado y `StatusCode == 204`.

## ADDED Requirements

> Delta introducida por el change `2026-07-14-fix-126-operational-tech-debt` (issue #126). Verificado en `openspec/changes/archive/2026-07-14-fix-126-operational-tech-debt/verify-report.md`.

### Requirement: AuthApiClient timeout 10s

`AuthApiClient` MUST set `Timeout = TimeSpan.FromSeconds(10)` on its
inner `HttpClient`. This timeout governs the entire request lifecycle
including connection, sending, and receiving.

#### Scenario: AuthApiClient timeout raises TaskCanceledException

- **GIVEN** `AuthApiClient` is configured with `Timeout = 10s`
- **AND** the upstream API does not respond within 10 seconds
- **WHEN** `SignInModel.OnPostAsync` invokes `AuthApiClient.LoginAsync`
- **THEN** a `TaskCanceledException` MUST be thrown
- **AND** the exception MUST NOT be caught and swallowed by the client.

### Requirement: UnidadOrganizativaApiClient timeout 10s

`UnidadOrganizativaApiClient` MUST set `Timeout =
TimeSpan.FromSeconds(10)` on its inner `HttpClient`. This timeout
governs the entire request lifecycle including connection, sending,
and receiving.

#### Scenario: UnidadOrganizativaApiClient timeout raises TaskCanceledException

- **GIVEN** `UnidadOrganizativaApiClient` is configured with `Timeout = 10s`
- **AND** the upstream API does not respond within 10 seconds
- **WHEN** a consumer invokes any operation on `UnidadOrganizativaApiClient`
- **THEN** a `TaskCanceledException` MUST be thrown
- **AND** the exception MUST NOT be caught and swallowed by the client.

### Requirement: SignIn UX for transport exceptions

`SignInModel.OnPostAsync` MUST catch `HttpRequestException` and
display a Spanish error message: "No se pudo conectar con el
servidor. Verificá tu conexión y volvé a intentar." MUST catch
`TaskCanceledException` when the `CancellationToken` was NOT canceled
by the caller and display: "El servidor tardó demasiado en responder.
Volvé a intentar en unos segundos." When the `CancellationToken` IS
cancelled by the caller, `TaskCanceledException` MUST propagate
without being caught by this handler.

#### Scenario: HttpRequestException shows Spanish error message

- **GIVEN** `SignInModel.OnPostAsync` attempts to call `AuthApiClient.LoginAsync`
- **AND** the API is unreachable (network failure, DNS resolution failure, connection refused)
- **WHEN** `AuthApiClient.LoginAsync` throws `HttpRequestException`
- **THEN** the page MUST render with a Spanish error message: "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar."
- **AND** the user MUST remain on the sign-in page (no redirect to `/Error`).

#### Scenario: TaskCanceledException (non-CT) shows timeout message

- **GIVEN** `SignInModel.OnPostAsync` attempts to call `AuthApiClient.LoginAsync`
- **AND** the API does not respond within the client timeout (10s)
- **AND** the `CancellationToken` was NOT cancelled by the caller
- **WHEN** `AuthApiClient.LoginAsync` throws `TaskCanceledException`
- **THEN** the page MUST render with a Spanish error message: "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos."
- **AND** the user MUST remain on the sign-in page (no redirect to `/Error`).

## ADDED Requirements

> Delta introducida por el change `2026-07-21-password-reset-181` (issue #181). Verificado en `openspec/changes/archive/2026-07-21-password-reset-181/verify-report.md`. Las pantallas de recuperación son anónimas y el cliente HTTP NO debe añadir el bearer header. Los flujos autenticados del resto del contrato (`*ApiClient` administrativos, `LoginAsync`) NO se modifican.

### Requirement: `IAuthApiClient.ForgotPasswordAsync` y `ResetPasswordAsync` son anónimos

`AuthApiClient.ForgotPasswordAsync` y `AuthApiClient.ResetPasswordAsync` MUST ser invocados **sin** atravesar `ApiBearerTokenHandler`. El cliente MUST NO añadir el header `Authorization: Bearer <jwt>` a estos request, y MUST NO lanzar `InvalidOperationException` cuando el ticket de cookie esté ausente o haya expirado.

#### Scenario: ForgotPassword sin Authorization header

- **GIVEN** `AuthApiClient.ForgotPasswordAsync` is configured to skip the bearer handler
- **AND** the user has no active cookie or the cookie has expired
- **WHEN** `ForgotPasswordModel.OnPostAsync` invokes the method
- **THEN** the outbound HTTP request MUST NOT contain an `Authorization` header
- **AND** the call MUST NOT throw `InvalidOperationException` due to a missing bearer token.

#### Scenario: ResetPassword sin Authorization header

- **GIVEN** `AuthApiClient.ResetPasswordAsync` is configured to skip the bearer handler
- **AND** the user has no active cookie or the cookie has expired
- **WHEN** `ResetPasswordModel.OnPostAsync` invokes the method
- **THEN** the outbound HTTP request MUST NOT contain an `Authorization` header
- **AND** the call MUST NOT throw `InvalidOperationException` due to a missing bearer token.

### Requirement: ForgotPassword / ResetPassword mantienen propagación de fallos nativos de transporte

Los métodos `ForgotPasswordAsync` y `ResetPasswordAsync` de `AuthApiClient` MUST propagar `TaskCanceledException` y `HttpRequestException` sin traducirlas a resultados funcionales, en línea con el requisito **"Propagar fallos nativos de transporte"** de esta misma spec. La excepción `HttpRequestException` MUST preservar el `StatusCode` cuando esté disponible (en particular `429`) para que los PageModels puedan discriminar el mensaje.

#### Scenario: `429` se propaga como HttpRequestException con StatusCode

- **GIVEN** the upstream API responds `429 Too Many Requests` to `forgot-password`
- **WHEN** `AuthApiClient.ForgotPasswordAsync` returns from the HTTP call
- **THEN** the page model MUST observe an `HttpRequestException` whose `StatusCode == 429`
- **AND** the exception MUST NOT be swallowed by the client.

#### Scenario: Cancelación previa del token

- **GIVEN** the caller passes a pre-cancelled `CancellationToken` to `AuthApiClient.ForgotPasswordAsync` or `ResetPasswordAsync`
- **WHEN** the client executes the call
- **THEN** the operation MUST finalizar como cancelada
- **AND** the HTTP send MUST NOT initiate.

### Requirement: ForgotPassword / ResetPassword exceptuadas de `CommandResultMapper`

`AuthApiClient.ForgotPasswordAsync` y `AuthApiClient.ResetPasswordAsync` MUST NO delegar la clasificación de respuestas en `CommandResultMapper.Map`, alineadas con la excepción vigente para `AuthApiClient.LoginAsync` (esta misma spec, requisito **"Clientes HTTP administrativos usan `CommandResultMapper`"** — escenario "`AuthApiClient` queda exceptuado"). Los PageModels discriminan el resultado por el `StatusCode` del `HttpRequestException` o por el código de retorno.

#### Scenario: Mapper común no se invoca para recovery

- **GIVEN** `ForgotPassword` o `ResetPassword` produce cualquier respuesta HTTP no exitosa (`4xx`, `5xx`)
- **WHEN** el cliente procesa la respuesta
- **THEN** MUST NO llamar a `CommandResultMapper.Map`
- **AND** MUST devolver el control al PageModel mediante excepción nativa o respuesta cruda, según corresponda.
