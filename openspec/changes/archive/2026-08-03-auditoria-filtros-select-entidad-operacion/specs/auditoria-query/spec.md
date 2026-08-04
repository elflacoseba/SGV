# Delta for auditoria-query

> Cambio `2026-08-03-auditoria-filtros-select-entidad-operacion` (issue #251). Modifica la capability vigente `openspec/specs/auditoria-query/spec.md`. El nuevo endpoint `filter-options` se incorpora a esta misma capability; no se crea ninguna capability nueva.

## MODIFIED Requirements

### Requirement: Filtros combinables de consulta

El listado SHOULD soportar filtros opcionales combinables: `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserName` y `CorrelationId`. `UserName` filtra contra `u.UserName` de `AspNetUsers` (LEFT JOIN ya existente), NO contra el GUID técnico `a.UserId`; la comparación es case-insensitive gracias al collation MySQL `utf8mb4_0900_ai_ci` por defecto. Los filtros vacíos MUST ignorarse y NO filtrar. `DateFrom` es inclusivo; `DateTo` es inclusivo en fecha. `CorrelationId` SHOULD aceptar un `Guid` y filtrar exactamente los registros que compartan esa correlación. Si `DateFrom` es posterior a `DateTo`, la petición MUST responder `400 Validation` con un contrato observable coherente (mensaje explícito de rango invertido); NO se devuelve un conjunto vacío.
(Previously: el filtro de usuario se llamaba `UserId` y comparaba contra `a.UserId` (GUID técnico); ahora se llama `UserName` y compara contra `u.UserName` de `AspNetUsers`.)

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

#### Scenario: Filtro por UserName localiza al usuario por nombre, no por GUID

- GIVEN un registro de auditoría cuyo `u.UserName` en `AspNetUsers` es `jperez`
- WHEN se envía `?userName=jperez`
- THEN el registro aparece en el resultado
- AND el parámetro legacy `?userId={guid}` ya NO filtra (se ignora o no produce match)

#### Scenario: UserName inexistente devuelve conjunto vacío

- GIVEN registros existentes y un `UserName` que no existe en `AspNetUsers`
- WHEN se envía `?userName=noexiste`
- THEN el resultado es un `PagedResult` vacío, NO un error

#### Scenario: Filtro UserName case-insensitive

- GIVEN un registro cuyo `u.UserName` es `jperez`
- WHEN se envía `?userName=Jperez` y luego `?userName=jperez`
- THEN ambos requests devuelven el mismo conjunto de registros

### Requirement: Shell web admin-only con estados vacío y error de transporte

`Pages/Auditorias/Index` SHALL renderizar tabla paginada server-side con filtros horizontales (toolbar), `<th>` ordenables con indicadores de dirección, selector de `PageSize` (10/20/50/100), paginación con números de página, exponer paginación PRG-compatible y reaccionar a fallos de transporte recuperables del `AuditoriaApiClient`. Los filtros `entityName` y `operation` SHOULD renderizarse como `<select>` poblados dinámicamente desde `GET /api/v1/auditorias/filter-options` (con opción "Todos" que limpia el filtro); ante fallo del endpoint, la página SHALL hacer fallback a inputs de texto y mostrar un mensaje no bloqueante (info/warning soft, NO un `Error` rojo), de modo que el listado siga operativo.
(Previously: los filtros `Entidad` y `Operación` eran inputs de texto fijos; no existía dependencia del endpoint `filter-options` ni comportamiento de fallback.)

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

#### Scenario: Si el endpoint filter-options falla, IndexModel hace fallback a inputs de texto

- GIVEN el `AuditoriaApiClient.GetFilterOptionsAsync` lanza una excepción de transporte
- WHEN se renderiza `Pages/Auditorias/Index`
- THEN los filtros `entityName` y `operation` se muestran como `<input type="text">` usables
- AND la página muestra un mensaje no bloqueante (info/warning soft), NO un error rojo
- AND el listado paginado sigue funcionando normalmente

## ADDED Requirements

### Requirement: Endpoint filter-options para poblar selects de Entidad y Operación

El módulo SHALL exponer `GET /api/v1/auditorias/filter-options` protegido con `[Authorize(Roles = Administrador)]`, heredando la restricción de "Autorización restringida al rol Administrador". El endpoint SHALL devolver `200 OK` con un objeto `{ entityNames: string[], operations: string[] }` derivado de `SELECT DISTINCT EntityName` y `SELECT DISTINCT Operation` sobre la tabla `Auditorias` con `AsNoTracking()`. Ambos arrays SHOULD estar ordenados alfabéticamente y no contener duplicados. La respuesta MUST NOT incluir `OldValuesJson`, `NewValuesJson`, `EntityId`, `UserId` ni `UserName` (respeta D-2). Valores vacíos o null en la columna MUST NO exponerse (solo strings no vacíos). Una lista vacía (`[]`) es válida cuando no hay datos y NO constituye un error. El endpoint MUST aplicar un cap duro de 100 elementos por array: si el `DISTINCT` devolviera más de 100 valores, se devuelven los primeros 100 ordenados alfabéticamente.

#### Scenario: Endpoint filter-options devuelve listas distintas

- GIVEN registros persistidos con varios `EntityName` y `Operation`
- WHEN un administrador envía `GET /api/v1/auditorias/filter-options`
- THEN recibe `200 OK` con `entityNames` y `operations` rellenas y ordenadas

#### Scenario: Endpoint filter-options sin credenciales responde 401

- GIVEN un cliente sin credenciales válidas
- WHEN envía `GET /api/v1/auditorias/filter-options`
- THEN recibe `401 Unauthorized`

#### Scenario: Endpoint filter-options autenticado sin rol Administrador responde 403

- GIVEN un usuario autenticado con rol distinto a `Administrador`
- WHEN envía `GET /api/v1/auditorias/filter-options`
- THEN recibe `403 Forbidden`
- AND el cuerpo NO contiene datos de auditoría

#### Scenario: Endpoint filter-options expone solo columnas seguras

- GIVEN cualquier conjunto de registros persistidos
- WHEN el administrador obtiene la respuesta de `filter-options`
- THEN el JSON serializado NO contiene `oldValuesJson`, `newValuesJson`, `entityId`, `userId`, `userName`, `correlationId`, `occurredAt` ni `id`

#### Scenario: Endpoint filter-options ordena alfabéticamente y deduplica

- GIVEN 5 filas con `EntityName` distintos repetidos entre sí
- WHEN se obtiene la respuesta
- THEN el array `entityNames` contiene exactamente los valores únicos ordenados `A-Z`

#### Scenario: Endpoint filter-options descarta cadenas vacías

- GIVEN filas con `EntityName = ""` persistidas por error
- WHEN se obtiene la respuesta
- THEN el array `entityNames` NO contiene cadenas vacías ni null

#### Scenario: Endpoint filter-options se acota a 100 valores por array

- GIVEN una tabla con más de 100 `EntityName` distintos
- WHEN se obtiene la respuesta
- THEN `entityNames` contiene exactamente 100 elementos, ordenados alfabéticamente (los primeros según orden lexicográfico)