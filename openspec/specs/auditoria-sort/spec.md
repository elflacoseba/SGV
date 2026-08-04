# auditoria-sort Specification

## Purpose

Definir el ordenamiento server-side del listado de auditoría por cinco columnas (`fecha`, `entidad`, `operacion`, `usuario`, `correlacion`), con default `fecha_desc`, desempate determinista por `Id`, y la regla web de resetear a página 1 al cambiar el criterio de orden. Es una capability nueva complementaria a `auditoria-query`.

## Requirements

### Requirement: Ordenamiento server-side por cinco columnas

`Sort` (`string?`) en `AuditoriaListQuery` SHALL aceptar exactamente los valores: `fecha_asc`, `fecha_desc`, `entidad_asc`, `entidad_desc`, `operacion_asc`, `operacion_desc`, `usuario_asc`, `usuario_desc`, `correlacion_asc`, `correlacion_desc`. El ordenamiento MUST aplicarse server-side en la query de persistencia (no client-side) y se mapea así:

| `Sort` | Columna |
|--------|---------|
| `fecha_asc/desc` | `OccurredAt` |
| `entidad_asc/desc` | `EntityName` |
| `operacion_asc/desc` | `Operation` |
| `usuario_asc/desc` | `UserId` (listado) |
| `correlacion_asc/desc` | `CorrelationId` |

El orden default, cuando `Sort` es nulo/vacío/inválido, MUST ser `fecha_desc` (equivale a `OccurredAt DESC, Id DESC`). Cualquier `Sort` no reconocido MUST caer a `fecha_desc` sin error, para no romper la consulta por input malformado.

#### Scenario: Default fecha_desc cuando Sort se omite

- GIVEN un administrador que omite `sort`
- WHEN envía `GET /api/v1/auditorias`
- THEN los registros se ordenan por `OccurredAt DESC, Id DESC`

#### Scenario: Orden por entidad ascendente

- GIVEN registros con distintos `EntityName`
- WHEN se envía `?sort=entidad_asc`
- THEN los ítems se devuelven ordenados alfabéticamente por `EntityName` ascendente

#### Scenario: Sort inválido cae a default sin error

- GIVEN un administrador que envía `?sort=cualquierCosa`
- WHEN la API procesa la query
- THEN responde `200` con orden `fecha_desc` (NO `400`)

#### Scenario: Dirección descendente respetada

- GIVEN registros con distintos `Operation`
- WHEN se envía `?sort=operacion_desc`
- THEN los ítems se ordenan por `Operation` descendente

### Requirement: Desempate determinista por Id

Todo ordenamiento SHALL desempatar por `Id` en la dirección opuesta implícitamente determinista (ascendente cuando el sort primario es `desc` y viceversa, o simplemente `Id DESC` para el default) de modo que el orden sea estable entre páginas y testeable. El desempate MUST aplicarse siempre, sin excepción, incluso cuando el sort primario es único.

#### Scenario: Empate en columna primaria se rompe por Id

- GIVEN dos registros con igual valor en la columna ordenada
- WHEN se aplica cualquier `Sort`
- THEN el de `Id` mayor aparece primero en orden `desc`, o después en `asc`, de forma determinista

### Requirement: Reset a página 1 al cambiar sort en la shell web

`Pages/Auditorias/Index` SHALL resetear `Page` a `1` cuando el administrador cambia el criterio `Sort`. Los enlaces de los headers ordenables (`<th>`) MUST incluir el `Sort` actual en sus route values y NO preservar `Page` anterior. Los enlaces de paginación MUST preservar el `Sort` activo.

#### Scenario: Cambiar sort reinicia a página 1

- GIVEN el administrador está en `?page=3&sort=fecha_desc`
- WHEN hace click en el header de `entidad_asc`
- THEN navega a `?page=1&sort=entidad_asc`

#### Scenario: Paginación preserva sort activo

- GIVEN el administrador está en `?page=1&sort=operacion_desc`
- WHEN hace click en «Siguiente»
- THEN navega a `?page=2&sort=operacion_desc`

#### Scenario: Indicador visual de dirección activa

- GIVEN un `Sort` activo en la tabla
- WHEN se renderizan los headers
- THEN el header de la columna activa muestra un indicador de dirección `asc`/`desc`

## Notas de implementación (no normativas)

- El sort dinámico se resuelve con un `switch` expresión sobre `Sort` que devuelve el `IOrderedQueryable`/`OrderBy` apropiado, manteniendo `AsNoTracking`.
- Verificar con `EXPLAIN` que el nuevo índice `(CorrelationId, OccurredAt DESC)` (definido en `auditoria-query`/configuración) evite `Using filesort` para `correlacion_desc`.
