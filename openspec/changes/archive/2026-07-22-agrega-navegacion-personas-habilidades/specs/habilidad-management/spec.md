# Delta: habilidad-management

## Propósito

Incorporar la página web readonly que permite consultar las personas asociadas a una habilidad, manteniendo la gestión del vínculo en el módulo de Personas.

## ADDED Requirements

### Requirement: REQ-HM-NEW-PAGE — Página de personas por habilidad

El sistema MUST exponer `Pages/Organizacion/Habilidades/Personas.cshtml` para mostrar las personas que poseen una habilidad específica. La página MUST ofrecer paginación, búsqueda, orden y toggle entre `activas` y `eliminadas`, mostrando legajo, apellidos, nombres, email y nivel.

#### Scenario: Consulta paginada con segmento

* **GIVEN** una habilidad válida y un usuario autenticado
* **WHEN** abre `Habilidades/Personas` con búsqueda, orden o `status`
* **THEN** la grilla MUST mostrar los resultados paginados del segmento solicitado.

#### Scenario: Sin resultados

* **GIVEN** una habilidad sin personas que coincidan con el filtro
* **WHEN** se carga la página
* **THEN** MUST mostrar un estado vacío legible y conservar los controles de búsqueda y segmento.

### Requirement: REQ-HM-NEW-AUTH — Acceso de lectura autenticado

La página MUST usar `[Authorize]` sin restricción de rol; cualquier usuario autenticado puede consultar y un anónimo MUST ser redirigido al sign-in.

#### Scenario: Acceso anónimo y autenticado

* **GIVEN** una solicitud anónima o de un usuario autenticado
* **WHEN** se solicita la página
* **THEN** el anónimo MUST ser redirigido al sign-in y el autenticado MUST poder cargarla.

### Requirement: REQ-HM-NEW-READONLY — Sin gestión de asociaciones

La página MUST ser solo lectura y MUST NOT exponer formularios de alta, modificación o baja de habilidad sobre persona. La gestión MUST continuar exclusivamente en `Pages/Personas/PersonaHabilidades`.

#### Scenario: Acciones de la página

* **GIVEN** una página cargada con personas asociadas
* **WHEN** se inspeccionan sus acciones
* **THEN** MUST existir navegación de consulta, pero MUST NOT existir alta, baja o edición del vínculo.

### Requirement: REQ-HM-NEW-LINK — Enlace al detalle de Persona

Cada fila de la grilla MUST enlazar al detalle correspondiente en `Pages/Personas/Details` usando el identificador de la persona.

#### Scenario: Navegación al detalle

* **GIVEN** una fila con `PersonaId` válido
* **WHEN** el usuario selecciona la persona
* **THEN** el enlace MUST apuntar al detalle de esa misma persona.
