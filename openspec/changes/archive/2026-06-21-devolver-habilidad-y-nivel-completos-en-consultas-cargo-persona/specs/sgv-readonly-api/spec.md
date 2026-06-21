# Delta for SGV Read-only API

## ADDED Requirements

### Requirement: Enriched Cargo and Persona skill query documentation

The system MUST document `GET /api/v1/cargos/{cargoId}/skills` and `GET /api/v1/personas/{personaId}/skills` as enriched read contracts that preserve required `skillId` and `nivelId` and add nested `skill` and `nivel` objects. This change MUST remain separate from the prior assignment change and MUST NOT imply changes to Cargo/Persona parent payloads or to PUT/DELETE subresource contracts.

#### Scenario: Document the enriched cargo skill query response

- GIVEN a client inspects the API documentation
- WHEN the documented response for `GET /api/v1/cargos/{cargoId}/skills` is reviewed
- THEN the success contract MUST show `skillId`, `nivelId`, `skill`, and `nivel`.

#### Scenario: Document the enriched persona skill query response

- GIVEN a client inspects the API documentation
- WHEN the documented response for `GET /api/v1/personas/{personaId}/skills` is reviewed
- THEN the success contract MUST show `skillId`, `nivelId`, `skill`, and `nivel`.

#### Scenario: Preserve scope boundaries in documentation

- GIVEN a client compares the prior assignment capability with this change
- WHEN the documentation is reviewed
- THEN it MUST describe this change as a GET-only response enrichment
- AND it MUST NOT document parent Cargo/Persona payload changes or new write-contract fields.
