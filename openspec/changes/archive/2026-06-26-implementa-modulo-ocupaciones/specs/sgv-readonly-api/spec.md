# Delta for sgv-readonly-api

## ADDED Requirements

### Requirement: Occupation API Contract

The system MUST expose `api/v1/ocupaciones` as a documented consumer-facing resource. It MUST support `GET /api/v1/ocupaciones`, `GET /api/v1/ocupaciones/{id}`, `POST /api/v1/ocupaciones`, `PUT /api/v1/ocupaciones/{id}`, `PATCH /api/v1/ocupaciones/{id}/finalizar`, `PATCH /api/v1/ocupaciones/{id}/reactivar`, and `DELETE /api/v1/ocupaciones/{id}`. Successful responses MUST return consumer-safe occupation data and MUST NOT expose audit internals.

#### Scenario: Read occupation data through the public contract

- GIVEN occupations exist in persistence
- WHEN a client requests the collection or detail endpoint
- THEN the API MUST return consumer-facing occupation payloads
- AND the payloads MUST describe assignment lifecycle fields without exposing audit storage fields.

#### Scenario: Write occupation data through the public contract

- GIVEN a client targets occupations
- WHEN the client uses documented create, update, finalize, reactivate, or logical delete operations
- THEN the API MAY modify persisted occupation data according to the occupation management contract.

### Requirement: Occupation API Discoverability

The system MUST publish Swagger/OpenAPI documentation for the occupation resource, including its history-aware list behavior and lifecycle conflict responses.

#### Scenario: Discover occupation endpoints

- GIVEN the API documentation is available
- WHEN a client inspects the documented resources
- THEN `/api/v1/ocupaciones` MUST appear with its collection, detail, and lifecycle action endpoints.

#### Scenario: Discover occupation conflicts

- GIVEN the occupation write contract is documented
- WHEN a client reviews create, update, finalize, reactivate, or delete operations
- THEN the documentation MUST describe `404` for missing references
- AND it MUST describe `409` for inactive references, finalized non-editability, and active uniqueness collisions.
