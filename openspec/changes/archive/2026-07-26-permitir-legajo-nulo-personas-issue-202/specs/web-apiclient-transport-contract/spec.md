# Delta para web-apiclient-transport-contract

> Delta introducida por el change `permitir-legajo-nulo-personas-issue-202` (issue #202). Formaliza la serialización de `Legajo` nullable en los wire-types `CrearPersonaRequest` y `ActualizarPersonaRequest` consumidos por el shell web y la normalización previa en el PageModel.

## ADDED Requirements

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
