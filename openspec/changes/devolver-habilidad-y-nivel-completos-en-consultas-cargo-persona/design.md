# Design: Return full Habilidad and NivelHabilidad data in Cargo/Persona skill queries

## Technical Approach

Use a GET-only read-model split. `PUT`/`DELETE` keep `CargoSkillDto` and `PersonaSkillDto` unchanged, while `GET /api/v1/cargos/{cargoId}/skills` and `GET /api/v1/personas/{personaId}/skills` move to dedicated detail DTOs that preserve `skillId` and `nivelId` and add nested `skill`/`nivel` objects. Repositories will satisfy the GET path with a single EF Core projection per request, aligned with the bounded-query requirements in `cargo-skill-query-contract` and `persona-skill-query-contract`.

## Architecture Decisions

| Decision | Options | Choice | Rationale |
|----------|---------|--------|-----------|
| DTO scope | Expand existing write DTOs / add GET-only DTOs | Add GET-only DTOs | Keeps write contracts stable and limits the change to the requested read behavior. |
| Query strategy | Row-by-row catalog lookups / `Include` + domain mapping / direct projection | Direct projection from repository | Avoids N+1, avoids extra mapper reflection work, and keeps query cost bounded to one SQL statement. |
| Shared nested models | Duplicate nested shapes / reuse catalog DTOs where possible | Reuse `HabilidadDto`, add shared `NivelHabilidadDto` | Minimizes duplication while defining the missing level payload explicitly. |

## Data Flow

`CargosController.GetSkills` / `PersonasController.GetSkills`
→ `ICargoSkillServicio.ListAsync` / `IPersonaSkillServicio.ListAsync`
→ `ICargoSkillRepository.ListDetailedByCargoIdAsync` / `IPersonaSkillRepository.ListDetailedByPersonaIdAsync`
→ EF Core `AsNoTracking + Where + Select`
→ `CargoSkillDetailDto[]` / `PersonaSkillDetailDto[]`

Write paths (`UpsertAsync`, `DeleteAsync`) keep using the current domain-based repository methods and current DTOs.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/NivelHabilidadDto.cs` | Create | Shared nested level payload: `id`, `codigo`, `nombre`, `valorNumerico`, `orden`. |
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs` | Create | GET-only Cargo skill contract with `skillId`, `nivelId`, `skill`, `nivel`. |
| `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaSkillDetailDto.cs` | Create | GET-only Persona skill contract with `skillId`, `nivelId`, `skill`, `nivel`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/ICargoSkillServicio.cs` | Modify | Change `ListAsync` return type to `IReadOnlyList<CargoSkillDetailDto>`. |
| `src/SGV.Aplicacion/Personas/Comandos/IPersonaSkillServicio.cs` | Modify | Change `ListAsync` return type to `IReadOnlyList<PersonaSkillDetailDto>`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` | Modify | Delegate GET list path to the new detailed repository method; keep write methods unchanged. |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs` | Modify | Same split for Persona. |
| `src/SGV.Aplicacion/Organizacion/Consultas/ICargoSkillRepository.cs` | Modify | Add `ListDetailedByCargoIdAsync(...)`. |
| `src/SGV.Aplicacion/Personas/Consultas/IPersonaSkillRepository.cs` | Modify | Add `ListDetailedByPersonaIdAsync(...)`. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/CargoSkillRepository.cs` | Modify | Project directly to `CargoSkillDetailDto` in one bounded query. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaSkillRepository.cs` | Modify | Project directly to `PersonaSkillDetailDto` in one bounded query. |
| `src/SGV.Api/Controllers/CargosController.cs` | Modify | GET response type becomes `IReadOnlyList<CargoSkillDetailDto>` only. |
| `src/SGV.Api/Controllers/PersonasController.cs` | Modify | GET response type becomes `IReadOnlyList<PersonaSkillDetailDto>` only. |
| `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | Modify | Assert GET returns nested `skill`/`nivel`; keep PUT/DELETE assertions unchanged. |
| `tests/SGV.Tests/Api/PersonaSkillControllerTests.cs` | Modify | Same for Persona. |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modify | Verify GET schemas document `skillId`, `nivelId`, `skill`, and `nivel` without changing write schemas. |
| `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Modify | Validate detailed GET mapping and that list path does not call catalog repositories. |
| `tests/SGV.Tests/Aplicacion/Personas/PersonaSkillServicioTests.cs` | Modify | Same for Persona. |
| `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` | Modify | Verify projected nested data and bounded-query behavior for Cargo list. |
| `tests/SGV.Tests/Persistencia/PersonaSkillRepositoryTests.cs` | Modify | Verify projected nested data and bounded-query behavior for Persona list. |

## Interfaces / Contracts

```csharp
public sealed record NivelHabilidadDto(Guid Id, string Codigo, string Nombre, byte ValorNumerico, int Orden);

public sealed record CargoSkillDetailDto(Guid SkillId, Guid NivelId, HabilidadDto Skill, NivelHabilidadDto Nivel);

public sealed record PersonaSkillDetailDto(Guid SkillId, Guid NivelId, HabilidadDto Skill, NivelHabilidadDto Nivel);
```

Repository list methods will project these DTOs directly from `CargoHabilidadEntity` / `PersonaHabilidadEntity`, `Habilidad`, and `NivelHabilidad` joins. Parent Cargo/Persona DTOs and write request/response contracts stay unchanged.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|--------------|----------|
| API | GET contract enrichment and write-contract stability | Controller tests + Swagger schema assertions. |
| Application | GET uses detailed repository output without per-row catalog lookups | Unit tests with fake repositories and call counters. |
| Persistence | Single-request projection returns nested catalog data with bounded command count | MySQL integration tests; add a lightweight command-count helper if needed. |

## Migration / Rollout

No migration required.

## Open Questions

- [ ] None blocking. Keep this change separate from `asociar-habilidades-cargos-personas`; do not edit prior change artifacts except for historical reference.
