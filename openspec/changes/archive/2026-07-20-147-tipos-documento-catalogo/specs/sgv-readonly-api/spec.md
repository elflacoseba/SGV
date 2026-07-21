# Capability: SGV Read-only API (delta)

> **Status:** MODIFIED — capability exists at `openspec/specs/sgv-readonly-api/spec.md`. This delta adds the read-only catalog endpoints for `TiposDocumento` and its documentation requirement. The new endpoints follow the same default-deny authentication posture as `tipo-unidad-organizativa-catalog` and `nivel-cargo-catalog`.
> **Change:** `2026-07-20-147-tipos-documento-catalogo` (issue #147)

## ADDED Requirements

### Requirement: Catálogo `tipos-documento` listado y detallado

El sistema DEBE exponer `GET /api/v1/tipos-documento` que devuelve los 4 tipos seedeados y `GET /api/v1/tipos-documento/{id:guid}` que devuelve un tipo puntual. Ambos endpoints DEBEN requerir autenticación (default-deny global). Los endpoints de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre `TiposDocumento` NO DEBEN estar expuestos.

#### Escenario: Listar `TiposDocumento` autenticado

- **DADO** los 4 tipos seedeados en `TiposDocumento` (`DNI`, `LE`, `LC`, `Pasaporte`)
- **CUANDO** un cliente autenticado solicita `GET /api/v1/tipos-documento`
- **ENTONCES** la API DEBE responder `200 OK`
- **Y** el cuerpo es un array JSON de 4 elementos con `id`, `codigo`, `nombre`, `patronValidacion` (cuando aplique), `longitudMinima` y `longitudMaxima`.

#### Escenario: Acceso anónimo a `tipos-documento` es rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita `GET /api/v1/tipos-documento` o `GET /api/v1/tipos-documento/{id:guid}`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

### Requirement: Contrato `TipoDocumentoDto` documentado en Swagger

La documentación HTTP MUST describir `TipoDocumentoDto` con `id: Guid`, `codigo: string`, `nombre: string`, `patronValidacion: string?`, `longitudMinima: int?` y `longitudMaxima: int?`. MUST incluir los endpoints `GET /api/v1/tipos-documento` y `GET /api/v1/tipos-documento/{id:guid}` con respuesta `200 OK` documentada. MUST NO documentar endpoints de escritura.

#### Escenario: Swagger expone el contrato del catálogo

- **DADO** un consumidor abriendo Swagger
- **CUANDO** inspecciona `TipoDocumentosController`
- **ENTONCES** la documentación MUST listar `GET /api/v1/tipos-documento` con respuesta `200 OK` y el esquema `TipoDocumentoDto`
- **Y** MUST listar `GET /api/v1/tipos-documento/{id:guid}` con respuesta `200 OK` y `404 Not Found`
- **Y** NO DEBE listar operaciones `POST`, `PUT`, `PATCH` o `DELETE` sobre el recurso.

#### Escenario: Forma del DTO coincide con el seed

- **DADO** el `TipoDocumento` seedeado con `Codigo="Pasaporte"`, `PatronValidacion="^[A-Za-z]{3}\d{6}$"`, `LongitudMinima=null`, `LongitudMaxima=null`
- **CUANDO** un cliente autenticado solicita `GET /api/v1/tipos-documento`
- **ENTONCES** el elemento correspondiente contiene `codigo="Pasaporte"`, `nombre="Pasaporte"`, `patronValidacion="^[A-Za-z]{3}\\d{6}$"`, `longitudMinima=null` y `longitudMaxima=null`.