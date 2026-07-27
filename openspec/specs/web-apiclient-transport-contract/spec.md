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

## ADDED Requirements

> Delta introducida por el change `migrar-campo-categoria-habilidades-a-tabla`. Define el contrato del cliente HTTP tipado read-only de `CategoriasHabilidad` y la traducción de sus errores via `CommandResultMapper`.

### Requirement: `ICategoriaHabilidadApiClient` es read-only y delega a `CommandResultMapper`

`SGV.Web` MUST exponer `ICategoriaHabilidadApiClient` con dos operaciones read-only: `GetAllAsync(CancellationToken)` y `GetByIdAsync(Guid id, CancellationToken)`. El cliente MUST delegar la clasificación de respuestas HTTP en `CommandResultMapper.Map` para producir el resultado tipado (mismo seam que el resto de los `*ApiClient` administrativos). El cliente MUST NOT exponer operaciones de escritura (`POST`, `PUT`, `PATCH`, `DELETE`). El cliente MUST propagar `HttpRequestException` y `TaskCanceledException` nativas, sin traducirlas a resultados funcionales. El cliente MUST respetar un `CancellationToken` pre-cancelado y MUST NOT iniciar el envío HTTP cuando la cancelación ya fue solicitada.

#### Scenario: `GetAllAsync` con catálogo poblado

- **GIVEN** `ICategoriaHabilidadApiClient.GetAllAsync`
- **AND** el backend responde `200 OK` con un array JSON de 4 categorías
- **WHEN** el `PageModel` de Crear o Editar Habilidad lo invoca
- **THEN** el resultado MUST ser `IReadOnlyList<CategoriaHabilidadDto>` con 4 elementos
- **AND** cada elemento MUST exponer `Id`, `Codigo` y `Nombre` consumer-safe.

#### Scenario: `GetByIdAsync` con id existente

- **GIVEN** `ICategoriaHabilidadApiClient.GetByIdAsync(<guid>)`
- **AND** el backend responde `200 OK` con la categoría solicitada
- **WHEN** se completa la llamada
- **THEN** el resultado MUST ser `CategoriaHabilidadDto?` con el id consultado
- **AND** MUST exponer `Codigo` y `Nombre`.

#### Scenario: `GetByIdAsync` con id inexistente responde 404 → tipado

- **GIVEN** el backend responde `404 Not Found` para `GET /api/v1/categorias-habilidad/<guid-fake>`
- **WHEN** se invoca `GetByIdAsync(<guid-fake>)`
- **THEN** el resultado MUST ser `null` (recurso inexistente)
- **AND** MUST NO lanzar excepción.

#### Scenario: Backend no disponible no se traduce a resultado de negocio

- **GIVEN** `ICategoriaHabilidadApiClient.GetAllAsync`
- **AND** el pipeline HTTP finaliza con `HttpRequestException` (DNS, conexión rechazada)
- **WHEN** se invoca la operación
- **THEN** la excepción MUST propagarse al consumidor (por ejemplo, el `PageModel`)
- **AND** MUST NOT devolverse como un resultado funcional.

#### Scenario: Cancelación o timeout del transporte

- **GIVEN** una operación de `ICategoriaHabilidadApiClient` en ejecución
- **WHEN** el pipeline HTTP finaliza con `TaskCanceledException`
- **THEN** la excepción MUST propagarse al consumidor
- **AND** la operación MUST NOT devolverse como un resultado funcional.

#### Scenario: Token pre-cancelado

- **GIVEN** un consumidor entrega un `CancellationToken` ya cancelado
- **WHEN** invoca `GetAllAsync` o `GetByIdAsync`
- **THEN** la operación MUST finalizar como cancelada
- **AND** el envío HTTP MUST NOT iniciarse.

#### Scenario: Cliente read-only expone solo `GET`

- **GIVEN** la superficie pública de `ICategoriaHabilidadApiClient`
- **WHEN** se inspeccionan sus métodos
- **THEN** MUST exponer únicamente `GetAllAsync` y `GetByIdAsync`
- **AND** MUST NOT exponer `CreateAsync`, `UpdateAsync` ni `DeleteAsync`.

### Requirement: `HabilidadApiClient` traduce fallos de `CategoriaId` a `HabilidadError.CategoriaInexistente`

`HabilidadApiClient.CreateAsync` y `HabilidadApiClient.UpdateAsync` MUST traducir las respuestas 400 con código de error `CategoriaHabilidadNoExiste` (proveniente del backend) a `HabilidadCommandResult.Failure(HabilidadError { Type = HabilidadErrorType.CategoriaInexistente, Categoria = ErrorCategoria.Validation })`, delegando en `CommandResultMapper.Map`. La traducción MUST preservar `StatusCode = 400`, `Code`, `Message` y `FieldErrors` cuando aplique, en línea con el requisito **"Clientes HTTP administrativos usan `CommandResultMapper`"** de esta misma spec.

