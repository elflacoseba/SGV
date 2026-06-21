# Proposal: Return full Habilidad and NivelHabilidad data in Cargo/Persona skill queries

## Intent

Enrich only `GET /api/v1/cargos/{cargoId}/skills` and `GET /api/v1/personas/{personaId}/skills` so consumers receive full nested `skill` and `nivel` data without extra round trips. This is a new contract-enrichment change, separate from the completed assignment change that introduced the subresources with `skillId` and `nivelId` only.

## Scope

### In Scope
- Add nested consumer-safe `skill` and `nivel` objects to the two GET skill subresources.
- Preserve `skillId` and `nivelId` in those GET responses for backward compatibility.
- Define performance expectations so later design uses single-query eager loading or projection, not per-row lookups.

### Out of Scope
- Changes to Cargo or Persona parent payloads.
- Changes to PUT/DELETE skill subresource response contracts.
- Changes to the completed `asociar-habilidades-cargos-personas` scope beyond referencing it as prior context.

## Non-goals

- Replacing `skillId` or `nivelId` with nested-only fields.
- Adding new write behaviors, filters, pagination, or catalog fields.
- Reworking unrelated Habilidad, NivelHabilidad, Cargo, or Persona contracts.

## Capabilities

### New Capabilities
- `cargo-skill-query-contract`: GET contract for Cargo skill subresource with additive nested skill and level data.
- `persona-skill-query-contract`: GET contract for Persona skill subresource with additive nested skill and level data.

### Modified Capabilities
- `sgv-readonly-api`: document enriched GET response contracts for the two skill subresources.

## Approach

Introduce query-specific detailed read DTOs for GET only, keeping write DTOs unchanged. Reuse the existing `HabilidadDto` shape and add a consumer-safe `NivelHabilidadDto`. Repository/query paths must load the related `Habilidad` and `NivelHabilidad` data through eager loading or projection in one query per endpoint path.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Api/Controllers/CargosController.cs` | Modified | GET cargo skills response contract/documentation |
| `src/SGV.Api/Controllers/PersonasController.cs` | Modified | GET persona skills response contract/documentation |
| `src/SGV.Aplicacion/Organizacion/**` | Modified | Query DTO/service contract for cargo skill reads |
| `src/SGV.Aplicacion/Personas/**` | Modified | Query DTO/service contract for persona skill reads |
| `src/SGV.Infraestructura/Persistencia/Repositorios/*SkillRepository.cs` | Modified | Single-query related data loading |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Breaking consumers by changing existing fields | Low | Keep `skillId` and `nivelId` additive |
| N+1 query behavior | Medium | Require eager loading/projection and repository-level tests |
| Scope creep into write contracts or parent payloads | Medium | Limit change to GET subresources only |

## Rollback Plan

Revert the enriched GET DTO wiring and controller contracts, restoring the current ID-only query payload while leaving assignment endpoints unchanged.

## Dependencies

- Existing `HabilidadDto` contract and current Cargo/Persona skill subresource endpoints.

## Success Criteria

- [ ] Specs clearly define additive GET-only nested `skill` and `nivel` contracts for Cargo and Persona skill queries.
- [ ] Specs keep `skillId` and `nivelId` required in the enriched GET responses.
- [ ] Design explicitly forbids N+1 loading for nested catalog data.
