# Proposal: Refactor EF persistence entities

## Intent

Separate EF Core persistence models from Domain business entities across the Infrastructure persistence layer. Every EF-mapped infrastructure table should be represented by an Infrastructure persistence type suffixed with `Entity`, while Domain entities remain EF-agnostic business classes. This is a pure internal refactor with no intended observable behavior, schema, contract, seed data, or audit-semantic change.

## Scope

### In Scope
- Replace direct EF mapping of SGV Domain classes with `*Entity` persistence classes for all tables represented by `SgvDbContext`, excluding framework-owned Identity internals.
- Retarget EF configurations, `DbSet` declarations, seed data, repositories, and relationship mappings to persistence entities.
- Preserve current database table names, columns, keys, indexes, constraints, seed values, migration intent, public contracts, repository results, and logical audit entity names.

### Out of Scope
- New features, API/application contract changes, database redesign, table/column renames, data migrations that alter persisted values, or behavior changes.
- Related cleanup not required for the split, such as query optimization, repository redesign, DTO redesign, or changing Identity persistence.
- Creating specs, design, tasks, or implementation code in this phase.

## Non-goals

- Do not change Domain behavior or make Domain aware of EF Core.
- Do not introduce spec-level behavior changes.
- Do not change observable audit semantics; persisted audit records should keep existing logical entity naming.

## Capabilities

### New Capabilities
None

### Modified Capabilities
None

## Approach

Create Infrastructure persistence classes such as `CargoEntity`, `HabilidadEntity`, `PuestoEntity`, `VacanteEntity`, `AuditoriaEntity`, and corresponding entities for every EF-mapped SGV table. Move EF configuration targets from Domain types to persistence types, then map at repository/application boundaries so callers still receive Domain models or existing response contracts. Keep EF table mappings explicit to prevent schema drift, and normalize audit names so `*Entity` CLR names do not leak into audit data.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` | Modified | Use `DbSet<*Entity>` for SGV persistence tables while preserving table names. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs` | Modified | Retarget all EF configurations and relationships to persistence entities. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/*.cs` | Modified | Map persistence entities to/from Domain models without changing repository behavior. |
| `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` | Modified | Seed the same data through persistence entities. |
| `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` | Modified | Preserve existing logical audit entity names. |
| `tests/SGV.Tests/Persistencia/**` | Modified | Re-verify schema, mappings, repositories, seed data, and audit semantics. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| EF generates unintended schema diffs | Medium | Keep explicit mappings and compare model snapshot/migration output against current schema. |
| Audit records expose `*Entity` names | Medium | Add/keep logical-name normalization and regression coverage. |
| Relationship mapping regressions | Medium | Cover repositories, relationships, and seed data with persistence tests. |

## Rollback Plan

Revert the refactor commit(s), restore Domain classes as EF-mapped CLR types in `SgvDbContext` and configurations, remove `*Entity` persistence classes/mappers, and discard any migration/model-snapshot noise not required to preserve the current schema.

## Dependencies

- Existing `sgv-database` and `sgv-readonly-api` behavior remain the source of truth.
- EF Core/Pomelo/MySQL mappings must remain schema-compatible with the current model.

## Success Criteria

- [ ] All SGV EF-mapped infrastructure tables use Infrastructure `*Entity` persistence types, except framework-owned Identity internals.
- [ ] Domain entities remain independent of EF Core and are not exposed as persistence models.
- [ ] No database schema, seed content, API contract, repository behavior, or audit semantic changes are introduced.
