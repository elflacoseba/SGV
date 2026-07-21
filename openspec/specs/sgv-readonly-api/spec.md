# SGV Read-only API Specification

## Purpose

Expose SGV catalog and structure data through an external read-only HTTP API. The API MUST return persisted data for organizational units, organizational unit types, roles, positions, and skills, and MUST NOT require authentication in this version, except for Cargos (`/api/v1/cargos`) and its `/skills` subresource, which require authentication for reads and the `Administrador` role for mutations (see `cargo-management` and `cargo-skill-query-contract` specs).

## Requirements

### Requirement: Read-only Resource Access

The system MUST expose HTTP access to organizational units, organizational unit types, roles, positions, skills, and Personas. It MUST return real persisted data for all supported resources. Organizational units, roles (cargos), positions, skills, and Personas MAY expose documented create, update, soft-delete/deactivate, and reactivate actions; the types of organizational units MUST remain read-only in this version. `/api/v1/skills` SHALL remain as the canonical route of the skills catalog. Personas SHALL use `api/v1/personas` and SHALL remain limited to administrative data in this slice.
(Previously: skills and organizational unit types remained read-only; skills did not expose write operations. Personas were not exposed as an HTTP resource.)

#### Scenario: List supported resources

- **GIVEN** persisted organizational units, organizational unit types, roles, positions, skills, and Personas exist
- **WHEN** a client requests each supported resource collection
- **THEN** the API MUST return the matching persisted records for each collection
- **AND** the response for `tipos-unidad-organizativa` MUST be a JSON array of `{ id, codigo, nombre }` elements
- **AND** each response MUST be successful.

#### Scenario: Resource `tipos-unidad-organizativa` is listed

- **GIVEN** the read-only API is documented
- **WHEN** the list of supported resources is enumerated
- **THEN** `tipos-unidad-organizativa` MUST appear in the list
- **AND** it MUST be advertised with two endpoints: `GET /api/v1/tipos-unidad-organizativa` (list) and `GET /api/v1/tipos-unidad-organizativa/{id:guid}` (detail).

#### Scenario: Empty supported resource collection

- **GIVEN** a supported resource has no persisted records
- **WHEN** a client requests that resource collection
- **THEN** the API MUST return a successful response with an empty collection.

#### Scenario: Allow organizational unit writes

- **GIVEN** a client targets organizational units
- **WHEN** the client uses a documented create, update, parent-change, or soft-delete action
- **THEN** the API MAY modify persisted organizational unit data according to the CRUD contract.

#### Scenario: Allow cargo write operations

- **GIVEN** a client targets roles (cargos)
- **WHEN** the client uses a documented create, update, or soft-delete/reactivate action
- **THEN** the API MAY modify persisted cargo data according to the cargo management contract.

#### Scenario: Allow puesto write operations

- **GIVEN** a client targets positions
- **WHEN** the client uses a documented create, update, deactivate, or reactivate action
- **THEN** the API MAY modify persisted position data according to the puesto management contract.

#### Scenario: Allow skill write operations

- **GIVEN** a client targets skills via `/api/v1/skills`
- **WHEN** the client uses documented create, update, deactivate, or reactivate actions
- **THEN** the API MAY modify persisted skill catalog data according to the skill management contract.

#### Scenario: Allow persona administrative operations

- **GIVEN** a client targets Personas via `api/v1/personas`
- **WHEN** the client uses documented create, update, deactivate, or reactivate actions
- **THEN** the API MAY modify persisted Persona data according to the persona management contract.

#### Scenario: Reject unrelated write operations

- **GIVEN** a client targets organizational unit types
- **WHEN** the client attempts to create, update, or delete data through the API
- **THEN** the API MUST NOT modify persisted data
- **AND** the operation MUST NOT be exposed as a supported API action.

### Requirement: Response Contracts

The system MUST return response models intended for API consumers. Responses MUST NOT expose persistence or domain entities directly.

