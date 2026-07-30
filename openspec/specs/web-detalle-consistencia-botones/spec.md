# Delta: Consistencia de botones en vistas Detalle (web-detalle-consistencia-botones)

## Purpose

Normalizar la barra de botones "Editar / Volver al listado" en las 6 vistas
`Details.cshtml` de los módulos de organización y personas (`Cargos`,
`Habilidades`, `Puestos`, `UnidadesOrganizativas`, `Ocupaciones` y `Personas`),
tomando como canónicos `Cargos/Details.cshtml` y `Personas/Details.cshtml`.

## ADDED Requirements

### Requirement: REQ-DET-BTN-001 Ubicación de la barra fuera del card

La barra de botones "Editar / Volver al listado" de las 6 vistas Detalle MUST
renderizarse en un `<div class="row mt-3"><div class="col-12 d-flex gap-2">…</div></div>`
situado fuera del `<div class="card">` principal. MUST NOT ubicarse dentro de
`card-body` ni de `card-footer`.

#### Scenario: Botones fuera del card en recurso existente

- GIVEN un recurso existente de cualquier módulo con vista Detalle
- WHEN se renderiza `<modulo>/Details/{id}`
- THEN los botones Editar y Volver MUST aparecer en un `<div class="row mt-3">` separado
- AND MUST NOT estar anidados dentro del card de datos del recurso.

### Requirement: REQ-DET-BTN-002 Botón Editar canónico

El botón Editar de las 6 vistas Detalle MUST usar `class="btn btn-warning"` y un
ícono `<i class="ti ti-pencil me-1"></i>` inmediatamente antes del texto "Editar".
MUST NOT usar `btn-outline-warning` ni el ícono `ti-edit`.

#### Scenario: Botón Editar usa clase e ícono canónicos

- GIVEN cualquier Details renderizada con permisos de edición
- WHEN se construye la barra de botones
- THEN el botón Editar MUST contener el atributo `class="btn btn-warning"` y el `<i class="ti ti-pencil me-1">`.

### Requirement: REQ-DET-BTN-003 Botón Volver al listado canónico

El botón Volver al listado MUST usar `class="btn btn-outline-secondary"` con un
ícono `<i class="ti ti-arrow-left me-1"></i>`. MUST NOT usar `btn-light` ni
omitir el ícono de retorno.

#### Scenario: Botón Volver usa clase e ícono canónicos

- GIVEN cualquier Details renderizada (recurso existente o estado 404)
- WHEN se renderiza el botón de retorno al listado fuera del card
- THEN el botón MUST contener `class="btn btn-outline-secondary"` y `<i class="ti ti-arrow-left me-1">`.

### Requirement: REQ-DET-BTN-004 URL de Editar preserva estado de paginación

El `href` del botón Editar de Ocupaciones MUST generarse con
`Url.Page("/Organizacion/Ocupaciones/Edit", new { id, p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`.
MUST NOT usar URLs hardcodeadas con interpolación del id. `UnidadesOrganizativas`
y los módulos canónicos MUST preservar los parámetros de retorno que ya usaban
(`returnPage`/`returnSearch`/`returnSort`, o `p`/`search`/`sort` según el módulo).

#### Scenario: Editar en Ocupaciones preserva p/search/sort

- GIVEN se invoca `Ocupaciones/Details/{id}?p=2&search=foo&sort=Nombre`
- WHEN se renderiza el botón Editar
- THEN su `href` MUST incluir `p=2`, `search=foo` y `sort=Nombre` junto con el `id`.

### Requirement: REQ-DET-BTN-005 URL de Volver preserva estado de paginación

El `href` del botón Volver de Ocupaciones MUST generarse con
`Url.Page("/Organizacion/Ocupaciones/Index", new { p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`.
`UnidadesOrganizativas` MUST seguir usando `Model.ReturnToListUrl` (que ya
preserva `returnPage` y el resto del contexto de listado). Los módulos canónicos
MUST preservar `p`/`search`/`sort` como ya lo hacen.

#### Scenario: Volver en Ocupaciones preserva p/search/sort

- GIVEN se invoca `Ocupaciones/Details/{id}?p=2&search=foo&sort=Nombre`
- WHEN se renderiza el botón Volver al listado
- THEN su `href` MUST incluir `p=2`, `search=foo` y `sort=Nombre`.

#### Scenario: Volver en UnidadesOrganizativas preserva returnPage

- GIVEN se abre `UnidadesOrganizativas/Details/{id}` desde el Index con `returnPage=2`
- WHEN se renderiza el botón Volver (recurso existente o estado 404)
- THEN su `href` MUST provenir de `Model.ReturnToListUrl` y MUST preservar `p=2` (vía `returnPage`).

### Requirement: REQ-DET-BTN-006 Estructura contenedora de la barra

La barra de botones de las 6 vistas Detalle MUST usar la estructura
`<div class="row mt-3"><div class="col-12 d-flex gap-2">…</div></div>` como
contenedor de los botones Editar/Volver, con `gap-2` para el espaciado horizontal.

#### Scenario: Contenedor canónico con gap-2

- GIVEN cualquier Details con recurso existente
- WHEN se renderiza la barra de botones
- THEN MUST contener `<div class="col-12 d-flex gap-2">` envolviendo a los botones Editar y Volver.

## MODIFIED Requirements

(ninguno — capability nueva; no existe `openspec/specs/web-detalle-consistencia-botones/`.)

## REMOVED Requirements

(ninguno.)

## RENAMED Requirements

(ninguno.)