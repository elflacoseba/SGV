# Unidad Organizativa CRUD Specification

## Purpose

Define managed create, update, parent-change, read, and soft-delete behavior for organizational units.

## Requirements

### Requirement: Manage Organizational Units

The system MUST allow clients to create, update, read, re-parent, and soft-delete organizational units through stable API contracts.

#### Scenario: Create organizational unit

- GIVEN valid organizational unit data with a unique active code
- WHEN a client creates the unit
- THEN the system MUST persist it
- AND return the created unit contract.

#### Scenario: Update organizational unit

- GIVEN an active organizational unit exists
- WHEN a client updates editable fields with valid data
- THEN the system MUST persist the changes
- AND preserve its identifier.

#### Scenario: Soft-delete organizational unit

- GIVEN an active organizational unit exists
- WHEN a client deletes the unit
- THEN the system MUST mark it inactive or deleted
- AND exclude it from active read results.

### Requirement: Validate Organizational Unit Writes

The system MUST reject invalid organizational unit writes before committing partial changes.

#### Scenario: Reject duplicate active code

- GIVEN an active organizational unit already uses a code
- WHEN a client creates or updates another active unit with that code
- THEN the system MUST reject the operation with a predictable conflict error.

#### Scenario: Reject invalid hierarchy change

- GIVEN an organizational unit exists in a hierarchy
- WHEN a client sets itself or one of its descendants as parent
- THEN the system MUST reject the operation
- AND MUST NOT change the hierarchy.
