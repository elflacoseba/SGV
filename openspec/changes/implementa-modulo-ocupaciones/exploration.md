## Exploration: Implement Occupations module

### Current State
`Ocupacion` already exists only as domain + persistence infrastructure. The repo has a domain entity in `src/SGV.Dominio/Ocupaciones/Ocupacion.cs`, enum `TipoAsignacion` in `src/SGV.Dominio/Ocupaciones/TipoAsignacion.cs`, EF entity/configuration in `src/SGV.Infraestructura/Persistencia/{Entidades/OcupacionEntity.cs,Configuraciones/OcupacionConfiguracion.cs}`, `DbSet` registration in `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs`, and domain/model tests in `tests/SGV.Tests/{Dominio/Ocupaciones/OcupacionTests.cs,Persistencia/ModeloPersistenciaTests.cs}`. There is NO application slice for Ocupaciones yet: no DTOs, no requests, no validators, no repository contract/implementation, no query/command services, no DI registrations, no API controller, and no Swagger/API tests. Current business rules already enforce one active occupation per `Puesto`, one active occupation per `Persona + Puesto`, `FechaFin >= FechaInicio`, and numeric enum persistence for `TipoAsignacion`.

### Affected Areas
- `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` — current lifecycle only supports create + finalize; no update/reactivate semantics exist.
- `src/SGV.Dominio/Ocupaciones/TipoAsignacion.cs` — persisted enum contract already fixed at numeric values 0/1/2.
- `src/SGV.Infraestructura/Persistencia/Entidades/OcupacionEntity.cs` — persistence entity exists and is API-ready only at storage level.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` — MySQL-compatible uniqueness and date constraints already exist.
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs` — latest migration already aligned the schema with concurrent occupations and enum storage.
- `src/SGV.Infraestructura/Persistencia/Mapeos/{PersistenceToDomainMapper.cs,DomainToPersistenceMapper.cs}` — no Ocupacion mapping methods exist yet.
- `src/SGV.Infraestructura/DependencyInjection.cs` — no Ocupacion repository/service registrations exist.
- `src/SGV.Api/Controllers/{PuestosController.cs,CargosController.cs,PersonasController.cs}` — strongest CRUD/subresource patterns to mirror.
- `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` — reference for CRUD command flow with validators, typed errors, and Unit of Work.
- `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs` — reference for assignment-style subresource operations when the resource links two aggregates.
- `tests/SGV.Tests/Api/{PuestosControllerTests.cs,PersonasControllerTests.cs,SwaggerConfigurationTests.cs}` — controller + Swagger patterns to replicate.
- `openspec/specs/{sgv-database/spec.md,sgv-readonly-api/spec.md}` — database rules exist, but there is no current API/module contract for Ocupaciones.

### Approaches
1. **Standalone Occupations module** — create `api/v1/ocupaciones` with list/detail/create/update/finalize-or-delete/reactivate semantics defined explicitly for the occupation lifecycle.
   - Pros: clean module boundary, matches the user request for a dedicated module/controller, avoids coupling the feature to either Personas or Puestos, easier Swagger discoverability.
   - Cons: requires defining contracts that are NOT obvious from the current domain, especially what “update”, “delete”, and “reactivate” mean for a historical assignment.
   - Effort: High

2. **Occupations as subresource under Personas or Puestos** — model write operations similarly to `PersonaSkill` / `CargoSkill` assignments.
   - Pros: fits that an occupation links `Persona` and `Puesto`, can reduce route ambiguity for parent context.
   - Cons: clashes with the user request for “its controller”, duplicates navigation from two sides, and makes “complete management” harder because both parents are first-class in the aggregate relationship.
   - Effort: Medium/High

### Recommendation
Prefer a standalone `OcupacionesController` proposal, but FIRST define the lifecycle contract before implementation. The current repo proves storage/model readiness, not module readiness. The safest next phase is a proposal/spec that settles whether Ocupaciones follows catalog-style CRUD (`GET/POST/PUT/DELETE/PATCH reactivate`) like `PuestosController`, or assignment/history semantics (`GET/POST/PATCH finalizar` with limited editing). Without that decision, “all methods in its controller” is underspecified and likely to produce the wrong API.

### Risks
- The biggest ambiguity is lifecycle: `Ocupacion` is not a simple catalog. It has historical dates plus audit soft-delete fields, but the domain only exposes `Finalizar(...)`, not `Actualizar`, `Desactivar`, or `Activar`.
- A CRUD controller copied blindly from `PuestosController` would over-promise behavior the domain does not currently define.
- Any create/update flow must validate active `Persona` and active `Puesto`, and must surface MySQL uniqueness conflicts for `Puesto` and `Persona + Puesto` as business `409` responses.
- There is no current application/persistence mapper for Ocupacion, so the feature is broader than “add controller methods”.
- Schema work may be unnecessary: the latest migration already covers enum persistence and concurrent occupations. A new migration should only happen if the proposal adds new fields/indexes.
- The branch requirement is already satisfied: the workspace is currently on `implementa-modulo-ocupaciones`.

### Ready for Proposal
Yes — with one critical clarification to settle in proposal/spec: should Ocupaciones be a true CRUD module like Puestos/Personas, or a historical assignment module with create/query/finalize semantics and only limited updates?
