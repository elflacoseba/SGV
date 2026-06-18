# Delta para sgv-readonly-api

## MODIFIED Requirements

### Requirement: Read-only Resource Access

The system MUST expose HTTP access to organizational units, organizational unit types, roles, positions, and skills. It MUST return real persisted data for all supported resources. Organizational units and **roles (cargos)** MAY expose supported create, update, and soft-delete/reactivate actions; positions, skills, and organizational unit types MUST remain read-only in this version.
(Previously: organizational units were the only writable resource; roles, positions, skills, and organizational unit types were all read-only.)

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

#### Scenario: Reject unrelated write operations

- **GIVEN** a client targets positions, skills, or organizational unit types
- **WHEN** the client attempts to create, update, or delete data through the API
- **THEN** the API MUST NOT modify persisted data
- **AND** the operation MUST NOT be exposed as a supported API action.

### Requirement: Public API Discoverability

The system MUST publish API documentation that allows consumers to discover the read-only endpoints, organizational unit write endpoints, **cargo management endpoints**, and response contracts.
(Previously: documentation only covered organizational unit write operations; cargo management endpoints were not included.)

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

#### Scenario: Exclude unsupported operations from documentation

- **GIVEN** positions, skills, and organizational unit types remain read-only
- **WHEN** a client inspects the API documentation
- **THEN** create, update, and delete operations for those resources MUST NOT be documented as available actions.