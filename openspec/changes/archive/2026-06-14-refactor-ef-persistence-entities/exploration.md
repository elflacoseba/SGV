## Exploration: Refactor EF persistence entities from domain entities

### Current State
Entity Framework currently maps SGV domain classes directly. `SgvDbContext` exposes `DbSet<T>` for domain types such as `Cargo`, `Puesto`, `Vacante`, and `Auditoria` (`src/SGV.Infraestructura/Persistencia/SgvDbContext.cs`). Infrastructure configurations also target domain types through `IEntityTypeConfiguration<T>` (`src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs`).

This means Infrastructure depends on the Domain object model for:
- table mappings and indexes,
- relationships and navigation loading,
- seed data,
- audit interception,
- repository query materialization.

The Domain layer itself does not reference EF Core APIs, but it is still persistence-shaped in practice because many entities include EF-friendly private constructors, private setters, navigation properties, and collection backing fields that EF materializes directly.

### Affected Areas
- `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` — all `DbSet<T>` declarations currently point to domain classes.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs` — 19 configuration files map domain classes directly to tables.
- `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` — seed data currently instantiates domain classes.
- `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` — audit logic inspects tracked domain entities and persists `ClrType.Name`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/*.cs` — repositories query EF directly as domain entities; `ReadOnlyRepository<T>` assumes `Set<T>()` returns domain objects.
- `src/SGV.Infraestructura/Persistencia/Migraciones/*.cs` — snapshots and migrations currently describe domain CLR types.
- `src/SGV.Aplicacion/*/Consultas/*Repository.cs` and query services — application contracts currently expect domain entities to come out of repositories.
- `src/SGV.Dominio/**/*.cs` — business entities currently carry persistence-oriented shape (parameterless constructors, navigation properties, private setters, audit fields in base classes).
- `tests/SGV.Tests/Persistencia/*.cs` — persistence tests inspect the EF model using current CLR types and current repositories.

### Approaches
1. **Full persistence model split** — Introduce `*Entity` classes in Infrastructure for every EF-mapped table and map repositories between persistence entities and domain entities.
   - Pros: cleanest architecture boundary; EF concerns stay in Infrastructure; Domain stops being the EF model.
   - Cons: largest change surface; requires explicit mapping for graphs, collections, and audit behavior; likely touches all repositories, configs, seeds, migrations, and persistence tests.
   - Effort: High

2. **Phased persistence model split** — First introduce the pattern and apply it to the smallest read-only slice, then expand entity-by-entity while preserving schema and contracts.
   - Pros: safer rollout; proves mapping pattern before touching complex aggregates; easier review and rollback.
   - Cons: temporary mixed model during the transition; requires discipline so both patterns do not drift.
   - Effort: Medium

### Recommendation
Use **Approach 2**.

This looks like a **refactor with behavior-preservation as the default goal**, not a schema redesign. The safest target is:
- keep table names, columns, indexes, constraints, and foreign keys unchanged,
- keep application repository contracts returning domain entities,
- introduce Infrastructure-only persistence types with the `Entity` suffix,
- add explicit mappers between persistence entities and domain entities,
- preserve audit semantics by recording logical/domain names instead of raw persistence CLR names.

The smallest safe slice is a shallow read-only catalog such as `Cargo` or `Habilidad`. Those repositories have simple filters and no deep aggregate reconstruction. `Puesto`, `Vacante`, `Postulacion`, and historical entities should come later because they rely on richer relationships.

### Risks
- **Audit behavior drift**: `AuditoriaSaveChangesInterceptor` currently records `entrada.Metadata.ClrType.Name`; after the split it would record names like `CargoEntity` unless explicitly normalized. That is observable persisted behavior.
- **Graph reconstruction complexity**: current repositories return domain entities with loaded navigations (`Puesto` includes `UnidadOrganizativa` and `Cargo`). Separate persistence entities require explicit mapping/hydration.
- **Invariant duplication**: domain constructors/behaviors enforce business rules, while persistence entities will allow raw materialization. Mapping must avoid creating invalid domain objects or bypassing invariants inconsistently.
- **Migration churn**: even if the database schema is unchanged, EF snapshots and generated migrations may show noisy CLR-type changes. The rollout must avoid accidental schema diffs.
- **Test fallout**: persistence tests currently inspect EF metadata using domain CLR types and will need to be rewritten around persistence entities while preserving the same database guarantees.
- **Partial split trap**: if repositories, seeds, and interceptors are not migrated together, the solution can end up with two incompatible models.

### Ready for Proposal
Yes — if the proposal is explicit that this is a phased refactor that preserves the current database schema and API behavior. The proposal should also call out one intentional decision up front: whether audit records must continue exposing logical/domain entity names instead of new `*Entity` CLR names.
