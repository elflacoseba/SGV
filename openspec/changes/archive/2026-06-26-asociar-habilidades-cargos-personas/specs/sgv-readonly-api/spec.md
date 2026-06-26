# Delta para SGV Read-only API

## MODIFIED Requirements

### Requirement: Read-only Resource Access

El sistema MUST exponer acceso HTTP a organizational units, organizational unit types, roles, positions, skills y Personas. Además, MUST documentar los subrecursos `/api/v1/cargos/{cargoId}/skills` y `/api/v1/personas/{personaId}/skills` como contratos administrables. `/api/v1/skills` SHALL seguir siendo el catálogo canónico de habilidades.
(Anteriormente: la API no documentaba la gestión de habilidades por subrecurso de Cargo y Persona.)

#### Scenario: Listar recursos soportados

- **GIVEN** datos persistidos existentes
- **WHEN** un cliente consulta el contrato de la API
- **THEN** la documentación MUST incluir los subrecursos de habilidades por Cargo y Persona
- **AND** `/api/v1/skills` MUST quedar como catálogo.
