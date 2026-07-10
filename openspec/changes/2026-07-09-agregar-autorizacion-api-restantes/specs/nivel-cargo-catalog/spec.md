# Delta for nivel-cargo-catalog

## ADDED Requirements

### Requirement: Autorización de lectura de NivelesCargo

`NivelesCargoController` MUST requerir autenticación para sus endpoints de lectura. `GET /api/v1/niveles-cargo` y `GET /api/v1/niveles-cargo/{id:guid}` MUST responder `2xx` únicamente para usuarios autenticados y MUST conservar el contrato de respuesta vigente (`id`, `codigo`, `nombre`, `valorNumerico`, `orden`). Los endpoints de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre `NivelesCargo` MUST NO estar expuestos; cualquier intento de escritura MUST responder `405 Method Not Allowed` o no estar disponible como acción documentada, independientemente del estado de autenticación del cliente.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita `GET /api/v1/niveles-cargo` o `GET /api/v1/niveles-cargo/{id:guid}`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Lectura autenticada exitosa

- GIVEN un usuario autenticado
- WHEN solicita `GET /api/v1/niveles-cargo` o `GET /api/v1/niveles-cargo/{id:guid}`
- THEN la API MUST responder `2xx` con el contrato de lectura vigente del catálogo.