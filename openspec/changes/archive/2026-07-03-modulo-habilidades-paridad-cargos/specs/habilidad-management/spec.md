# Delta for habilidad-management

## Propósito

Extender el catálogo maestro de Habilidades para soportar consulta segmentada autenticada, exponer el catálogo público de `NivelHabilidad` y alinear autorización con el patrón vigente de `Cargos`.

## Modificaciones

- Se mantiene `GET /api/v1/skills` como lectura legacy de activas, ahora autenticada.
- Se agrega `GET /api/v1/skills/consulta` con paginación, búsqueda, orden y segmentos `activas|eliminadas`.
- Se agrega `GET /api/v1/niveles-habilidad` como catálogo de lectura para consumo web.

## MODIFIED Requirements

### Requirement: Consultar Habilidades

El sistema MUST mantener `GET /api/v1/skills` y `GET /api/v1/skills/{id:guid}` como contrato legacy de lectura de habilidades activas para usuarios autenticados y MUST agregar `GET /api/v1/skills/consulta` como consulta paginada y filtrada. `status` MUST aceptar `activas|eliminadas`; si se omite o es inválido MUST caer a `activas`. `page < 1` MUST normalizarse a `1`; `pageSize < 1` MUST caer a `20`; `pageSize > 100` MUST limitarse a `100`; `sort` desconocido MUST caer a `codigo_asc`.

(Previously: `GET /api/v1/skills` y `GET /api/v1/skills/{id:guid}` eran el contrato canónico de lectura y devolvían habilidades activas por defecto, sin `/consulta` segmentada.)

#### Scenario: Listar habilidades activas legacy

- GIVEN que existen habilidades activas e inactivas
- WHEN un usuario autenticado solicita `GET /api/v1/skills`
- THEN el sistema MUST devolver solo habilidades activas.

#### Scenario: Obtener habilidad inexistente o inactiva

- GIVEN que una Habilidad no existe o está inactiva
- WHEN un usuario autenticado la solicita por identificador
- THEN el sistema MUST responder como recurso no encontrado para consultas activas.

#### Scenario: Consulta de eliminadas no mezcla segmentos

- GIVEN habilidades activas e inactivas en persistencia
- WHEN un usuario autenticado consulta `GET /api/v1/skills/consulta?status=eliminadas&search=com&page=2&pageSize=10&sort=nombre_desc`
- THEN la respuesta MUST exponer `Items` con habilidades inactivas únicamente que coincidan con `search=com`
- AND MUST aplicar normalización con `Page=1`, `PageSize=100`
- AND MUST exponer `TotalCount` consistente con el segmento `eliminadas` y `sort=nombre_desc`.

#### Scenario: Paginación o status inválidos se normalizan

- GIVEN habilidades activas e inactivas en persistencia
- WHEN un usuario autenticado consulta `/api/v1/skills/consulta?status=archivo&page=0&pageSize=500`
- THEN la respuesta MUST caer a `activas`
- AND MUST usar `page=1` y `pageSize=100`.

#### Scenario: Búsqueda sin coincidencias devuelve página vacía

- GIVEN un usuario autenticado consulta un segmento válido
- WHEN `search` no coincide con ninguna habilidad del segmento
- THEN el sistema MUST responder `200 OK` con `items` vacíos
- AND MUST conservar `totalCount=0` y metadatos de la página solicitada normalizada.

## ADDED Requirements

### Requirement: Publicar catálogo HTTP de niveles de habilidad

El sistema MUST exponer `GET /api/v1/niveles-habilidad` como catálogo consumer-safe para `NivelHabilidad`, ordenado ascendentemente por `Orden`, sin mezclar reglas de asignación a cargos o personas.

#### Scenario: Catálogo de niveles disponible para web

- GIVEN que existen niveles de habilidad sembrados en persistencia
- WHEN un cliente solicita `GET /api/v1/niveles-habilidad`
- THEN el sistema MUST responder `200 OK` con elementos ordenados por `orden`
- AND cada elemento MUST exponer solo campos consumer-safe del nivel.

#### Scenario: Catálogo vacío sigue siendo válido

- GIVEN que no existen niveles de habilidad publicados
- WHEN un cliente solicita `GET /api/v1/niveles-habilidad`
- THEN el sistema MUST responder `200 OK` con una colección vacía.

### Requirement: Autorización de endpoints de habilidades

`SkillsController` MUST requerir autenticación a nivel de controller. `GET /api/v1/skills`, `GET /api/v1/skills/{id}` y `GET /api/v1/skills/consulta` MUST permitir cualquier usuario autenticado. `POST`, `PUT`, `DELETE` y `PATCH /reactivar` MUST requerir rol `Administrador`.

#### Scenario: Lecturas autenticadas exitosas

- GIVEN un usuario autenticado
- WHEN solicita una lectura de `SkillsController`
- THEN la API MUST responder `2xx` con el contrato documentado.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita una lectura o mutación de `SkillsController`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de create, update, delete o reactivate
- WHEN la ejecuta un usuario autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND si la ejecuta un `Administrador`, MUST conservar su contrato `2xx` vigente.

## Out of scope

- No agrega asignaciones `habilidad↔cargo` ni `habilidad↔persona`.
- No agrega nuevos parámetros de `/skills/consulta` fuera de `status`, `search`, `sort`, `page` y `pageSize`.
- No agrega `nivelId` a `POST` o `PUT /api/v1/skills` en este cambio.
