# Delta for SGV Read-only API

## MODIFIED Requirements

### Requirement: Read-only Resource Access

The system MUST expose HTTP access to organizational units, roles, positions, and skills. It MUST return real persisted data for all supported resources. Organizational units MAY expose supported create, update, parent-change, and soft-delete actions; roles, positions, and skills MUST remain read-only in this version.
(Previously: all supported resources were strictly read-only and could not expose create, update, or delete behavior.)

#### Scenario: List supported resources

- GIVEN persisted organizational units, roles, positions, and skills exist
- WHEN a client requests each supported resource collection
- THEN the API MUST return the matching persisted records for each collection
- AND each response MUST be successful.

#### Scenario: Empty supported resource collection

- GIVEN a supported resource has no persisted records
- WHEN a client requests that resource collection
- THEN the API MUST return a successful response with an empty collection.

#### Scenario: Allow organizational unit writes only

- GIVEN a client targets organizational units
- WHEN the client uses a documented create, update, parent-change, or soft-delete action
- THEN the API MAY modify persisted organizational unit data according to the CRUD contract.

#### Scenario: Reject unrelated write operations

- GIVEN a client targets roles, positions, or skills
- WHEN the client attempts to create, update, or delete data through the API
- THEN the API MUST NOT modify persisted data
- AND the operation MUST NOT be exposed as a supported API action.

### Requirement: Public API Discoverability

The system MUST publish API documentation that allows consumers to discover the read-only endpoints, organizational unit write endpoints, and response contracts.
(Previously: documentation could only list read-only endpoints and had to exclude create, update, and delete operations for all supported resources.)

#### Scenario: Discover endpoints through API documentation

- GIVEN the API is running locally
- WHEN a client opens the API documentation
- THEN the documentation MUST list read endpoints for organizational units, roles, positions, and skills
- AND it MUST describe the successful response contract for each endpoint.

#### Scenario: Discover organizational unit write operations

- GIVEN organizational unit CRUD is supported
- WHEN a client inspects the API documentation
- THEN documented organizational unit create, update, parent-change, and soft-delete operations MUST be discoverable.

#### Scenario: Exclude unsupported operations from documentation

- GIVEN roles, positions, and skills remain read-only
- WHEN a client inspects the API documentation
- THEN create, update, and delete operations for those resources MUST NOT be documented as available actions.