#### Scenario: `CreateAsync` con `CategoriaId` inexistente se traduce a `CategoriaInexistente`

- **GIVEN** un `POST /api/v1/skills` con `CategoriaId = <guid-fake>`
- **WHEN** el backend responde 400 con `ValidationProblemDetails` y código `CategoriaHabilidadNoExiste`
- **THEN** `HabilidadApiClient.CreateAsync` MUST devolver `HabilidadCommandResult.Failure(HabilidadError { Type = CategoriaInexistente, Categoria = Validation })`
- **AND** MUST preservar `StatusCode = 400` como metadata.

#### Scenario: `UpdateAsync` con `CategoriaId` inválido no persiste

- **GIVEN** un `PUT /api/v1/skills/{id}` con `CategoriaId = <guid-fake>`
- **WHEN** el backend responde 400 con código `CategoriaHabilidadNoExiste`
- **THEN** `HabilidadApiClient.UpdateAsync` MUST devolver `HabilidadCommandResult.Failure` con `Categoria == Validation`
- **AND** MUST NOT haber producido cambios persistidos (la operación ni siquiera llegó al servicio de aplicación).

#### Scenario: Cliente usa `CommandResultMapper` para clasificar la respuesta

- **GIVEN** cualquier respuesta no exitosa del backend a `HabilidadApiClient.CreateAsync`, `UpdateAsync`, `GetAllAsync` o `GetByIdAsync`
- **WHEN** el cliente procesa la respuesta
- **THEN** la `Categoria` resultante MUST provenir de `CommandResultMapper.Map`
- **AND** el cliente MUST NO mantener una matriz `switch` privada que duplique el mapper común.

## ADDED Requirements

