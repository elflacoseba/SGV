# Delta for unidad-organizativa-crud

## ADDED Requirements

### Requirement: Autorización de endpoints de unidades organizativas

`UnidadesOrganizativasController` MUST requerir autenticación. `GET /api/v1/unidades-organizativas`, `GET /api/v1/unidades-organizativas/{id}` y `GET /api/v1/unidades-organizativas/consulta` MUST permitir el acceso a cualquier usuario autenticado y MUST responder `2xx` con el contrato de lectura vigente. `POST`, `PUT`, `PATCH` (incluyendo `ActualizarPadre` y `Reactivar`) y `DELETE` MUST requerir el rol `Administrador`; con payload válido y rol correcto, MUST conservar sus contratos `2xx` vigentes.

#### Scenario: Lectura autenticada exitosa

- GIVEN un usuario autenticado
- WHEN solicita `GET /api/v1/unidades-organizativas`, `GET /api/v1/unidades-organizativas/{id}` o `GET /api/v1/unidades-organizativas/consulta`
- THEN la API MUST responder `2xx` con el contrato de lectura vigente.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita un `GET` o una mutación de `UnidadesOrganizativasController` (incluyendo `ActualizarPadre` y `Reactivar`)
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de mutación sobre unidades organizativas
- WHEN la ejecuta un usuario autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx` con el contrato vigente.