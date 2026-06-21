# Tasks: Return full Habilidad and NivelHabilidad data in Cargo/Persona skill queries

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 520-700 |
| 400-line budget risk | High |
| 800-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 shared DTOs + Cargo slice -> PR 2 Persona slice + final Swagger/persistence checks |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Add shared/nested DTOs and complete Cargo GET read slice with tests | PR 1 | Base = feature branch or size-exception if kept single PR |
| 2 | Complete Persona GET read slice and lock Swagger/persistence regressions | PR 2 | Base = PR 1 branch if chained |

## Phase 1: RED API and contract tests

- [x] 1.1 Update `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` to fail first on GET items missing nested `skill`/`nivel`, while PUT/DELETE assertions stay on `CargoSkillDto` only.
- [x] 1.2 Update `tests/SGV.Tests/Api/PersonaSkillControllerTests.cs` with the same GET-only enrichment and unchanged write-contract assertions.
- [x] 1.3 Extend `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` to fail first unless both GET endpoints document `skillId`, `nivelId`, `skill`, and `nivel`, and write schemas remain unchanged.

## Phase 2: Shared read contracts and application interfaces

- [x] 2.1 Create `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/NivelHabilidadDto.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs`, and `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaSkillDetailDto.cs`.
- [x] 2.2 Modify `src/SGV.Aplicacion/Organizacion/Comandos/ICargoSkillServicio.cs` and `src/SGV.Aplicacion/Personas/Comandos/IPersonaSkillServicio.cs` so `ListAsync` returns detail DTOs only for GET paths.
- [x] 2.3 Modify `src/SGV.Aplicacion/Organizacion/Consultas/ICargoSkillRepository.cs` and `src/SGV.Aplicacion/Personas/Consultas/IPersonaSkillRepository.cs` to add direct-projection list methods for detailed reads without changing write members.

## Phase 3: GREEN implementation by bounded query path

- [x] 3.1 Update `src/SGV.Infraestructura/Persistencia/Repositorios/CargoSkillRepository.cs` to project `CargoSkillDetailDto` with `AsNoTracking + Where + Select` in one query; keep current write methods intact.
- [x] 3.2 Update `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaSkillRepository.cs` with the same single-query projection pattern.
- [x] 3.3 Update `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` and `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs` so GET delegates to detailed repository methods and never performs per-row catalog lookups.
- [x] 3.4 Update `src/SGV.Api/Controllers/CargosController.cs` and `src/SGV.Api/Controllers/PersonasController.cs` so only GET response types move to detail DTOs.

## Phase 4: GREEN application and persistence verification

- [x] 4.1 Update `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` and `tests/SGV.Tests/Aplicacion/Personas/PersonaSkillServicioTests.cs` to verify detailed GET results and zero catalog-repository calls on list scenarios.
- [x] 4.2 Update `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` and `tests/SGV.Tests/Persistencia/PersonaSkillRepositoryTests.cs` to verify nested DTO data, empty-list behavior, and bounded per-request query execution.
- [ ] 4.3 Refactor duplicated test helpers only inside the touched skill-query test files; keep this change separate from `asociar-habilidades-cargos-personas` artifacts and behavior.
