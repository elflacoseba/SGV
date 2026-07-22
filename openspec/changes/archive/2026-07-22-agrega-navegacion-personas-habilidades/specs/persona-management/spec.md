# Delta: persona-management

## Propósito

Hacer descubrible desde el listado de Personas la página existente de gestión de habilidades, preservando autorización y contexto de navegación.

## ADDED Requirements

### Requirement: REQ-PM-NEW — Botón Habilidades por Persona activa

Cada fila activa de `Pages/Personas/Index.cshtml` MUST exponer un botón `Habilidades`, con icono `ti ti-stars` y clases `btn-primary btn-icon btn-sm rounded-circle`, que navegue a `Pages/Personas/PersonaHabilidades` con el id de la persona.

#### Scenario: Administrador navega desde una fila activa

* **GIVEN** un Administrador en el listado de Personas y una fila activa
* **WHEN** selecciona `Habilidades`
* **THEN** el enlace MUST incluir el id correcto y apuntar a `PersonaHabilidades`.

### Requirement: REQ-PM-NEW-ADMIN — Gating por rol y segmento

El botón MUST renderizarse solo si `Model.EsAdministrador` y la vista no es `IsDeletedView`.

#### Scenario: Gating de visibilidad

* **GIVEN** una fila activa o eliminada y un usuario administrador o no administrador
* **WHEN** se renderiza el listado
* **THEN** solo la combinación activa + administrador MUST mostrar el botón.

### Requirement: REQ-PM-NEW-POSITION — Orden de acciones

El botón `Habilidades` MUST ubicarse en la columna `Acciones`, entre `Detalle` y `Editar`.

#### Scenario: Orden visual del listado

* **GIVEN** una fila activa visible para un administrador
* **WHEN** se renderiza la columna `Acciones`
* **THEN** `Habilidades` MUST aparecer después de `Detalle` y antes de `Editar`.

### Requirement: REQ-PM-NEW-CONTEXT — Preservación del contexto de listado

`BuildHabilidadesRouteValues` MUST conservar `page`, `search`, `sort` y `status` del listado actual al construir la ruta hacia `PersonaHabilidades`.

#### Scenario: Regreso sin perder filtros

* **GIVEN** un listado con `page`, `search`, `sort` y `status` definidos
* **WHEN** se construye el enlace `Habilidades`
* **THEN** la ruta MUST transportar los cuatro valores para permitir volver al contexto original.
