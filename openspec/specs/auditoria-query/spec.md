# Specification: auditoria-query

## Purpose

Consulta de solo lectura de los registros de auditoría persistidos por el sistema (interceptor y servicio de escritura existentes). Expone metadatos de cada operación y las propiedades modificadas, sin valores anteriores ni posteriores. Accesible únicamente por el rol `Administrador`, tanto por API como por la shell web. La escritura de auditoría queda fuera de alcance.

## Requirements

### Requirement: Autorización restringida al rol Administrador

Todo acceso de consulta de auditoría (API y Web) SHALL exigir un usuario autenticado con rol `Administrador`. Peticiones sin autenticación MUST responder `401 Unauthorized`; peticiones autenticadas sin el rol MUST responder `403 Forbidden`. La shell web NO MUST呈现 enlaces hacia auditoría a usuarios sin el rol.

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

`GET /api/v1/auditorias` SHALL devolver un `PagedResult<AuditoriaDto>` de registros ordenados por `OccurredAt` descendente, con desempate determinista por `Id` descendente. `Page` y `PageSize` viajan en la query; valores omitidos TOMAN los defaults del sistema; `PageSize` MUST acotarse a un máximo definido.

#### Scenario: Defaults aplicados cuando se omiten parámetros

- GIVEN un administrador que omite `page` y `pageSize`
- WHEN envía `GET /api/v1/auditorias`
- THEN recibe `200` con la primera página usando los defaults del contrato
- AND `TotalCount` refleja el total existente

#### Scenario: Orden determinista en empates de fecha

- GIVEN dos registros con igual `OccurredAt` y distintos `Id`
- WHEN se solicita el listado
- THEN el de `Id` mayor aparece primero

#### Scenario: PageSize excede el máximo permitido

- GIVEN un administrador que envía `pageSize` mayor al máximo
- WHEN recibe la respuesta
- THEN la cantidad de ítems es el máximo acotado, NO el valor solicitado

### Requirement: Filtros combinables de consulta

El listado SHOULD soportar filtros opcionales combinables: `EntityName`, `Operation`, `DateFrom`, `DateTo` y `UserId`. Los filtros vacíos MUST ignorarse y NO filtrar. `DateFrom` es inclusivo; `DateTo` es inclusivo en fecha. Si `DateFrom` es posterior a `DateTo`, la petición MUST responder `400 Validation` con un contrato observable coherente (mensaje explícito de rango invertido); NO se devuelve un conjunto vacío.

#### Scenario: Filtros combinados filtran el resultado

- GIVEN registros de varias entidades y operaciones
- WHEN se envía `?EntityName=Persona&Operation=Modificacion&DateFrom=2026-01-01&DateTo=2026-01-31`
- THEN los ítems cumplen todos los filtros simultáneamente

#### Scenario: Filtros omitidos no filtran

- GIVEN registros diversos
- WHEN se omite todo filtro
- THEN el resultado incluye todos los registros paginados

#### Scenario: Rango de fechas invertido

- GIVEN `DateFrom` posterior a `DateTo`
- WHEN se solicita el listado
- THEN recibe `400 Validation` con un error explícito de rango invertido, NO un conjunto vacío

### Requirement: Detalle por identificador

`GET /api/v1/auditorias/{id}` SHALL devolver `200 OK` con un `AuditoriaDto` cuando el registro existe, y `404 Not Found` cuando no existe o el `id` no es un GUID válido.

#### Scenario: Detalle existente

- GIVEN un registro persistido con `Id` conocido
- WHEN el administrador solicita `GET /api/v1/auditorias/{id}`
- THEN recibe `200` con el DTO completo

#### Scenario: Detalle inexistente

- GIVEN un `Id` sin registro
- WHEN el administrador solicita el detalle
- THEN recibe `404 Not Found`

### Requirement: Contrato wire sin valores anteriores/posteriores

`AuditoriaDto` MUST exponer únicamente `Id`, `EntityName`, `EntityId`, `Operation`, `OccurredAt`, `UserId`, `ChangedPropertiesJson` y `CorrelationId`. Los campos `OldValuesJson` y `NewValuesJson` MUST NOT estar presentes en el wire contract, ni siquiera como nulos.

#### Scenario: DTO no expone old/new values

- GIVEN cualquier registro persistido
- WHEN el administrador obtiene el detalle o listado
- THEN la respuesta serializada NO contiene campos `old` ni `new`

### Requirement: Protección de datos sensibles

El módulo SHOULD evitar exponer PII más allá de los metadatos. Las consultas no se auditarán (no hay auditoría de la auditoría). Retención, purga y exportación quedan fuera de v1.

#### Scenario: Sin exposición de PII en listado

- GIVEN un registro cuyos old/new values contienen PII
- WHEN se lista o se consultan detalles
- THEN la PII sólo podría inferirse de `ChangedPropertiesJson` (nombres de propiedades), nunca de valores

### Requirement: Shell web admin-only con estados vacío y error de transporte

`Pages/Auditorias/Index` SHALL renderizar tabla paginada server-side con sidebar de filtros, exponer paginación PRG-compatible y reaccionar a conjuntos vacíos y fallos de transporte recuperables del `AuditoriaApiClient`.

#### Scenario: Listado vacío

- GIVEN filtros que no producen resultados
- WHEN el administrador accede a la página
- THEN se muestra un estado vacío legible, NO una tabla vacía sin mensaje

#### Scenario: Error de transporte recuperable

- GIVEN una falla temporal de conexión API→Web
- WHEN el `AuditoriaApiClient` no puede completar la query
- THEN la página muestra un mensaje de error de transporte recuperable sin perder los filtros ingresados

#### Scenario: Paginación web conserva filtros

- GIVEN filtros activos en la página actual
- WHEN el administrador navega a la siguiente página
- THEN la navegación conserva los filtros aplicados

## Notas de implementación (no normativas)

- Reutiliza la tabla `Auditorias` existente; no existen ni se requieren migraciones.
- El ordenamiento por `OccurredAt DESC, Id DESC` aprovecha el índice existente `EntityName+EntityId+OccurredAt` con sort posterior por Id; el desempate por Id garantiza determinismo testeable.