# Delta for cargo-management

## ADDED Requirements

### Requirement: Autorización de endpoints de cargos

`CargosController` MUST requerir autenticación. `GET /api/v1/cargos` y `GET /api/v1/cargos/{id}` MUST permitir cualquier usuario autenticado. `POST`, `PUT`, `PATCH` y `DELETE` MUST requerir rol `Administrador` y, con payload válido y rol correcto, MUST conservar sus contratos `2xx` vigentes.

#### Scenario: Lectura autenticada exitosa
- GIVEN un usuario autenticado
- WHEN solicita `GET /api/v1/cargos` o `GET /api/v1/cargos/{id}`
- THEN la API MUST responder `2xx` con el contrato de lectura vigente.

#### Scenario: Acceso anónimo rechazado
- GIVEN un cliente sin credenciales
- WHEN solicita un GET o una mutación de `CargosController`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador
- GIVEN una solicitud válida de mutación sobre cargos
- WHEN la ejecuta un usuario autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx`.
