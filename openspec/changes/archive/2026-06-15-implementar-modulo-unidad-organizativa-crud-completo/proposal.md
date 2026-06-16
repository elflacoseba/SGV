# Proposal: Implement UnidadOrganizativa CRUD

## Intent

Enable managed CRUD for `UnidadOrganizativa` while preserving Clean Architecture. This changes the current read-only API assumption.

## Scope

### In Scope
- Add API create, update, parent change, list, get by id, and deactivate/delete.
- Use application-layer commands/use cases, write repository operations, and UnitOfWork.
- Define soft delete, cycle prevention, validation/errors, and duplicate active `Codigo` handling.
- Plan reviewable slices because full CRUD likely exceeds 400 changed lines.

### Out of Scope
- CRUD for roles, positions, skills, vacancies, or other SGV resources.
- Authentication/authorization unless a later spec changes anonymous access.
- Hard deletes with dependent children or positions.
- Database provider changes or SQL Server support.

### Non-goals
- Rewriting the domain model or EF persistence boundary.
- Changing read DTO shape unless writes require it.

## Capabilities

### New Capabilities
- `unidad-organizativa-crud`: API/application writes, validation, errors, and soft delete.

### Modified Capabilities
- `sgv-readonly-api`: allow writes for organizational units while unrelated resources stay read-only.
- `sgv-database`: clarify hierarchy, soft delete, and active-code uniqueness.

## Approach

Add application use cases/commands backed by write repository methods and UnitOfWork. Endpoints translate HTTP requests into commands and return stable success/error contracts. Parent changes reject self-parent and descendant-parent cycles. Delete deactivates/soft-deletes so read filters remain coherent.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `openspec/specs/sgv-readonly-api/spec.md` | Modified | No-write conflict. |
| `openspec/specs/sgv-database/spec.md` | Modified | Hierarchy, soft delete, unique active `Codigo`. |
| `src/SGV.Aplicacion/Organizacion` | Modified | Write use cases and validation. |
| `src/SGV.Infraestructura/Persistencia` | Modified | Write repository and UnitOfWork. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modified | Endpoints. |
| `tests/SGV.Tests` | Modified | Application, persistence, API coverage. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| API conflict with read-only spec | High | Modify/supersede requirement first. |
| Hierarchy cycles through parent updates | Medium | Validate descendants before saving parent changes. |
| Duplicate `Codigo` leaks as DB exception | Medium | Pre-check and map unique-index failures. |
| Review overload | High | Split implementation into chained reviewable slices. |

## Rollback Plan

Revert endpoints, write services, repository methods, and spec deltas. If a migration is later introduced, provide rollback or forward fix; prefer no schema change if existing soft-delete/index support is sufficient.

## Dependencies

- Existing domain methods, EF entity/configuration, UnitOfWork, and read services.

## Success Criteria

- [ ] Units can be created, updated, re-parented, read, and soft-deleted.
- [ ] Cycles, duplicate active `Codigo`, and validation failures return predictable errors without partial writes.
- [ ] Existing unrelated read-only resources remain read-only.
- [ ] Delivery is sliced to respect the 400-line review budget.
