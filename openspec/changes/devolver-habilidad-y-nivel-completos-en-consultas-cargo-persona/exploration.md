## Exploration: Return full Habilidad and NivelHabilidad data for Cargo/Persona skill queries

### Current State
The current subresource queries `GET /api/v1/cargos/{cargoId}/skills` and `GET /api/v1/personas/{personaId}/skills` return only association identifiers. `CargoSkillDto` and `PersonaSkillDto` expose just `skillId` and `nivelId`, and both list services map directly from `CargoHabilidad.HabilidadId` plus `NivelRequeridoId` / `NivelHabilidadId`. The persistence repositories also read only association rows, without eager-loading `Habilidad` or `NivelHabilidad`, so the query path cannot currently return full catalog data.

The previous OpenSpec change `asociar-habilidades-cargos-personas` intentionally locked the contract to `skillId` and `nivelId`, was verified as archive-ready, and should be treated as completed scope rather than reopened.

### Affected Areas
- `src/SGV.Api/Controllers/CargosController.cs` — `GetSkills` response contract for cargo skill queries.
- `src/SGV.Api/Controllers/PersonasController.cs` — `GetSkills` response contract for persona skill queries.
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDto.cs` — current cargo read model is too small.
- `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaSkillDto.cs` — current persona read model is too small.
- `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadDto.cs` — reusable consumer-safe model for nested skill data.
- `src/SGV.Aplicacion/Organizacion/Comandos/ICargoSkillServicio.cs` — list contract may need a query-specific DTO.
- `src/SGV.Aplicacion/Personas/Comandos/IPersonaSkillServicio.cs` — list contract may need a query-specific DTO.
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` — current list path maps IDs only.
- `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs` — current list path maps IDs only.
- `src/SGV.Infraestructura/Persistencia/Repositorios/CargoSkillRepository.cs` — current query does not include `Habilidad` or `NivelRequerido`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaSkillRepository.cs` — current query does not include `Habilidad` or `NivelHabilidad`.
- `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` — current domain mapping discards navigation data needed by richer query responses.
- `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` — asserts only `skillId`/`nivelId` today.
- `tests/SGV.Tests/Api/PersonaSkillControllerTests.cs` — asserts only `skillId`/`nivelId` today.
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` — list tests assume ID-only DTOs.
- `tests/SGV.Tests/Aplicacion/Personas/PersonaSkillServicioTests.cs` — list tests assume ID-only DTOs.
- `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` — should verify full query loading without N+1 behavior.
- `tests/SGV.Tests/Persistencia/PersonaSkillRepositoryTests.cs` — should verify full query loading without N+1 behavior.
- `openspec/changes/asociar-habilidades-cargos-personas/specs/*` — evidence that the prior change explicitly targeted minimal DTOs.

### Approaches
1. **Expand the existing shared DTOs** — Add nested full `skill` and `nivel` data to `CargoSkillDto` and `PersonaSkillDto`, and keep using those same DTOs for GET and PUT responses.
   - Pros: Single contract per subresource; additive if `skillId`/`nivelId` are preserved; simplest API surface for consumers.
   - Cons: Broadens write-response contracts too; touches command results, fakes, and more tests than the read requirement strictly needs.
   - Effort: Medium

2. **Create query-specific detailed DTOs for GET only** — Keep write responses minimal, but make `ListAsync` and GET endpoints return a richer read model such as `{ skillId, nivelId, skill, nivel }`.
   - Pros: Smallest behavioral scope; aligns directly with the request; limits backward-compatibility risk for PUT/DELETE consumers.
   - Cons: GET and PUT responses become asymmetric; requires extra DTOs and list-specific wiring.
   - Effort: Medium

### Recommendation
Use **Approach 2** with an additive read contract: keep `skillId` and `nivelId`, and add nested consumer-safe `skill` and `nivel` objects for the two GET subresources only. That keeps the change focused on query behavior, avoids unnecessary write-contract churn, and gives clients a safer migration path.

Implementation planning should prefer **single-query eager loading or projection** from the repositories for `Habilidad` and `NivelHabilidad`. Do NOT resolve each nested object with per-row repository calls in the application service, because that would introduce an avoidable N+1 query pattern.

Likely new DTOs for proposal/design:
- `NivelHabilidadDto` with `id`, `codigo`, `nombre`, `valorNumerico`, `orden`
- `CargoSkillDetailDto` and `PersonaSkillDetailDto` with `skillId`, `nivelId`, `skill: HabilidadDto`, `nivel: NivelHabilidadDto`

### Risks
- Breaking contract risk if the GET payload replaces `skillId`/`nivelId` instead of extending them.
- N+1 or over-fetching risk if nested `Habilidad` / `NivelHabilidad` data is loaded row by row instead of with `Include` or projection.
- Scope creep risk if the team also changes PUT/DELETE responses in the same slice without an explicit decision.
- Test churn across API, application, persistence, Swagger, and fake services because the current suite is anchored to ID-only DTOs.
- Prior-change overlap risk if the proposal tries to “extend” `asociar-habilidades-cargos-personas` instead of treating this as a new contract change.

### Ready for Proposal
Yes — create a **new** OpenSpec change focused on enriching the Cargo/Persona skill query contract. The proposal should state that the existing assignment change remains complete, and this new change only upgrades the read payload so clients receive full Habilidad and NivelHabilidad data without extra round trips.
