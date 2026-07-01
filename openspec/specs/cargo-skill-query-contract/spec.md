# Cargo Skill Query Contract Specification

## Purpose

Define the GET-only response contract for `/api/v1/cargos/{cargoId}/skills` without changing parent Cargo payloads or Cargo skill write contracts introduced by the prior assignment change.

## Requirements

### Requirement: Enriched cargo skill collection response

The system MUST return each Cargo skill association with required `skillId` and `nivelId`, and MUST also return nested `skill` and `nivel` objects in the same GET response. `skill` MUST expose `{ id, codigo, nombre, descripcion, categoria }`. `nivel` MUST expose `{ id, codigo, nombre, valorNumerico, orden }`.

#### Scenario: Return identifiers and nested catalog data

- GIVEN a Cargo has one or more associated skills
- WHEN a client requests `/api/v1/cargos/{cargoId}/skills`
- THEN each response item MUST include `skillId`, `nivelId`, `skill`, and `nivel`
- AND `skillId` and `nivelId` MUST remain populated even when the nested objects are present.

#### Scenario: Return an empty collection without shape changes

- GIVEN a Cargo exists and has no associated skills
- WHEN a client requests `/api/v1/cargos/{cargoId}/skills`
- THEN the API MUST return a successful empty collection.

### Requirement: Query-only contract scope

The system MUST apply this enrichment only to the GET Cargo skill collection contract. Cargo parent payloads and Cargo skill write contracts MUST remain unchanged in this change.

#### Scenario: Keep non-GET contracts unchanged

- GIVEN a client uses the existing Cargo skill write endpoints
- WHEN the response contract is evaluated for this change
- THEN the change MUST NOT require nested `skill` or `nivel` objects outside the GET collection response.

### Requirement: Bounded query execution for nested skill data

The system MUST resolve the nested `skill` and `nivel` data for one Cargo skill collection request with data-access work that stays bounded per request and MUST NOT grow linearly with the number of returned associations.

#### Scenario: Reject row-by-row catalog loading

- GIVEN a Cargo skill query returns multiple associations
- WHEN the request is verified against its data-access behavior
- THEN the nested `skill` and `nivel` data MUST NOT be loaded through one follow-up lookup per returned row.

### Requirement: Autorización del subrecurso de skills de cargos

`GET /api/v1/cargos/{cargoId}/skills` MUST requerir autenticación y MUST conservar el contrato enriquecido vigente. Las mutaciones del subrecurso `/skills` MUST requerir rol `Administrador` y, con payload válido y rol correcto, MUST conservar sus respuestas `2xx` vigentes.

#### Scenario: Consulta autenticada exitosa

- GIVEN un usuario autenticado
- WHEN solicita `GET /api/v1/cargos/{cargoId}/skills`
- THEN la API MUST responder `2xx` con el contrato enriquecido vigente.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita el GET o una mutación de `/api/v1/cargos/{cargoId}/skills`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de alta, cambio o baja del subrecurso
- WHEN la ejecuta un usuario autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx`.
