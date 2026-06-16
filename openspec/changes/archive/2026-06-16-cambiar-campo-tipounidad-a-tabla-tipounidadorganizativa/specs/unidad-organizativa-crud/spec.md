# Capability: Unidad Organizativa CRUD (delta)

> **Status:** MODIFIED — capability exists at `openspec/specs/unidad-organizativa-crud/spec.md`. This delta modifies existing requirements and adds one new scenario. The original spec file is **not** touched in this phase; `sdd-archive` will sync the delta.
> **Change:** `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa`
> **Delta marker IDs:** `REQ-UOC-EXISTING-001` (create), `REQ-UOC-EXISTING-002` (update), `REQ-UOC-EXISTING-003` (DTO shape). These are delta-local tracking IDs that point at the modified scenarios below.

## Summary of the change

The `tipoUnidad: string` field is removed from both the request body (`CrearUnidadOrganizativaRequest`, `ActualizarUnidadOrganizativaRequest`) and the response DTO (`UnidadOrganizativaDto`). It is replaced by:

- A `tipoUnidadId: Guid` field on the **request** (required on create, optional on update). The Guid MUST reference an existing row in the `TipoUnidadOrganizativa` catalog (see capability `tipo-unidad-organizativa-catalog`).
- A `tipoUnidadId: Guid` and a denormalized `tipoUnidadNombre: string` on the **response DTO**, both fetched in the same query as the unit (single JOIN, no second round-trip).

The application service (not the database FK) enforces the catalog existence check before the write, so unknown or missing IDs produce a `400 Bad Request` validation error rather than a database-level error.

## MODIFIED Requirements

### Requirement: Manage Organizational Units (MODIFIED)

The system MUST allow clients to create, update, read, re-parent, and soft-delete organizational units through stable API contracts. The legacy `tipoUnidad: string` field is removed from the request body and from the response DTO. On writes, a `tipoUnidadId: Guid` referencing an existing `TipoUnidadOrganizativa` row is required. On reads, the response includes both the `tipoUnidadId` and a denormalized `tipoUnidadNombre`.

#### Scenario: Create organizational unit (MODIFIED — tracks REQ-UOC-EXISTING-001)

- **GIVEN** valid organizational unit data with a unique active code AND a `tipoUnidadId` referencing an existing `TipoUnidadOrganizativa`
- **WHEN** a client creates the unit
- **THEN** the system MUST persist it with that foreign key
- **AND** return the created unit contract
- **AND** the response MUST include `tipoUnidadId: <Guid>` and `tipoUnidadNombre: <string>` (denormalized)
- **AND** the response MUST NOT include a `tipoUnidad` string field.

#### Scenario: Update organizational unit (MODIFIED — tracks REQ-UOC-EXISTING-002)

- **GIVEN** an active organizational unit exists
- **WHEN** a client updates editable fields with valid data, optionally including a new `tipoUnidadId`
- **THEN** the system MUST persist the changes
- **AND** preserve its identifier
- **AND** if a `tipoUnidadId` was supplied, the system MUST validate it against the catalog and update the foreign key before commit.

#### Scenario: Read organizational unit (ADDED — tracks REQ-UOC-EXISTING-003)

- **GIVEN** an active organizational unit exists with a foreign key to a `TipoUnidadOrganizativa` named `Facultad`
- **WHEN** a client reads the unit
- **THEN** the response is `{ ..., "tipoUnidadId": "<Guid>", "tipoUnidadNombre": "Facultad" }`
- **AND** the response MUST NOT include a `tipoUnidad` string field.

#### Scenario: Soft-delete organizational unit (UNCHANGED)

- **GIVEN** an active organizational unit exists
- **WHEN** a client deletes the unit
- **THEN** the system MUST mark it inactive or deleted
- **AND** exclude it from active read results.

### Requirement: Validate Organizational Unit Writes (MODIFIED)

The system MUST reject invalid organizational unit writes before committing partial changes. In addition to the existing duplicate-code and invalid-hierarchy checks, the system MUST reject any write whose `tipoUnidadId` is missing (on create) or does not reference an existing `TipoUnidadOrganizativa` row (on create and on update). A rejected write MUST NOT modify the database.

#### Scenario: Reject duplicate active code (UNCHANGED)

- **GIVEN** an active organizational unit already uses a code
- **WHEN** a client creates or updates another active unit with that code
- **THEN** the system MUST reject the operation with a predictable conflict error.

#### Scenario: Reject invalid hierarchy change (UNCHANGED)

- **GIVEN** an organizational unit exists in a hierarchy
- **WHEN** a client sets itself or one of its descendants as parent
- **THEN** the system MUST reject the operation
- **AND** MUST NOT change the hierarchy.

#### Scenario: Reject create with non-existent type id (ADDED — tracks REQ-UOC-EXISTING-001)

- **GIVEN** a random Guid that does not exist in the `TipoUnidadOrganizativa` catalog
- **WHEN** a client `POST`s a new unit with `tipoUnidadId: <random>`
- **THEN** the response status is `400 Bad Request`
- **AND** the response body includes a validation error indicating that the referenced `TipoUnidadOrganizativa` does not exist
- **AND** no entity is persisted.

#### Scenario: Reject create with missing type id (ADDED — tracks REQ-UOC-EXISTING-001)

- **GIVEN** a request body without a `tipoUnidadId` field
- **WHEN** a client `POST`s it
- **THEN** the response status is `400 Bad Request`
- **AND** the response body includes a `required` field error for `tipoUnidadId`
- **AND** no entity is persisted.

#### Scenario: Reject update with non-existent type id (ADDED — tracks REQ-UOC-EXISTING-002)

- **GIVEN** an existing unit
- **WHEN** a client `PUT`s it with a `tipoUnidadId` that does not exist in the catalog
- **THEN** the response status is `400 Bad Request`
- **AND** the existing unit MUST NOT be modified.

## ADDED Requirements

None at the requirement level. The new behavior is captured by the modified scenarios above and the three delta marker IDs (`REQ-UOC-EXISTING-001`, `REQ-UOC-EXISTING-002`, `REQ-UOC-EXISTING-003`) used by the orchestrator for cross-phase tracking.

## REMOVED Requirements

None.