> Delta introducida por el change `permitir-legajo-nulo-personas-issue-202` (issue #202). Verifica `openspec/changes/archive/2026-07-26-permitir-legajo-nulo-personas-issue-202/verify-report.md`. Formaliza la serialización de `Legajo` nullable en los wire-types `CrearPersonaRequest` y `ActualizarPersonaRequest` consumidos por el shell web y la normalización previa en el PageModel.

### Requirement: `CrearPersonaRequest.Legajo` y `ActualizarPersonaRequest.Legajo` son `string?`

`SGV.Contracts.Personas.CrearPersonaRequest.Legajo` y `SGV.Contracts.Personas.ActualizarPersonaRequest.Legajo` MUST tiparse como `string?`. La API surface MUST aceptar y deserializar (vía `System.Text.Json`) los siguientes payloads intercambiables — todos equivalentes a `string? == null` — para el campo `legajo`: ausente, `"legajo": null` y `"legajo": ""` (string vacío). En la respuesta, cuando el valor persistido es `NULL`, el cliente MUST observar `Legajo == null` en `PersonaDto`. La longitud máxima MUST seguir siendo `50` caracteres cuando se informa; strings más largas MUST rechazarse con `400 Bad Request` y `FieldErrors["Legajo"]`.

#### Scenario: Payload `legajo: null` deserializa a `string? == null`

- **GIVEN** un `POST /api/v1/personas` con cuerpo `{"legajo": null, …}`
- **WHEN** `PersonasController.Create` recibe el request vía `System.Text.Json`
- **THEN** `request.Legajo` MUST ser `null`
- **AND** `CrearPersonaRequestValidator` MUST pasar la validación
- **AND** `PersonaServicioComandos.CrearAsync` MUST invocar `repository.AddAsync(persona)` con `Persona.Legajo = NULL`.

#### Scenario: Payload sin la clave `legajo` deserializa a `string? == null`

- **GIVEN** un `POST /api/v1/personas` cuyo cuerpo omite por completo la clave `legajo`
- **WHEN** el request llega al controller
- **THEN** `request.Legajo` MUST ser `null` (System.Text.Json treats missing keys as default value for nullable strings)
- **AND** el comportamiento de unicidad MUST ser el mismo que cuando el valor es explícitamente `null`.

#### Scenario: Payload con `legajo: ""` deserializa como string vacío y se trata como ausente

- **GIVEN** un `POST /api/v1/personas` con `{"legajo": "", …}` enviado por un consumidor externo
- **WHEN** el request llega al controller
- **THEN** `request.Legajo` MUST ser `""` (string vacío), no `null`
- **AND** `PersonaServicioComandos.CrearAsync` MUST tratar `""` como ausente para la regla de unicidad (`ExistsActiveLegajoAsync` se omite cuando `string.IsNullOrEmpty` es `true`)
- **AND** el web shell MUST NOT permitir llegar a este estado: la normalización en el PageModel convierte whitespace y vacío a `null` antes de la serialización.

#### Scenario: PageModel normaliza whitespace a null antes de la API

- **GIVEN** el operador envía `Legajo = "   "` en el formulario de creación (`Pages/Personas/Create.cshtml.cs`)
- **AND** el PageModel normaliza a `null` antes de construir `CrearPersonaRequest`
- **WHEN** el PageModel invoca `PersonaApiClient.CreateAsync(request)`
- **THEN** el cuerpo HTTP resultante MUST serializarse como `{"legajo": null, …}` (o sin la clave, según `JsonSerializerOptions.Default`)
- **AND** el backend MUST persistir `Legajo = NULL` y responder `201 Created`.

#### Scenario: PageModel Edit normaliza whitespace antes de invocar Update

- **GIVEN** una Persona activa con `Legajo="L-001"`
- **AND** el operador envía `Legajo = "   "` en `Pages/Personas/Edit.cshtml.cs`
- **WHEN** el PageModel normaliza a `null` y llama a `PersonaApiClient.UpdateAsync(request)`
- **THEN** el payload MUST contener `legajo: null` o la clave ausente
- **AND** el backend MUST responder `200 OK`, persistir `Legajo = NULL` y registrar la fila `UpdateLegajo` (`LegajoAnterior="L-001"`, `LegajoNuevo=null`).

#### Scenario: Respuesta GET persona con Legajo persistido NULL

- **GIVEN** una Persona activa persistida con `Legajo = NULL`
- **WHEN** la API responde `200 OK` con `PersonaDto`
- **THEN** `PersonaDto.Legajo` MUST ser `null` (no `""`)
- **AND** el cliente MUST poder discriminar `null` de string vacío sin parseos adicionales.

#### Scenario: Legajo > 50 caracteres rechazado por el validator

- **GIVEN** un `POST /api/v1/personas` con `legajo` de longitud > 50
- **WHEN** el validator corre
- **THEN** la API MUST responder `400 Bad Request` con `FieldErrors["Legajo"]`
- **AND** MUST NOT persistir ningún cambio.

### Requirement: `PersonaApiClient` no pre-procesa `Legajo`

`PersonaApiClient.CreateAsync`, `PersonaApiClient.UpdateAsync` y `PersonaApiClient.ReactivateAsync` MUST delegar la serialización a `System.Text.Json` configurado por el host (`PostAsJsonAsync`/`PutAsJsonAsync`/`PatchAsJsonAsync`); MUST NOT pre-procesar `Legajo` (sin `Trim`, sin normalización `null` ↔ `""`, sin mapeo de casing). El PageModel `Pages/Personas/Create.cshtml.cs` y `Pages/Personas/Edit.cshtml.cs` son los únicos responsables de normalizar `Legajo` whitespace-only a `null` antes de construir el request. Cualquier intento de mutación del campo dentro del cliente MUST fallar el build por revisión de código.

#### Scenario: Cliente entrega crudo `null` y serializa `legajo: null`

- **GIVEN** un `CrearPersonaRequest` con `Legajo = null` construido por el PageModel
- **WHEN** `PersonaApiClient.CreateAsync` envía el request
- **THEN** el payload saliente MUST ser `{"legajo": null, …}`
- **AND** el cliente MUST NOT alterar el valor ni su casing antes de la serialización.

#### Scenario: Cliente entrega crudo `""` y serializa `legajo: ""` (no permitido por UI)

- **GIVEN** un `CrearPersonaRequest` con `Legajo = ""` construido por un consumidor no-UI (test seam)
- **WHEN** `PersonaApiClient.CreateAsync` envía el request
- **THEN** el payload saliente MUST ser `{"legajo": "", …}`
- **AND** el cliente MUST NOT transformar `""` en `null` ni trimear el valor antes de la serialización.

#### Scenario: Cliente entrega valor con espacios y los preserva

- **GIVEN** un `CrearPersonaRequest` con `Legajo = "  L-7  "` (no whitespace-only)
- **WHEN** `PersonaApiClient.UpdateAsync` envía el request
- **THEN** el payload saliente MUST contener `"legajo": "  L-7  "` exactamente
- **AND** el cliente MUST NOT aplicar `Trim` antes de la serialización.

## ADDED Requirements

> Delta introducida por el change `2026-07-27-cambiar-contrasena-usuario-logueado`
> (issue #204). Verificado en `openspec/changes/archive/2026-07-27-cambiar-contrasena-usuario-logueado/verify-report.md`.
> Esta delta agrega el método autenticado `IAuthApiClient.ChangePasswordAsync`
> y su implementación en `AuthApiClient`. Los requisitos previos (transporte
> nativo, `CommandResultMapper` excepto para `AuthApiClient`, anónimos para
> `ForgotPasswordAsync`/`ResetPasswordAsync`) NO se modifican.

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
