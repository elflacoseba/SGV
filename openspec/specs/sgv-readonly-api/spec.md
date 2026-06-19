# SGV Read-only API Specification

## Purpose

Expose SGV catalog and structure data through an external read-only HTTP API. The API MUST return persisted data for organizational units, organizational unit types, roles, positions, and skills, and MUST NOT require authentication in this version.

## Requirements

### Requirement: Read-only Resource Access

The system MUST expose HTTP access to organizational units, organizational unit types, roles, positions, and skills. It MUST return real persisted data for all supported resources. Organizational units, roles (cargos), positions, and skills MAY expose documented create, update, and soft-delete/reactivate actions; the types of organizational units MUST remain read-only in this version. `/api/v1/skills` SHALL remain as the canonical route of the skills catalog.
(Previously: skills and organizational unit types remained read-only; skills did not expose write operations.)

#### Scenario: List supported resources

- **GIVEN** persisted organizational units, organizational unit types, roles, positions, and skills exist
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

The system MUST publish API documentation that allows consumers to discover read endpoints, write endpoints for organizational units, roles (cargos), positions, and skills, and response contracts.
(Previously: documentation excluded write operations for positions and skills.)

#### Scenario: Discover endpoints through API documentation

- **GIVEN** the API is running locally
- **WHEN** a client opens the API documentation
- **THEN** the documentation MUST list read endpoints for organizational units, organizational unit types, roles, positions, and skills
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
- **THEN** documented skill create, update, deactivate, and reactivate operations under `/api/v1/skills` MUST be discoverable.

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

The system MUST allow access to the read-only endpoints without requiring authentication or authorization in this version.

#### Scenario: Anonymous client reads supported data

- **GIVEN** the API is running and persisted data exists
- **WHEN** an unauthenticated client requests a supported resource collection
- **THEN** the API MUST process the request without requiring credentials
- **AND** the response MUST follow the same contract as an authenticated request would.