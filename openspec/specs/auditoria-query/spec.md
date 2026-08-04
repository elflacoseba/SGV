# Specification: auditoria-query

## Purpose

Consulta de solo lectura de los registros de auditoría persistidos por el sistema (interceptor y servicio de escritura existentes). Expone metadatos de cada operación y las propiedades modificadas, sin valores anteriores ni posteriores. Accesible únicamente por el rol `Administrador`, tanto por API como por la shell web. La escritura de auditoría queda fuera de alcance.

## Requirements

### Requirement: Autorización restringida al rol Administrador

Todo acceso de consulta de auditoría (API y Web) SHALL exigir un usuario autenticado con rol `Administrador`. Peticiones sin autenticación MUST responder `401 Unauthorized`; peticiones autenticadas sin el rol MUST responder `403 Forbidden`. La shell web NO MUST presentar enlaces hacia auditoría a usuarios sin el rol.

#### Scenario: Acceso anónimo a la API

- GIVEN un cliente sin credenciales válidas
- WHEN envía `GET /api/v1/auditorias`
- THEN recibe `401 Unauthorized`

#### Scenario: Usuario autenticado sin rol Administrador

- GIVEN un usuario autenticado con rol distinto a `Administrador`
- WHEN envía `GET /api/v1/auditorias`
- THEN recibe `403 Forbidden`
- AND el cuerpo NO contiene datos de auditoría

#### Scenario: Administrador accede a la API

- GIVEN un usuario autenticado con rol `Administrador`
- WHEN envía `GET /api/v1/auditorias`
- THEN recibe `200 OK` con un `PagedResult<AuditoriaDto>`

#### Scenario: Shell web oculta acceso a no administradores

- GIVEN un usuario autenticado sin rol `Administrador` navegando la shell
- WHEN renderiza el menú principal
- THEN la entrada «Auditorías» NO se ofrece

### Requirement: Listado paginado con orden determinista reciente-primero

`GET /api/v1/auditorias` SHALL devolver un `PagedResult<AuditoriaDto>`. El orden SHALL controlarse con el query param opcional `Sort` cuya semántica completa (valores válidos, default `fecha_desc`, tiebreak determinista) se define en la capability `auditoria-sort`. `Page` y `PageSize` viajan en la query; valores omitidos TOMAN los defaults del sistema. `PageSize` MUST acotarse al rango `1–100`: valores menores a 1 MUST normalizarse a 1, valores mayores a 100 MUST normalizarse a 100. El selector UI de `PageSize` (10/20/50/100) se define en `auditoria-page-size`.

#### Scenario: Defaults aplicados cuando se omiten parámetros

- GIVEN un administrador que omite `page`, `pageSize` y `sort`
- WHEN envía `GET /api/v1/auditorias`
- THEN recibe `200` con la primera página usando los defaults del contrato
- AND el orden aplicado es `fecha_desc` (equivalente al `OccurredAt DESC` previo)

#### Scenario: Orden determinista en empates de fecha

- GIVEN dos registros con igual `OccurredAt` y distintos `Id`
- WHEN se solicita el listado con cualquier `Sort` válido
- THEN el desempate por `Id` descendente garantiza un orden estable y testeable

#### Scenario: PageSize por debajo del mínimo se normaliza a 1

- GIVEN un administrador que envía `pageSize=0` o `pageSize=-5`
- WHEN recibe la respuesta
- THEN la cantidad de ítems refleja un `pageSize` efectivo de `1`, NO un error

#### Scenario: PageSize excede el máximo permitido

- GIVEN un administrador que envía `pageSize=500`
- WHEN recibe la respuesta
- THEN la cantidad de ítems es el máximo acotado (`100`), NO el valor solicitado

### Requirement: Filtros combinables de consulta

El listado SHOULD soportar filtros opcionales combinables: `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId` y `CorrelationId`. Los filtros vacíos MUST ignorarse y NO filtrar. `DateFrom` es inclusivo; `DateTo` es inclusivo en fecha. `CorrelationId` SHOULD aceptar un `Guid` y filtrar exactamente los registros que compartan esa correlación. Si `DateFrom` es posterior a `DateTo`, la petición MUST responder `400 Validation` con un contrato observable coherente (mensaje explícito de rango invertido); NO se devuelve un conjunto vacío.

#### Scenario: Filtros combinados filtran el resultado

- GIVEN registros de varias entidades y operaciones
- WHEN se envía `?EntityName=Persona&Operation=Modificacion&DateFrom=2026-01-01&DateTo=2026-01-31`
- THEN los ítems cumplen todos los filtros simultáneamente

#### Scenario: Filtro por CorrelationId aísla la correlación

- GIVEN varios registros con distintos `CorrelationId`
- WHEN se envía `?CorrelationId={guid}`
- THEN el resultado contiene sólo los registros cuyo `CorrelationId` coincide exactamente

#### Scenario: Filtros omitidos no filtran

- GIVEN registros diversos
- WHEN se omite todo filtro
- THEN el resultado incluye todos los registros paginados

#### Scenario: Rango de fechas invertido

- GIVEN `DateFrom` posterior a `DateTo`
- WHEN se solicita el listado
- THEN recibe `400 Validation` con un error explícito de rango invertido, NO un conjunto vacío

### Requirement: Detalle por identificador