#### Scenario: Return consumer-safe resource data

- **GIVEN** persisted data exists for a supported resource
- **WHEN** a client requests that resource
- **THEN** the response MUST contain only consumer-facing fields for that resource
- **AND** the response MUST NOT include persistence tracking or internal audit fields unless explicitly specified by the API contract.

#### Scenario: Include relationships by identifier or summary

- **GIVEN** a position references an organizational unit and a role
- **WHEN** a client requests positions
- **THEN** each position response SHOULD identify its related organizational unit and role in a consumer-safe form.

### Requirement: Public API Discoverability

The system MUST publish API documentation that allows consumers to discover read endpoints, write endpoints for organizational units, roles (cargos), positions, skills, Personas, `GET /api/v1/skills/consulta`, and `GET /api/v1/niveles-habilidad`, along with their response contracts. Documentation for skills MUST preserve `GET /api/v1/skills` as the legacy active-only read route and MUST describe `skills/consulta` as the segmented read contract.
(Previously: documentation allowed consumers to discover read endpoints and write endpoints for organizational units, roles, positions, skills, and Personas, but did not document `skills/consulta` or `niveles-habilidad`.)

#### Scenario: Discover endpoints through API documentation

- **GIVEN** the API is running locally
- **WHEN** a client opens the API documentation
- **THEN** the documentation MUST list read endpoints for organizational units, organizational unit types, roles, positions, skills, Personas, and `niveles-habilidad`
- **AND** it MUST describe the successful response contract for each endpoint.

#### Scenario: Discover organizational unit write operations

- **GIVEN** organizational unit CRUD is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented organizational unit create, update, parent-change, and soft-delete operations MUST be discoverable.

#### Scenario: Discover cargo management operations

- **GIVEN** cargo management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented cargo create, update, deactivate, and reactivate operations MUST be discoverable.

#### Scenario: Discover puesto management operations

- **GIVEN** position management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented position create, update, deactivate, and reactivate operations MUST be discoverable.

#### Scenario: Discover skill management operations

- **GIVEN** skill management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented skill create, update, deactivate, and reactivate operations under `/api/v1/skills` MUST be discoverable
- **AND** `GET /api/v1/skills/consulta` MUST appear as the segmented query route.

#### Scenario: Discover segmented skill query parameters

- **GIVEN** a client inspects `GET /api/v1/skills/consulta`
- **WHEN** the query parameters are reviewed
- **THEN** the documentation MUST describe `status=activas|eliminadas`, `search`, `sort`, `page` and `pageSize`
- **AND** MUST indicate that `activas` is the default segment.

#### Scenario: Discover skill-level catalog

- **GIVEN** a client inspects the API documentation
- **WHEN** the documented read resources are reviewed
- **THEN** `GET /api/v1/niveles-habilidad` MUST be discoverable
- **AND** its success response MUST describe the consumer-safe level catalog.

#### Scenario: Discover persona management operations

- **GIVEN** persona management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented Persona operations under `api/v1/personas` MUST be discoverable.

#### Scenario: Exclude unsupported operations from documentation

- **GIVEN** organizational unit types remain read-only
- **WHEN** a client inspects the API documentation
- **THEN** create, update, and delete operations for those resources MUST NOT be documented as available actions.

### Requirement: Puesto Management Contract

The system MUST manage positions as an administrable catalog. `codigo` and `nombre` SHALL be required; `PuestoSuperiorId` MAY be omitted; Occupations (Ocupaciones), Vacancies (Vacantes), permissions, and roles MUST remain out of scope.

#### Scenario: Create a valid position

- **GIVEN** a valid organizational unit and role (cargo) exist
- **WHEN** a position is created with `codigo` and `nombre`
- **THEN** the position MUST be persisted as active and MUST be available in active queries.

#### Scenario: Reject missing required data

- **GIVEN** a position creation or update request
- **WHEN** `codigo` or `nombre` is missing
- **THEN** the API MUST reject the request without persisting changes.

