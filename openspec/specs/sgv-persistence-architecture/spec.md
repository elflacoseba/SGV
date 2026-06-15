# SGV Persistence Architecture

## Requirements

### Requirement: EF Persistence Model Boundary

The system MUST keep Entity Framework persistence models in the Infrastructure layer and MUST NOT require Domain entities to know about Entity Framework mapping, tracking, or configuration concerns. EF-mapped SGV infrastructure persistence types MUST be identifiable as persistence types by using the `Entity` suffix, except framework-owned Identity internals.

#### Scenario: Domain model remains EF-agnostic

- GIVEN the SGV persistence model is used by Infrastructure
- WHEN Domain entities are inspected as business model types
- THEN they MUST NOT require EF Core mapping metadata or persistence configuration
- AND they MUST remain usable as Domain concepts independent of the database provider.

#### Scenario: EF-mapped SGV tables use persistence entities

- GIVEN an SGV table is mapped by the Infrastructure persistence context
- WHEN the mapped CLR type represents SGV application data
- THEN the mapped type MUST be an Infrastructure persistence type suffixed with `Entity`
- AND framework-owned Identity internals MAY keep their provider-owned types.

### Requirement: Observable Persistence Invariants

This refactor MUST preserve the existing database schema, persisted seed content, query results, repository-visible behavior, and public application/API contracts. It MUST NOT introduce table renames, column renames, key changes, index changes, constraint changes, data transformations, or contract shape changes.

#### Scenario: Schema remains unchanged

- GIVEN the current SGV MySQL/Pomelo persistence schema is the baseline
- WHEN the refactor is applied and persistence metadata is compared to the baseline
- THEN the database tables, columns, keys, indexes, constraints, and relationships MUST remain equivalent.

#### Scenario: Consumers observe the same behavior

- GIVEN existing persisted data and seed data are available
- WHEN application repositories or public read-only API contracts are exercised
- THEN returned results and contract shapes MUST remain equivalent to the pre-refactor behavior
- AND no new behavior or unsupported operation MUST be exposed.

### Requirement: Audit Logical Name Preservation

Audit records MUST preserve the existing logical entity names and observable audit semantics. Persistence CLR type names introduced for the refactor MUST NOT leak `Entity` suffixes into audit data when that would change previously observable logical names.

#### Scenario: Audit entries keep logical entity names

- GIVEN an audited SGV entity is created, modified, or deleted
- WHEN audit records are persisted after the refactor
- THEN the audited entity name MUST match the pre-refactor logical name
- AND audit operation, entity identifier, user, timestamp, old values, and new values MUST retain their observable semantics.
