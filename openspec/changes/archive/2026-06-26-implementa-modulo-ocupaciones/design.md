# Design: Implement Occupations Module

Implement `api/v1/ocupaciones` as a standalone historical-assignment module across `SGV.Api`, `SGV.Aplicacion`, `SGV.Dominio`, and `SGV.Infraestructura`. The design reuses the existing `Ocupaciones` schema and current Clean Architecture patterns already used by `Puestos` and `Personas`.

## Technical Approach

`Ocupacion` is not a catalog; its active state is derived from `FechaFin == null && IsDeleted == false`. Create opens an active assignment. Update and logical delete apply only to active rows. Finalize closes an active row by setting `FechaFin`. Reactivate explicitly means **reopening the same assignment row**, not creating a new period; therefore it clears `FechaFin` and `IsDeleted` after validations pass. This resolves the spec risk: reactivation is a corrective lifecycle action, not historical period splitting.

## Architecture Decisions

| Topic | Decision | Rationale |
|------|----------|-----------|
| Lifecycle model | Add domain methods `Actualizar(...)`, `Finalizar(...)`, `EliminarLogicamente()`, `Reactivar()` in `src/SGV.Dominio/Ocupaciones/Ocupacion.cs`. | Keeps invariants in domain and matches existing entity-centric behavior. |
| Reactivation semantics | Reopen the same record; preserve `FechaInicio`, clear `FechaFin`, clear delete metadata. | Fits spec requirement to reuse the same row and avoids a schema change for multi-period history. |
| Conflict handling | Pre-validate references/active uniqueness in application, but also translate DB unique violations by index name on save. | Friendly errors without losing race-safety. |
| Read model | Default collection returns active rows; `includeHistory=true` switches repository query to all non-physically-deleted rows. Detail always reads by id including historical rows. | Matches spec visibility rules and existing repository pattern. |

## Data Flow

```text
Client -> OcupacionesController -> Command/Query service
  -> IPersonaRepository / IPuestoRepository (reference state)
  -> IOcupacionRepository (read/write)
  -> IUnitOfWork.SaveChangesAsync
  -> DTO / ProblemDetails
```

Finalize/reactivate/delete use the same sequence, but load the occupation with history-aware state first.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Api/Controllers/OcupacionesController.cs` | Create | Resource routes, query param binding, ProblemDetails mapping. |
| `src/SGV.Aplicacion/Ocupaciones/**` | Create | Requests, DTOs, validators, query/command services, repository contract, command results. |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Modify | Lifecycle methods and invariant enforcement. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs` | Create | EF queries, history filter, persistence updates. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/{PersistenceToDomainMapper.cs,DomainToPersistenceMapper.cs}` | Modify | Add `Ocupacion` mapping. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modify | Register occupation repository/services. |
| `tests/SGV.Tests/{Dominio,Aplicacion,Api,Persistencia}/**Ocupacion*` | Create/Modify | Lifecycle, contract, mapping, and query coverage. |

## Interfaces / Contracts

```csharp
public sealed record CrearOcupacionRequest(Guid PersonaId, Guid PuestoId, DateOnly FechaInicio, TipoAsignacion TipoAsignacion, string? Observaciones);
public sealed record ActualizarOcupacionRequest(Guid PersonaId, Guid PuestoId, DateOnly FechaInicio, TipoAsignacion TipoAsignacion, string? Observaciones);
public sealed record FinalizarOcupacionRequest(DateOnly FechaFin, string? Observaciones);
public sealed record OcupacionDto(Guid Id, Guid PersonaId, string PersonaNombre, Guid PuestoId, string PuestoNombre, DateOnly FechaInicio, DateOnly? FechaFin, TipoAsignacion TipoAsignacion, string? Observaciones, string Estado);
```

Routes:
- `GET /api/v1/ocupaciones?includeHistory=false`
- `GET /api/v1/ocupaciones/{id}`
- `POST /api/v1/ocupaciones`
- `PUT /api/v1/ocupaciones/{id}`
- `PATCH /api/v1/ocupaciones/{id}/finalizar`
- `PATCH /api/v1/ocupaciones/{id}/reactivar`
- `DELETE /api/v1/ocupaciones/{id}`

Error mapping:
- `400`: FluentValidation shape errors; invalid date/domain arguments.
- `404`: occupation missing; referenced `Persona`/`Puesto` missing.
- `409`: inactive/deleted reference, finalized/deleted non-updatable row, invalid reactivation/delete transition, active unique conflict on `IX_Ocupaciones_ActivePuestoIdUnique` or `IX_Ocupaciones_ActivePersonaPuestoUnique`.

## Application and Infrastructure Design

Create `IOcupacionServicioConsulta` and `IOcupacionServicioComandos` plus `IOcupacionRepository`. Query service exposes `ListAsync(bool includeHistory)` and `GetByIdAsync(Guid id)`. Command service depends on `IPersonaRepository` and `IPuestoRepository`; it uses `GetByIdIncludingDeletedAsync` to distinguish `404` from `409` reference-state conflicts. Repository methods should include `GetByIdForUpdateAsync`, `GetByIdIncludingHistoryAsync`, `ListAsync(bool includeHistory)`, and uniqueness probes excluding the current id. EF queries should join `Persona` and `Puesto`, default to `AsNoTracking`, order active lists by `FechaInicio desc`, and rely on existing computed-column unique indexes for last-line enforcement.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Domain | Active-state invariant; update only active; finalize date rules; delete/reactivate transitions | xUnit entity tests in `tests/SGV.Tests/Dominio/Ocupaciones/`. |
| Application | Validator failures, 404 vs 409 reference resolution, finalized-not-editable, reactivation reopening semantics, DB conflict translation | Command/query service tests with fakes/mocks in `tests/SGV.Tests/Aplicacion/Ocupaciones/`. |
| Infrastructure | Mapper fidelity, default vs history queries, uniqueness/index behavior, history detail reads | EF/MySQL persistence tests in `tests/SGV.Tests/Persistencia/`. |
| API | Routes, `includeHistory`, ProblemDetails status codes, Swagger path visibility | Controller + swagger tests in `tests/SGV.Tests/Api/`. |

## Migration / Rollout

No schema migration is needed. `Ocupaciones` already has `FechaInicio`, nullable `FechaFin`, soft-delete audit columns, foreign keys, and active uniqueness computed indexes in `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs`. The chosen reactivation semantics reuse those columns instead of introducing new timeline fields.

## Open Questions

- [ ] None blocking; proceed to tasks.