#### Scenario: Deactivate and reactivate position

- **GIVEN** an active position exists
- **WHEN** it is deactivated and later reactivated
- **THEN** the system MUST apply soft-delete and MAY restore visibility if no active code conflict exists.

### Requirement: No Authentication Requirement

El sistema MUST aplicar una postura de default-deny: el único endpoint explícitamente anónimo de toda la API es `POST /api/v1/auth/login`. Cualquier otro endpoint MUST requerir autenticación; las mutaciones MUST requerir, además, el rol `Administrador`. Las lecturas autenticadas MUST conservar sus contratos `2xx` vigentes y los clientes autenticados sin el rol correcto sobre una mutación MUST recibir `403 Forbidden`. Los clientes sin credenciales sobre cualquier endpoint distinto de `POST /api/v1/auth/login` MUST recibir `401 Unauthorized`. La excepción `[AllowAnonymous]` MUST limitarse a `AuthController.Login` para que sobreviva la fallback policy global aplicada en `Program.cs`; cualquier otro caso MUST seguir la regla default-deny.
(Previously: todos los endpoints read-only existentes podían consumirse anónimamente, con excepción de las lecturas y mutaciones de Cargos y su subrecurso de skills, que ya requerían autenticación o rol `Administrador`. El resto de la API —incluidos PersonasController, UnidadesOrganizativasController, NivelesCargoController y TipoUnidadesOrganizativasController— permanecía accesible sin credenciales.)

#### Scenario: Login como única ruta anónima

- GIVEN la API está disponible con la fallback policy global activa
- WHEN un cliente sin credenciales solicita `POST /api/v1/auth/login` con payload válido
- THEN la API MUST responder `2xx` con el contrato vigente de autenticación
- AND la acción `Login` MUST ser la única ruta anonima permitida por la API.

#### Scenario: Lectura anónima rechazada en endpoint distinto a Login

- GIVEN la API está disponible con la fallback policy global activa
- WHEN un cliente sin credenciales solicita cualquier endpoint distinto a `POST /api/v1/auth/login` (incluidos `GET /api/v1/personas`, `GET /api/v1/unidades-organizativas`, `GET /api/v1/niveles-cargo`, `GET /api/v1/tipos-unidad-organizativa`, lecturas de Cargos u otros recursos)
- THEN la API MUST responder `401 Unauthorized`
- AND MUST NOT exponer datos persistidos a clientes anónimos.

#### Scenario: Lectura autenticada exitosa

