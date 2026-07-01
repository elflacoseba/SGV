# Delta for cargo-skill-query-contract

## ADDED Requirements

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
