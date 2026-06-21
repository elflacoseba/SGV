# Delta for SGV Persistence Architecture

## ADDED Requirements

### Requirement: Identity Infrastructure Boundary

The system MUST treat authentication users and roles as Infrastructure/API concerns and MUST NOT require Domain entities to depend on Identity framework types. Application-facing contracts MAY describe authenticated users and roles, but MUST NOT expose persistence entities or framework-owned Identity internals as SGV Domain models.

#### Scenario: Domain remains Identity-agnostic

- GIVEN authentication support is enabled for SGV
- WHEN Domain model types are inspected
- THEN they MUST NOT depend on Identity framework types
- AND Persona MUST remain a Domain concept independent of authentication storage.

#### Scenario: Consumer contracts hide framework internals

- GIVEN a consumer manages users or roles
- WHEN the system returns user or role data
- THEN the response MUST use consumer-safe contracts
- AND MUST NOT expose persistence tracking or framework-owned internals.

### Requirement: Approved Identity Persistence Evolution

The system MAY introduce Identity-specific persistence customization only to satisfy SGV authentication behavior: mandatory Persona association, fixed first-slice roles, and role assignments constrained to that catalog. This evolution MUST preserve the Clean Architecture boundary described by the persistence model requirements.

#### Scenario: Identity persistence change is scoped

- GIVEN this change introduces user and role management
- WHEN persistence behavior changes for authentication data
- THEN the change MUST be limited to mandatory Persona linkage and fixed role catalog behavior
- AND MUST NOT alter unrelated SGV domain persistence behavior.