`GET /api/v1/auditorias/{id}` SHALL devolver `200 OK` con un `AuditoriaDetalleDto` cuando el registro existe, y `404 Not Found` cuando no existe o el `id` no es un GUID válido. La forma del DTO enriquecido (con `EntityId`, `OldValuesJson`, `NewValuesJson`, `ChangedPropertiesJson` y `UserName`) se define en la capability `auditoria-detalle`. El endpoint MUST exigir rol `Administrador`, heredando la restricción de "Autorización restringida al rol Administrador" de esta spec.

#### Scenario: Detalle existente devuelve DTO enriquecido

- GIVEN un registro persistido con `Id` conocido y `OldValuesJson`/`NewValuesJson` poblados
- WHEN el administrador solicita `GET /api/v1/auditorias/{id}`
- THEN recibe `200` con `AuditoriaDetalleDto` incluyendo `EntityId`, old/new values y `UserName`

#### Scenario: Detalle inexistente

- GIVEN un `Id` sin registro
- WHEN el administrador solicita el detalle
- THEN recibe `404 Not Found`

#### Scenario: Detalle con id no GUID

- GIVEN un `id` con formato no parseable como `Guid`
- WHEN el administrador solicita el detalle
- THEN recibe `404 Not Found`

### Requirement: Contrato wire del listado sin valores anteriores/posteriores ni EntityId

`AuditoriaDto` (DTO de listado) MUST exponer únicamente `Id`, `EntityName`, `Operation`, `OccurredAt`, `UserId`, `UserName?`, `ChangedPropertiesJson` y `CorrelationId`. `EntityId` MUST NOT estar presente en el wire contract de listado. Los campos `OldValuesJson` y `NewValuesJson` MUST NOT estar presentes en `AuditoriaDto`, ni siquiera como nulos. `UserName?` (resultado de LEFT JOIN con `AspNetUsers`) MUST caer a `"—"` cuando el usuario no exista. Los valores anteriores/posteriores y `EntityId` sólo viven en `AuditoriaDetalleDto` (definido en `auditoria-detalle`); la separación física de tipos cierra D-2 por construcción.

#### Scenario: DTO de listado no expone old/new values ni EntityId

- GIVEN cualquier registro persistido
- WHEN el administrador obtiene el listado
- THEN la respuesta serializada NO contiene `entityId`, `oldValuesJson` ni `newValuesJson`

#### Scenario: UserName cae a guión cuando no hay usuario

- GIVEN un registro cuyo `UserId` no existe en `AspNetUsers`
- WHEN se lista o el LEFT JOIN no encuentra fila
- THEN `UserName` se proyecta como `"—"`

#### Scenario: UserName resuelto desde AspNetUsers

- GIVEN un registro cuyo `UserId` existe en `AspNetUsers`
- WHEN se lista
- THEN `UserName` se proyecta con el `UserName` de Identity

#### Scenario: Reflexión impide agregar old/new a AuditoriaDto

- GIVEN un test inspecciona los campos de `AuditoriaDto`
- THEN NO existe propiedad llamada `OldValuesJson` o `NewValuesJson`
- AND la separación de tipos hace que sea imposible exponerlos por accidente desde el listado

### Requirement: Protección de datos sensibles

El módulo SHOULD evitar exponer PII más allá de los metadatos. Las consultas no se auditarán (no hay auditoría de la auditoría). Retención, purga y exportación quedan fuera de v1.

#### Scenario: Sin exposición de PII en listado

- GIVEN un registro cuyos old/new values contienen PII
- WHEN se lista o se consultan detalles
- THEN la PII sólo podría inferirse de `ChangedPropertiesJson` (nombres de propiedades), nunca de valores

### Requirement: Shell web admin-only con estados vacío y error de transporte

`Pages/Auditorias/Index` SHALL renderizar tabla paginada server-side con filtros horizontales (toolbar), `<th>` ordenables con indicadores de dirección, selector de `PageSize` (10/20/50/100), paginación con números de página, exponer paginación PRG-compatible y reaccionar a fallos de transporte recuperables del `AuditoriaApiClient`.

#### Scenario: Listado vacío

- GIVEN filtros que no producen resultados
- WHEN el administrador accede a la página
- THEN se muestra un estado vacío legible, NO una tabla vacía sin mensaje

#### Scenario: Error de transporte recuperable

- GIVEN una falla temporal de conexión API→Web
- WHEN el `AuditoriaApiClient` no puede completar la query
- THEN la página muestra un mensaje de error de transporte recuperable sin perder los filtros ingresados

#### Scenario: Paginación web conserva filtros y sort

- GIVEN filtros activos y un sort elegido en la página actual
- WHEN el administrador navega a la siguiente página
- THEN la navegación conserva los filtros, sort y pageSize aplicados

## Notas de implementación (no normativas)

- Reutiliza la tabla `Auditorias` existente; no se requieren migraciones de columna.
- El ordenamiento por `OccurredAt DESC, Id DESC` (y sus variantes configurables vía `auditoria-sort`) se resuelve con un índice covering `(CorrelationId, OccurredAt)` cuando se filtra por correlación.
- El LEFT JOIN con `AspNetUsers` proyecta `UserName` con fallback `"—"` cuando el usuario no existe en Identity.
- El `AuditoriaDetalleDto` (capability `auditoria-detalle`) es la única superficie del wire que porta `EntityId`, `OldValuesJson` y `NewValuesJson`; la separación física de tipos cierra D-2 por construcción.
