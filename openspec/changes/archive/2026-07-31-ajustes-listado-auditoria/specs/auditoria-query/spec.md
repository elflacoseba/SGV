# Delta for auditoria-query

> Cambio `2026-07-31-ajustes-listado-auditoria` (issue #248). Modifica la capability vigente `openspec/specs/auditoria-query/spec.md`. Las nuevas capabilities `auditoria-sort`, `auditoria-detalle` y `auditoria-page-size` viven en sus propias specs dentro de este change.

## MODIFIED Requirements

### Requirement: Listado paginado con orden determinista reciente-primero

`GET /api/v1/auditorias` SHALL devolver un `PagedResult<AuditoriaDto>`. El orden SHALL controlarse con el query param opcional `Sort` cuya semántica completa (valores válidos, default `fecha_desc`, tiebreak determinista) se define en la capability `auditoria-sort`. `Page` y `PageSize` viajan en la query; valores omitidos TOMAN los defaults del sistema. `PageSize` MUST acotarse al rango `1–100`: valores menores a 1 MUST normalizarse a 1, valores mayores a 100 MUST normalizarse a 100. El selector UI de `PageSize` (10/20/50/100) se define en `auditoria-page-size`.
(Previously: orden fijo `OccurredAt DESC, Id DESC` sin query param; `PageSize` sólo tenía máximo acotado, sin piso explícito de 1.)

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
(Previously: filtros combinables sin `CorrelationId`.)

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
(Previously: el detalle devolvía `AuditoriaDto`, que exponía `EntityId` y omitía old/new values.)

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
(Previously: `AuditoriaDto` exponía `EntityId` y no incluía `UserName`; old/new values ya estaban prohibidos.)

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

## ADDED Files

- `src/SGV.Contracts/Auditoria/AuditoriaDetalleDto.cs` — DTO enriquecido de detalle (definido por `auditoria-detalle`).

## MODIFIED Files

- `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` — agregar `Sort?`, `CorrelationId?`; clamping `PageSize` 1–100.
- `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` — quitar `EntityId`; agregar `UserName?`.
- `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` — nuevo `GetDetalleDtoAsync`.
- `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` — sort dinámico, LEFT JOIN `AspNetUsers`, `GetDetalleDtoAsync`.
- `src/SGV.Api/Controllers/AuditoriasController.cs` — propagar `sort`/`correlationId`; `GetById` retorna `AuditoriaDetalleDto`.