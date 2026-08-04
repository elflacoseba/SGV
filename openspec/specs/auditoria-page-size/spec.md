# auditoria-page-size Specification

## Purpose

Definir el selector de `PageSize` (10/20/50/100) en la shell web del listado de auditoría, propagado vía querystring y preservado en los enlaces de paginación y de orden. Es una capability nueva complementaria a `auditoria-query` (que define el clamping API 1–100).

## Requirements

### Requirement: Selector de PageSize con opciones 10/20/50/100

`Pages/Auditorias/Index` SHALL renderizar un `<select>` de `PageSize` con las opciones `10`, `20`, `50` y `100`. La opción seleccionada MUST reflejar el `PageSize` actual de la query. El valor por defecto (cuando `pageSize` se omite o es inválido) MUST ser el default del sistema (`20`). El selector MUST propagar el cambio vía querystring `pageSize` y resetear `Page` a `1` al cambiar el tamaño de página.

#### Scenario: Selector renderiza las cuatro opciones

- GIVEN el administrador abre `/auditorias`
- WHEN se renderiza el selector de `pageSize`
- THEN las opciones disponibles son `10`, `20`, `50` y `100`
- AND la opción `20` queda seleccionada por defecto

#### Scenario: Selector refleja el pageSize actual

- GIVEN el administrador navega con `?pageSize=50`
- WHEN se renderiza el selector
- THEN la opción `50` queda marcada como activa

#### Scenario: Cambiar pageSize reinicia a página 1

- GIVEN el administrador está en `?page=3&pageSize=20`
- WHEN selecciona `pageSize=100` en el selector
- THEN navega a `?page=1&pageSize=100`

#### Scenario: PageSize omitido cae a default 20

- GIVEN el administrador navega sin `pageSize`
- WHEN se procesa la página
- THEN el `PageSize` efectivo es `20` y el selector lo refleja

### Requirement: Enlaces de paginación preservan PageSize

`BuildPagedRouteValues` en `Index.cshtml.cs` MUST incluir el `PageSize` actual en los route values de los enlaces de paginación (Anterior/Siguiente/número de página), de modo que al navegar entre páginas se conserve el tamaño elegido. Lo mismo aplica a los enlaces de orden (`BuildSortRouteValues`), que MUST preservar `pageSize` al cambiar `Sort`.

#### Scenario: Paginación conserva pageSize

- GIVEN el administrador está en `?page=1&pageSize=50`
- WHEN hace click en «Siguiente»
- THEN navega a `?page=2&pageSize=50`

#### Scenario: Cambiar sort conserva pageSize

- GIVEN el administrador está en `?page=1&pageSize=100&sort=fecha_desc`
- WHEN hace click en el header de `entidad_asc`
- THEN navega a `?page=1&pageSize=100&sort=entidad_asc`

### Requirement: PageSize inválido o fuera de rango se normaliza

La shell web MUST normalizar `pageSize` fuera del conjunto `{10,20,50,100}` (y del rango API 1–100) al default `20` antes de construir la query hacia la API. Valores no numéricos o negativos MUST caer a `20` sin error. La API conserva su propio clamping 1–100 (definido en `auditoria-query`), por lo que la shell es la primera línea de normalización del selector.

#### Scenario: PageSize no numérico cae a default

- GIVEN el administrador navega con `?pageSize=abc`
- WHEN la página procesa el request
- THEN el `PageSize` efectivo es `20`

#### Scenario: PageSize fuera de las opciones cae a default

- GIVEN el administrador navega con `?pageSize=15`
- WHEN la página procesa el request
- THEN el `PageSize` efectivo es `20` (no está en `{10,20,50,100}`)

## Notas de implementación (no normativas)

- `Index.cshtml.cs` mantiene `DefaultPageSize = 20` como constante; el selector y la normalización la referencian.
- El clamping 1–100 de la API garantiza que un `pageSize=100` explícito nunca sea recortado por encima.
