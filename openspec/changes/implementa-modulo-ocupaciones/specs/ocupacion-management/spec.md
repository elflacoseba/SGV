# Occupation Management Specification

## Purpose

Manage `Ocupacion` as a standalone historical assignment resource between `Persona` and `Puesto`.

## Requirements

### Requirement: Standalone Occupation Resource

The system MUST expose `api/v1/ocupaciones` as a standalone resource with collection, detail, create, update, finalize, reactivate, and logical delete operations.

#### Scenario: Create through the standalone resource

- GIVEN a client has valid occupation data
- WHEN it sends `POST /api/v1/ocupaciones`
- THEN the API MUST create the occupation and return the created resource
- AND the resource location MUST be addressable through `GET /api/v1/ocupaciones/{id}`.

#### Scenario: Lifecycle actions are route-level operations

- GIVEN an existing occupation id
- WHEN a client inspects the contract
- THEN finalize MUST be exposed as `PATCH /api/v1/ocupaciones/{id}/finalizar`
- AND reactivate MUST be exposed as `PATCH /api/v1/ocupaciones/{id}/reactivar`.

### Requirement: Historical Lifecycle Semantics

The system MUST treat each occupation as a historical assignment. Create MUST start an active record with `FechaFin = null`. Update MUST apply only to active, non-deleted occupations. Finalize MUST set `FechaFin` and finalized occupations MUST NOT remain editable. Logical delete MUST preserve the row and hide it from active views. Reactivation MUST restore the same row to active state only when all business validations still pass.

#### Scenario: Finalize an active occupation

- GIVEN an active occupation exists
- WHEN a client finalizes it with a valid end date
- THEN the occupation MUST keep its history and store `FechaFin`
- AND the finalized occupation MUST stop being active.

#### Scenario: Reject updates to a finalized occupation

- GIVEN an occupation already has `FechaFin`
- WHEN a client sends `PUT /api/v1/ocupaciones/{id}`
- THEN the API MUST reject the change with a conflict response
- AND the persisted occupation MUST remain unchanged.

#### Scenario: Reactivate a finalized or logically deleted occupation

- GIVEN an occupation is finalized or logically deleted
- WHEN a client requests reactivation and validations pass
- THEN the system MUST restore that same record to active visibility
- AND it MUST NOT create a replacement occupation row.

### Requirement: Read Visibility

The system MUST return active occupations by default in collection reads. The collection contract MUST support `includeHistory=true` to include finalized and logically deleted records. Detail reads MUST be able to return a persisted occupation even when it is historical, so the lifecycle can be audited and reactivated safely.

#### Scenario: List active occupations by default

- GIVEN active and historical occupations exist
- WHEN a client requests `GET /api/v1/ocupaciones`
- THEN the response MUST include only active occupations
- AND logically deleted or finalized rows MUST be excluded.

#### Scenario: Include history explicitly

- GIVEN active and historical occupations exist
- WHEN a client requests `GET /api/v1/ocupaciones?includeHistory=true`
- THEN the response MUST include active, finalized, and logically deleted occupations
- AND each item MUST expose enough lifecycle data to distinguish its current state.

### Requirement: Reference and Uniqueness Validation

The system MUST validate `Persona` and `Puesto` state on create, update, and reactivate. Missing references MUST return `404`. Inactive or logically deleted referenced records MUST return `409`. Active uniqueness collisions for `Puesto` and for `Persona + Puesto` MUST return `409`.

#### Scenario: Reject an inactive reference

- GIVEN the referenced `Persona` or `Puesto` exists but is inactive
- WHEN a client creates, updates, or reactivates an occupation
- THEN the API MUST reject the request with `409`
- AND no occupation state change MUST be persisted.

#### Scenario: Reject an active uniqueness collision

- GIVEN another active occupation already uses the same `Puesto` or the same `Persona + Puesto`
- WHEN a client creates, updates, or reactivates an occupation into that state
- THEN the API MUST reject the request with `409`
- AND the existing active occupation MUST remain unchanged.
