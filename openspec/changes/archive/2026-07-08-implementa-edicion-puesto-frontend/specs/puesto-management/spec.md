# Delta for puesto-management

## ADDED Requirements

### Requirement: Autorización de endpoints de puestos

`PuestosController` MUST requerir autenticación para todos sus métodos. `GET /api/v1/puestos` y `GET /api/v1/puestos/{id}` MUST permitir el acceso a cualquier usuario autenticado y MUST responder `2xx` con el contrato de lectura vigente. `POST /api/v1/puestos`, `PUT /api/v1/puestos/{id}`, `DELETE /api/v1/puestos/{id}` y `PATCH /api/v1/puestos/{id}/reactivar` MUST requerir el rol `Administrador`; con payload válido y rol correcto, MUST conservar sus contratos `2xx` vigentes.

#### Scenario: Lectura autenticada exitosa

- GIVEN un usuario autenticado
- WHEN solicita `GET /api/v1/puestos` o `GET /api/v1/puestos/{id}`
- THEN la API MUST responder `2xx` con el contrato de lectura vigente.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita un GET o una mutación de `PuestosController`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de mutación sobre puestos
- WHEN la ejecuta un usuario autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx` con el contrato vigente.