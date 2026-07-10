# Delta for persona-management

## ADDED Requirements

### Requirement: Autorización de endpoints de personas

`PersonasController` MUST requerir autenticación. `GET /api/v1/personas` y `GET /api/v1/personas/{id}` MUST permitir el acceso a cualquier usuario autenticado y MUST responder `2xx` con el contrato de lectura vigente. `POST`, `PUT`, `PATCH` y `DELETE` (incluyendo `Reactivar`, `AsignarSkill` y `QuitarSkill`) MUST requerir el rol `Administrador`; con payload válido y rol correcto, MUST conservar sus contratos `2xx` vigentes.

#### Scenario: Lectura autenticada exitosa

- GIVEN un usuario autenticado
- WHEN solicita `GET /api/v1/personas` o `GET /api/v1/personas/{id}`
- THEN la API MUST responder `2xx` con el contrato de lectura vigente.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita un `GET` o una mutación de `PersonasController` (incluyendo `Reactivar`, `AsignarSkill` y `QuitarSkill`)
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de mutación sobre personas
- WHEN la ejecuta un usuario autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx` con el contrato vigente.