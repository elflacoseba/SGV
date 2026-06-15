# Design: Refactor EF persistence entities

## Technical Approach

Introduce Infrastructure-only EF persistence models suffixed with `Entity` for every SGV table currently mapped through Domain CLR types in `SgvDbContext`: `Auditoria`, `UnidadOrganizativa`, `Cargo`, `Habilidad`, `NivelHabilidad`, `CargoHabilidad`, `Persona`, `PersonaHabilidad`, `Puesto`, `Ocupacion`, `EstadoVacante`, `Vacante`, `HistorialEstadoVacante`, `Postulante`, `EstadoPostulacion`, `Postulacion`, `HistorialEstadoPostulacion`, and `EvaluacionPostulacion`. Identity types remain framework-owned.

The split is internal: EF tracks `*Entity` types; Application repository interfaces still return Domain models. Configurations keep the same table names, column names, keys, indexes, check constraints, generated-column uniqueness workarounds, seed values, and MySQL/Pomelo behavior.

## Architecture Decisions

| Decision | Choice | Alternatives considered | Rationale |
|---|---|---|---|
| Persistence namespace | Create `src/SGV.Infraestructura/Persistencia/Entidades/*Entity.cs` plus `AuditableEntityBase`/`EntityBase`. | Keep persistence classes beside Domain types; reuse Domain bases. | Keeps EF concerns inside Infrastructure and avoids Domain depending on persistence shape. |
| Mapping boundary | Add explicit Infrastructure mappers under `Persistencia/Mapeos`, used by repositories. | AutoMapper; returning persistence entities from Application. | Explicit mapping preserves Clean Architecture and makes graph hydration reviewable. Avoids adding a dependency for a refactor. |
| Repository pattern | Replace generic `ReadOnlyRepository<T>` with a two-type base such as `ReadOnlyRepository<TPersistence, TDomain>` or map in concrete repositories. | Keep `Context.Set<TDomain>()`. | EF will no longer know Domain types, so queries must start from persistence `DbSet`s and map before returning. |
| Audit names | Normalize persistence CLR names before writing `AuditoriaEntity.EntityName`. | Store raw `CargoEntity` names. | Proposal requires observable audit semantics to keep logical names such as `Cargo`, not `CargoEntity`. |
| Migration handling | Update snapshot only after proving no schema operations are generated; do not add a schema-changing migration. | Accept migration churn. | The change is CLR-model-only; MySQL schema and seed results must not drift. |

## Data Flow

Read path:

```text
API/Application service -> I*Repository -> SgvDbContext DbSet<*Entity>
    -> EF query/includes/filters -> Persistence mapper -> Domain entity -> DTO mapping
```

Write/audit path:

```text
Infrastructure write/test -> *Entity tracked by EF
    -> AuditoriaSaveChangesInterceptor -> technical audit fields + AuditoriaEntity
    -> logical EntityName without Entity suffix
```

For aggregate reads like `Puesto`, the repository must include `UnidadOrganizativaEntity` and `CargoEntity`, then map the graph so current `PuestoServicioConsulta` can still read `Puesto.UnidadOrganizativa.Nombre` and `Puesto.Cargo.Nombre`.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Infraestructura/Persistencia/Entidades/*.cs` | Create | Persistence classes for all SGV EF tables, including audit/base types. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/*.cs` | Create | Explicit conversion from persistence graphs to Domain models and, where needed, Domain/test data to persistence entities. |
| `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` | Modify | Change SGV `DbSet<T>` declarations to `DbSet<*Entity>`; keep Identity base unchanged. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs` | Modify | Retarget every `IEntityTypeConfiguration<T>` to `*Entity`; preserve table/schema details, relationships, shadow generated columns, and indexes. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/ConfiguracionComun.cs` | Modify | Constrain shared helpers to Infrastructure base entity types. |
| `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` | Modify | Seed same IDs and values through `NivelHabilidadEntity`, `EstadoVacanteEntity`, `EstadoPostulacionEntity`, `CargoEntity`, and `HabilidadEntity`; keep Identity role seeding. |
| `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` | Modify | Track `EntityBase`/`AuditableEntityBase`, create `AuditoriaEntity`, and normalize logical names. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/*.cs` | Modify | Query persistence entities and return Domain entities without changing repository interfaces. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Modify carefully | Allow CLR-type metadata to reflect new entities only after validating no DDL change. |
| `tests/SGV.Tests/Persistencia/**` | Modify/Add | Assert model, repositories, seeds, schema compatibility, and audit regressions using persistence types. |

## Interfaces / Contracts

No public Application/API contracts change. Repository interfaces in `src/SGV.Aplicacion/**/Consultas` continue returning Domain classes. Persistence entities are internal Infrastructure details and should not be referenced by Domain or Application.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Persistence model | Tables, PKs, FKs, indexes, check constraints, generated columns, no SQL Server filters, EF Core 9/Pomelo provider. | Update `ModeloPersistenciaTests` to use `*Entity` CLR types and relational metadata. |
| Repositories | Current filters/order/results for `Cargo`, `Habilidad`, `UnidadOrganizativa`, `Puesto`, including `Puesto` relations. | Existing MySQL repository tests, rewritten to seed persistence entities or map fixtures. |
| Audit | Created/updated/deleted technical fields, soft delete, `Auditorias.EntityName` logical value, sensitive-field exclusion. | Add MySQL regression tests around `AuditoriaSaveChangesInterceptor`. |
| Migration safety | No table/column/index/constraint/seed drift. | Generate a throwaway migration or script diff locally; fail if DDL operations appear beyond snapshot CLR-name updates. |

## Migration / Rollout

No data migration is required. Roll out in one internal refactor slice, but verify in this order: persistence entities and configurations, `SgvDbContext`, seed data, audit interceptor, repositories/mappers, tests, then snapshot. If EF scaffolds schema operations, discard the migration and fix mappings until the diff is empty.

## Open Questions

- [ ] Can Domain constructors/setters support full graph reconstruction cleanly, or will Infrastructure need limited internal reflection/factory helpers for behavior-preserving hydration?
