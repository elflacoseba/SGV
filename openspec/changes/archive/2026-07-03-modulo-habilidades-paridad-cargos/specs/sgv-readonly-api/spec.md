# Delta for sgv-readonly-api

## Propósito

Actualizar la discoverability HTTP para que consumidores y Swagger descubran la nueva consulta segmentada de habilidades y el catálogo público de niveles de habilidad sin deprecar `GET /api/v1/skills`.

## Modificaciones

- Se documenta `GET /api/v1/skills/consulta` con su filtro `status` y contrato paginado.
- Se documenta `GET /api/v1/niveles-habilidad` como catálogo público de lectura.

## MODIFIED Requirements

### Requirement: Public API Discoverability

The system MUST publish API documentation that allows consumers to discover read endpoints, write endpoints for organizational units, roles (cargos), positions, skills, Personas, `GET /api/v1/skills/consulta`, and `GET /api/v1/niveles-habilidad`, along with their response contracts. Documentation for skills MUST preserve `GET /api/v1/skills` as the legacy active-only read route and MUST describe `skills/consulta` as the segmented read contract.

(Previously: documentation allowed consumers to discover read endpoints and write endpoints for organizational units, roles, positions, skills, and Personas, but did not document `skills/consulta` or `niveles-habilidad`.)

#### Scenario: Discover endpoints through API documentation

- GIVEN the API is running locally
- WHEN a client opens the API documentation
- THEN the documentation MUST list read endpoints for organizational units, organizational unit types, roles, positions, skills, Personas, and `niveles-habilidad`
- AND it MUST describe the successful response contract for each endpoint.

#### Scenario: Discover organizational unit write operations

- GIVEN organizational unit CRUD is supported
- WHEN a client inspects the API documentation
- THEN documented organizational unit create, update, parent-change, and soft-delete operations MUST be discoverable.

#### Scenario: Discover cargo management operations

- GIVEN cargo management is supported
- WHEN a client inspects the API documentation
- THEN documented cargo create, update, deactivate, and reactivate operations MUST be discoverable.

#### Scenario: Discover puesto management operations

- GIVEN position management is supported
- WHEN a client inspects the API documentation
- THEN documented position create, update, deactivate, and reactivate operations MUST be discoverable.

#### Scenario: Discover skill management operations

- GIVEN skill management is supported
- WHEN a client inspects the API documentation
- THEN documented skill create, update, deactivate, and reactivate operations under `/api/v1/skills` MUST be discoverable
- AND `GET /api/v1/skills/consulta` MUST appear as the segmented query route.

#### Scenario: Discover segmented skill query parameters

- GIVEN a client inspects `GET /api/v1/skills/consulta`
- WHEN the query parameters are reviewed
- THEN the documentation MUST describe `status=activas|eliminadas`, `search`, `sort`, `page` and `pageSize`
- AND MUST indicate that `activas` is the default segment.

#### Scenario: Discover skill-level catalog

- GIVEN a client inspects the API documentation
- WHEN the documented read resources are reviewed
- THEN `GET /api/v1/niveles-habilidad` MUST be discoverable
- AND its success response MUST describe the consumer-safe level catalog.

#### Scenario: Discover persona management operations

- GIVEN persona management is supported
- WHEN a client inspects the API documentation
- THEN documented Persona operations under `api/v1/personas` MUST be discoverable.

#### Scenario: Exclude unsupported operations from documentation

- GIVEN organizational unit types remain read-only
- WHEN a client inspects the API documentation
- THEN create, update, and delete operations for those resources MUST NOT be documented as available actions.

## Out of scope

- No cambia políticas de autenticación de endpoints públicos distintos de `skills`.
- No documenta endpoints de asignación `habilidad↔cargo` ni `habilidad↔persona` dentro de este cambio.
