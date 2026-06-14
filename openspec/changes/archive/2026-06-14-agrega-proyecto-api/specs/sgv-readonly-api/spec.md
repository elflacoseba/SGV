# SGV Read-only API Specification

## Purpose

Expose SGV catalog and structure data through an external read-only HTTP API. The API MUST return persisted data for organizational units, roles, positions, and skills, and MUST NOT require authentication in this version.

## Requirements

### Requirement: Read-only Resource Access

The system MUST expose read-only HTTP access to organizational units, roles, positions, and skills. It MUST return real persisted data and MUST NOT expose create, update, or delete behavior for these resources.

#### Scenario: List supported resources

- GIVEN persisted organizational units, roles, positions, and skills exist
- WHEN a client requests each supported resource collection
- THEN the API MUST return the matching persisted records for each collection
- AND each response MUST be successful.

#### Scenario: Empty supported resource collection

- GIVEN a supported resource has no persisted records
- WHEN a client requests that resource collection
- THEN the API MUST return a successful response with an empty collection.

#### Scenario: Reject unsupported write operations

- GIVEN a client targets a supported resource
- WHEN the client attempts to create, update, or delete data through the API
- THEN the API MUST NOT modify persisted data
- AND the operation MUST NOT be exposed as a supported API action.

### Requirement: Response Contracts

The system MUST return response models intended for API consumers. Responses MUST NOT expose persistence or domain entities directly.

#### Scenario: Return consumer-safe resource data

- GIVEN persisted data exists for a supported resource
- WHEN a client requests that resource
- THEN the response MUST contain only consumer-facing fields for that resource
- AND the response MUST NOT include persistence tracking or internal audit fields unless explicitly specified by the API contract.

#### Scenario: Include relationships by identifier or summary

- GIVEN a position references an organizational unit and a role
- WHEN a client requests positions
- THEN each position response SHOULD identify its related organizational unit and role in a consumer-safe form.

### Requirement: Public API Discoverability

The system MUST publish API documentation that allows consumers to discover the read-only endpoints and response contracts.

#### Scenario: Discover endpoints through API documentation

- GIVEN the API is running locally
- WHEN a client opens the API documentation
- THEN the documentation MUST list read-only endpoints for organizational units, roles, positions, and skills
- AND it MUST describe the successful response contract for each endpoint.

#### Scenario: Exclude unsupported operations from documentation

- GIVEN this API version is read-only
- WHEN a client inspects the API documentation
- THEN create, update, and delete operations for supported resources MUST NOT be documented as available actions.

### Requirement: No Authentication Requirement

The system MUST allow access to the read-only endpoints without requiring authentication or authorization in this version.

#### Scenario: Anonymous client reads supported data

- GIVEN the API is running and persisted data exists
- WHEN an unauthenticated client requests a supported resource collection
- THEN the API MUST process the request without requiring credentials
- AND the response MUST follow the same contract as an authenticated request would.
