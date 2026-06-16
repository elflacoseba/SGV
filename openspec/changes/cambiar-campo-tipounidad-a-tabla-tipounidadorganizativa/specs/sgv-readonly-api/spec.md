# Capability: SGV Read-only API (delta)

> **Status:** MODIFIED — capability exists at `openspec/specs/sgv-readonly-api/spec.md`. This delta extends the inventory of read-only resources in the `Read-only Resource Access` requirement. The original spec file is **not** touched in this phase; `sdd-archive` will sync the delta.
> **Change:** `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa`

## Summary of the change

The read-only API inventory is extended to include `tipos-unidad-organizativa` as a new read-only resource. Two anonymous endpoints are added: `GET /api/v1/tipos-unidad-organizativa` (list) and `GET /api/v1/tipos-unidad-organizativa/{id:guid}` (detail). The full behavior contract for the new resource lives in the new capability `tipo-unidad-organizativa-catalog`; this delta only documents the resource's presence in the read-only API inventory.

## MODIFIED Requirements

### Requirement: Read-only Resource Access (MODIFIED)

The system MUST expose HTTP access to organizational units, organizational unit types, roles, positions, and skills. It MUST return real persisted data for all supported resources. Organizational units MAY expose supported create, update, parent-change, and soft-delete actions; roles, positions, skills, and **organizational unit types** MUST remain read-only in this version.

**Changes in this delta:**

- The inventory of read-only resources is extended to include `tipos-unidad-organizativa` (the `TipoUnidadOrganizativa` catalog, see capability `tipo-unidad-organizativa-catalog`).
- `tipos-unidad-organizativa` is exposed via two anonymous endpoints: `GET /api/v1/tipos-unidad-organizativa` (list) and `GET /api/v1/tipos-unidad-organizativa/{id:guid}` (detail).
- No write endpoint (`POST`, `PUT`, `PATCH`, `DELETE`) is exposed for `tipos-unidad-organizativa`; the catalog is read-only by design.

#### Scenario: List supported resources (MODIFIED)

- **GIVEN** persisted organizational units, organizational unit types, roles, positions, and skills exist
- **WHEN** a client requests each supported resource collection
- **THEN** the API MUST return the matching persisted records for each collection
- **AND** the response for `tipos-unidad-organizativa` MUST be a JSON array of `{ id, codigo, nombre }` elements
- **AND** each response MUST be successful.

#### Scenario: Resource `tipos-unidad-organizativa` is listed (ADDED)

- **GIVEN** the read-only API is documented
- **WHEN** the list of supported resources is enumerated
- **THEN** `tipos-unidad-organizativa` MUST appear in the list
- **AND** it MUST be advertised with two endpoints: `GET /api/v1/tipos-unidad-organizativa` (list) and `GET /api/v1/tipos-unidad-organizativa/{id:guid}` (detail).

#### Scenario: Empty supported resource collection (UNCHANGED)

- **GIVEN** a supported resource has no persisted records
- **WHEN** a client requests that resource collection
- **THEN** the API MUST return a successful response with an empty collection.

#### Scenario: Allow organizational unit writes only (UNCHANGED)

- **GIVEN** a client targets organizational units
- **WHEN** the client uses a documented create, update, parent-change, or soft-delete action
- **THEN** the API MAY modify persisted organizational unit data according to the CRUD contract.

#### Scenario: Reject unrelated write operations (MODIFIED)

- **GIVEN** a client targets roles, positions, skills, or organizational unit types
- **WHEN** the client attempts to create, update, or delete data through the API
- **THEN** the API MUST NOT modify persisted data
- **AND** the operation MUST NOT be exposed as a supported API action.

### Requirement: Response Contracts (UNCHANGED)

The system MUST return response models intended for API consumers. Responses MUST NOT expose persistence or domain entities directly.

#### Scenario: Return consumer-safe resource data (UNCHANGED)

- **GIVEN** persisted data exists for a supported resource
- **WHEN** a client requests that resource
- **THEN** the response MUST contain only consumer-facing fields for that resource
- **AND** the response MUST NOT include persistence tracking or internal audit fields unless explicitly specified by the API contract.

#### Scenario: Include relationships by identifier or summary (UNCHANGED)

- **GIVEN** a position references an organizational unit and a role
- **WHEN** a client requests positions
- **THEN** each position response SHOULD identify its related organizational unit and role in a consumer-safe form.

### Requirement: Public API Discoverability (UNCHANGED)

The system MUST publish API documentation that allows consumers to discover the read-only endpoints, organizational unit write endpoints, and response contracts.

#### Scenario: Discover endpoints through API documentation (UNCHANGED)

- **GIVEN** the API is running locally
- **WHEN** a client opens the API documentation
- **THEN** the documentation MUST list read endpoints for organizational units, organizational unit types, roles, positions, and skills
- **AND** it MUST describe the successful response contract for each endpoint.

#### Scenario: Discover organizational unit write operations (UNCHANGED)

- **GIVEN** organizational unit CRUD is supported
- **WHEN** a client inspects the API documentation
- **THEN** documented organizational unit create, update, parent-change, and soft-delete operations MUST be discoverable.

#### Scenario: Exclude unsupported operations from documentation (UNCHANGED)

- **GIVEN** roles, positions, skills, and organizational unit types remain read-only
- **WHEN** a client inspects the API documentation
- **THEN** create, update, and delete operations for those resources MUST NOT be documented as available actions.

### Requirement: No Authentication Requirement (UNCHANGED)

The system MUST allow access to the read-only endpoints without requiring authentication or authorization in this version.

#### Scenario: Anonymous client reads supported data (UNCHANGED)

- **GIVEN** the API is running and persisted data exists
- **WHEN** an unauthenticated client requests a supported resource collection
- **THEN** the API MUST process the request without requiring credentials
- **AND** the response MUST follow the same contract as an authenticated request would.

## ADDED Requirements

None at the requirement level. The new resource is captured by the modified scenarios above.

## REMOVED Requirements

None.