- GIVEN un cliente autenticado
- WHEN solicita un endpoint de lectura de cualquier recurso cubierto por la API (Cargos, Personas, UnidadesOrganizativas, NivelesCargo, TipoUnidadesOrganizativa, Puestos o Skills)
- THEN la API MUST responder `2xx` con el contrato documentado del recurso solicitado.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de mutación sobre cualquier recurso cubierto por la API (Cargos, Personas, UnidadesOrganizativas, Puestos, Skills o Usuarios)
- WHEN la ejecuta un cliente autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx` con el contrato vigente.

#### Scenario: Catálogos read-only requieren autenticación

- GIVEN la API está disponible con la fallback policy global activa
- WHEN un cliente sin credenciales solicita `GET /api/v1/niveles-cargo` o `GET /api/v1/tipos-unidad-organizativa`
- THEN la API MUST responder `401 Unauthorized`
- AND un cliente autenticado MUST recibir `2xx` con el contrato de catálogo vigente.

### Requirement: Enriched Cargo and Persona skill query documentation

The system MUST document `GET /api/v1/cargos/{cargoId}/skills` and `GET /api/v1/personas/{personaId}/skills` as enriched read contracts that preserve required `skillId` and `nivelId` and add nested `skill` and `nivel` objects. This change MUST remain separate from the prior assignment change and MUST NOT imply changes to Cargo/Persona parent payloads or to PUT/DELETE subresource contracts.

#### Scenario: Document the enriched cargo skill query response

- **GIVEN** a client inspects the API documentation
- **WHEN** the documented response for `GET /api/v1/cargos/{cargoId}/skills` is reviewed
- **THEN** the success contract MUST show `skillId`, `nivelId`, `skill`, and `nivel`.

#### Scenario: Document the enriched persona skill query response

- **GIVEN** a client inspects the API documentation
- **WHEN** the documented response for `GET /api/v1/personas/{personaId}/skills` is reviewed
- **THEN** the success contract MUST show `skillId`, `nivelId`, `skill`, and `nivel`.

#### Scenario: Preserve scope boundaries in documentation

- **GIVEN** a client compares the prior assignment capability with this change
- **WHEN** the documentation is reviewed
- **THEN** it MUST describe this change as a GET-only response enrichment
- **AND** it MUST NOT document parent Cargo/Persona payload changes or new write-contract fields.

### Requirement: Contrato documentado de reactivación de unidades organizativas

La documentación HTTP MUST describir `PATCH /api/v1/unidades-organizativas/{id}/reactivar` como una operación soportada para unidades organizativas. La documentación MUST reflejar respuesta exitosa con `UnidadOrganizativaDto` y MUST documentar conflictos previsibles de reactivación sin inventar reglas nuevas.

#### Scenario: Descubrir el endpoint de reactivación

- GIVEN un cliente inspecciona la documentación de unidades organizativas
- WHEN revisa las operaciones disponibles
- THEN la documentación MUST incluir `PATCH /api/v1/unidades-organizativas/{id}/reactivar`.

#### Scenario: Documentar respuesta exitosa

- GIVEN un cliente inspecciona el endpoint de reactivación
- WHEN revisa la respuesta satisfactoria
- THEN la documentación MUST indicar `200 OK`
- AND MUST describir que devuelve `UnidadOrganizativaDto`.

#### Scenario: Documentar errores previsibles

- GIVEN un cliente inspecciona el endpoint de reactivación
- WHEN revisa sus errores posibles
- THEN la documentación MUST incluir `404 Not Found`
- AND MUST incluir `409 Conflict` para código activo duplicado o padre inactivo/eliminado.

### Requirement: Contrato documentado de filtro de listado de unidades organizativas

La documentación HTTP MUST describir que `GET /api/v1/unidades-organizativas/consulta` acepta un filtro de estado para consultar `activas` o `eliminadas`. La documentación MUST indicar que la vista por defecto es `activas`, MUST reutilizar el mismo contrato de respuesta y MUST NOT documentar una grilla mixta ni cambios al contrato del árbol.

#### Scenario: Descubrir el filtro de estado del listado

- GIVEN un cliente inspecciona la documentación de unidades organizativas
- WHEN revisa el endpoint `GET /api/v1/unidades-organizativas/consulta`
- THEN la documentación MUST describir el filtro para elegir `activas` o `eliminadas`
- AND MUST indicar que `activas` es la vista por defecto.

#### Scenario: Documentar la respuesta de eliminadas

- GIVEN un cliente inspecciona la consulta documentada de unidades organizativas
- WHEN revisa la respuesta para la vista de eliminadas
- THEN la documentación MUST describir el mismo contrato `UnidadOrganizativaDto`
- AND MUST dejar claro que la respuesta contiene solo unidades eliminadas.

#### Scenario: Mantener fuera de alcance el listado mixto y el árbol

- GIVEN un cliente compara las operaciones documentadas de lectura
- WHEN revisa el cambio del listado
- THEN la documentación MUST NO presentar una respuesta mixta de activas y eliminadas
- AND MUST mantener el árbol documentado como una lectura separada sin este filtro.

### Requirement: REQ-SRA-01 Swagger documenta consulta segmentada y reactivación de cargos

La documentación HTTP MUST exponer `GET /api/v1/cargos/consulta` con el filtro `status=activas|eliminadas`, MUST indicar que activas es el valor por defecto y MUST mantener visible `PATCH /api/v1/cargos/{id}/reactivar` con sus respuestas documentadas.

#### Scenario: Swagger permite descubrir consulta y reactivación de cargos

- GIVEN un consumidor abre Swagger para revisar el recurso de cargos
- WHEN inspecciona las operaciones documentadas del controller
- THEN encuentra `GET /api/v1/cargos/consulta` con `status` documentado
- AND encuentra `PATCH /api/v1/cargos/{id}/reactivar`.

#### Source

- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/specs/sgv-readonly-api/spec.md:9-27`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/proposal.md:35-39,43-45`
- `openspec/changes/archive/2026-07-02-cargos-filtro-activos-eliminados/exploration.md:91-111`

#### Verification

- API/Swagger: `Cargos_ConsultaEndpoint_DocumentaParametroStatus`
- API/Swagger: `Cargos_ReactivarEndpoint_SigueDocumentado`

## ADDED Requirements

> Delta introducida por el change `2026-07-14-fix-126-operational-tech-debt` (issue #126). Verificado en `openspec/changes/archive/2026-07-14-fix-126-operational-tech-debt/verify-report.md`.

### Requirement: Excepción de anonimato para probes operacionales

Los endpoints `GET /health/live` y `GET /health/ready` en `SGV.Api` y
`SGV.Web` MUST ser accesibles sin autenticación, como excepción
puntual del default-deny declarada por el requirement "No
Authentication Requirement" vigente. La `FallbackPolicy =
RequireAuthenticatedUser()` MUST permanecer intacta para el resto de
la API. La excepción MUST limitarse exclusivamente a las dos rutas
listadas y MUST NOT extenderse a ningún otro endpoint. La mecánica
MUST ser `.AllowAnonymous()` explícito en cada `MapHealthChecks(...)`
de `SGV.Api/Program.cs` y `SGV.Web/Program.cs`.

#### Scenario: Probe API `/health/live` responde 200 anónimo

- **GIVEN** `SGV.Api` arrancada con la fallback policy global activa
- **AND** sin credenciales (sin bearer token ni cookie)
- **WHEN** un cliente solicita `GET /health/live`
- **THEN** la API MUST responder `200 OK`
- **AND** MUST NOT responder `401 Unauthorized`.

#### Scenario: Probe API `/health/ready` responde 503 anónimo cuando MySQL está caído

- **GIVEN** `SGV.Api` arrancada con la fallback policy global activa
- **AND** MySQL inaccesible o el readiness check reporta `Unhealthy`
- **AND** sin credenciales
- **WHEN** un cliente solicita `GET /health/ready`
- **THEN** la API MUST responder `503 Service Unavailable` con cuerpo JSON describiendo el estado
- **AND** MUST NOT responder `401 Unauthorized`.

#### Scenario: Probe Web `/health/live` y `/health/ready` responden 200/503 anónimos sin redirigir

- **GIVEN** `SGV.Web` arrancada con cookie auth configurada
- **AND** sin cookie de autenticación presente en el request
- **WHEN** un cliente (orquestador) solicita `GET /health/live` o `GET /health/ready`
- **THEN** Web MUST responder `200` o `503` según el estado
- **AND** MUST NOT redirigir a `/auth/sign-in`
- **AND** MUST NOT responder `302 Found`.

#### Scenario: Default-deny sigue vigente para el resto de la API

- **GIVEN** la excepción de anonimato aplica solo a `/health/live` y `/health/ready`
- **WHEN** un cliente sin credenciales solicita cualquier otro endpoint (incluidos `/api/v1/personas`, `/api/v1/unidades-organizativas`, `/api/v1/niveles-cargo`, `/api/v1/tipos-unidad-organizativa`, lecturas o mutaciones de Cargos u otros recursos)
- **THEN** la API MUST responder `401 Unauthorized`
- **AND** la fallback policy `RequireAuthenticatedUser` MUST NO estar relajada.

### Requirement: Probes operacionales no exponen datos de negocio

Los probes `GET /health/live` y `GET /health/ready` MUST responder
únicamente con el estado del check (Healthy / Unhealthy / Degraded) y
detalles operativos del check (nombre, descripción acotada, duración).
MUST NOT exponer datos persistidos, entidades de dominio, registros de
auditoría ni mensajes de excepción con stack traces. El cuerpo de la
respuesta MUST ser JSON con la shape `{ status, totalDurationMs,
entries: [{ name, status, description, durationMs }] }` definida por
el writer compartido `HealthCheckResponseWriter`.

#### Scenario: Cuerpo JSON del probe no contiene stack trace ni exception

- **GIVEN** un probe `/health/ready` que reporta `Unhealthy` por fallo de transporte o de DB
- **WHEN** un cliente inspecciona el cuerpo de la respuesta
- **THEN** el JSON MUST contener `status: "Unhealthy"`
- **AND** MUST NO contener campos `stackTrace`, `exception`, ni mensajes con detalle interno del servidor.

### Requirement: Catálogo `tipos-documento` listado y detallado

El sistema DEBE exponer `GET /api/v1/tipos-documento` que devuelve los 4 tipos seedeados y `GET /api/v1/tipos-documento/{id:guid}` que devuelve un tipo puntual. Ambos endpoints DEBEN requerir autenticación (default-deny global). Los endpoints de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre `TiposDocumento` NO DEBEN estar expuestos.

#### Escenario: Listar `TiposDocumento` autenticado

- **DADO** los 4 tipos seedeados en `TiposDocumento` (`DNI`, `LE`, `LC`, `Pasaporte`)
- **CUANDO** un cliente autenticado solicita `GET /api/v1/tipos-documento`
- **ENTONCES** la API DEBE responder `200 OK`
- **Y** el cuerpo es un array JSON de 4 elementos con `id`, `codigo`, `nombre`, `patronValidacion` (cuando aplique), `longitudMinima` y `longitudMaxima`.

#### Escenario: Acceso anónimo a `tipos-documento` es rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita `GET /api/v1/tipos-documento` o `GET /api/v1/tipos-documento/{id:guid}`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

### Requirement: Contrato `TipoDocumentoDto` documentado en Swagger

La documentación HTTP MUST describir `TipoDocumentoDto` con `id: Guid`, `codigo: string`, `nombre: string`, `patronValidacion: string?`, `longitudMinima: int?` y `longitudMaxima: int?`. MUST incluir los endpoints `GET /api/v1/tipos-documento` y `GET /api/v1/tipos-documento/{id:guid}` con respuesta `200 OK` documentada. MUST NO documentar endpoints de escritura.

#### Escenario: Swagger expone el contrato del catálogo

- **DADO** un consumidor abriendo Swagger
- **CUANDO** inspecciona `TipoDocumentosController`
- **ENTONCES** la documentación MUST listar `GET /api/v1/tipos-documento` con respuesta `200 OK` y el esquema `TipoDocumentoDto`
- **Y** MUST listar `GET /api/v1/tipos-documento/{id:guid}` con respuesta `200 OK` y `404 Not Found`
- **Y** NO DEBE listar operaciones `POST`, `PUT`, `PATCH` o `DELETE` sobre el recurso.

#### Escenario: Forma del DTO coincide con el seed

- **DADO** el `TipoDocumento` seedeado con `Codigo="Pasaporte"`, `PatronValidacion="^[A-Za-z]{3}\d{6}$"`, `LongitudMinima=null`, `LongitudMaxima=null`
- **CUANDO** un cliente autenticado solicita `GET /api/v1/tipos-documento`
- **ENTONCES** el elemento correspondiente contiene `codigo="Pasaporte"`, `nombre="Pasaporte"`, `patronValidacion="^[A-Za-z]{3}\\d{6}$"`, `longitudMinima=null` y `longitudMaxima=null`.
