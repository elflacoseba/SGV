# Delta para SGV Read-only API

## MODIFIED Requirements

### Requirement: Read-only Resource Access

The system MUST expose HTTP access to organizational units, organizational unit types, roles, positions, skills, and Personas. It MUST return real persisted data for all supported resources. Organizational units, roles (cargos), positions, skills, and Personas MAY expose documented create, update, soft-delete/deactivate, and reactivate actions; the types of organizational units MUST remain read-only in this version. `/api/v1/skills` SHALL remain as the canonical route of the skills catalog. Personas SHALL use `api/v1/personas` and SHALL remain limited to administrative data in this slice.
(Previously: Personas were not exposed as an HTTP resource.)

#### Scenario: List supported resources

- **GIVEN** persisted organizational units, organizational unit types, roles, positions, skills, and Personas exist
- **WHEN** a client requests each supported resource collection
- **THEN** the API MUST return the matching persisted records for each collection
- **AND** each response MUST be successful.

#### Scenario: Resource `tipos-unidad-organizativa` is listed

- **GIVEN** the read-only API is documented
- **WHEN** the list of supported resources is enumerated
- **THEN** `tipos-unidad-organizativa` MUST appear with list and detail endpoints.

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
- **THEN** the API MUST NOT modify persisted data.

### Requirement: Public API Discoverability

The system MUST publish API documentation that allows consumers to discover read endpoints, write endpoints for organizational units, roles (cargos), positions, skills, and Personas, and response contracts.
(Previously: Personas were not documented as an API resource.)

#### Scenario: Discover endpoints through API documentation

- **GIVEN** the API is running locally
- **WHEN** a client opens the API documentation
- **THEN** the documentation MUST list read endpoints for organizational units, organizational unit types, roles, positions, skills, and Personas.

#### Scenario: Discover organizational unit write operations

- **GIVEN** organizational unit CRUD is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented organizational unit write operations MUST be discoverable.

#### Scenario: Discover cargo management operations

- **GIVEN** cargo management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented cargo write operations MUST be discoverable.

#### Scenario: Discover puesto management operations

- **GIVEN** position management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented position write operations MUST be discoverable.

#### Scenario: Discover skill management operations

- **GIVEN** skill management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented skill write operations under `/api/v1/skills` MUST be discoverable.

#### Scenario: Discover persona management operations

- **GIVEN** persona management is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented Persona operations under `api/v1/personas` MUST be discoverable.

#### Scenario: Exclude unsupported operations from documentation

- **GIVEN** organizational unit types remain read-only
- **WHEN** a client inspects the API documentation
- **THEN** create, update, and delete operations for those resources MUST NOT be documented as available actions.
