# Delta for tipo-unidad-organizativa-catalog

## ADDED Requirements

### Requirement: Autorización de lectura de TiposUnidadOrganizativa

`TipoUnidadesOrganizativasController` MUST requerir autenticación para sus endpoints de lectura. `GET /api/v1/tipos-unidad-organizativa` y `GET /api/v1/tipos-unidad-organizativa/{id:guid}` MUST responder `2xx` únicamente para usuarios autenticados y MUST conservar el contrato de respuesta vigente (`id`, `codigo`, `nombre`). Los endpoints de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre el catálogo MUST NO estar expuestos; cualquier intento de escritura MUST responder `405 Method Not Allowed` o no estar disponible como acción documentada, independientemente del estado de autenticación del cliente.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita `GET /api/v1/tipos-unidad-organizativa` o `GET /api/v1/tipos-unidad-organizativa/{id:guid}`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Lectura autenticada exitosa

- GIVEN un usuario autenticado
- WHEN solicita `GET /api/v1/tipos-unidad-organizativa` o `GET /api/v1/tipos-unidad-organizativa/{id:guid}`
- THEN la API MUST responder `2xx` con el contrato de lectura vigente del catálogo.