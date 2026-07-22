# Delta: habilidad-web-listado-detalle-baja

## Propósito

Agregar la navegación readonly desde el listado de Habilidades hacia las personas asociadas, sin habilitar gestión del vínculo.

## MODIFIED Requirements

### Requirement: Acciones contextuales por segmento

La vista `activas` MUST mostrar `Detalle`, `Cargos`, `Personas`, `Editar` y `Eliminar`. `Cargos` y `Personas` MUST navegar a sus subrecursos preservando `p`, `search`, `sort` y `status`. La vista `eliminadas` MUST ocultar `Detalle`, `Cargos`, `Personas`, `Editar` y `Eliminar`, y MUST mostrar solo `Reactivar`.

(Previously: la vista activa no incluía la acción `Personas` y la vista eliminada solo excluía las acciones existentes.)

#### Scenario: Vista activa muestra acciones de consulta

* **GIVEN** una habilidad activa visible en la grilla
* **WHEN** se renderiza la vista `activas`
* **THEN** la fila MUST exponer `Detalle`, `Cargos`, `Personas`, `Editar` y `Eliminar`.

#### Scenario: Vista eliminada mantiene solo reactivación

* **GIVEN** una habilidad eliminada visible en la grilla
* **WHEN** se renderiza la vista `eliminadas`
* **THEN** la fila MUST mostrar solo `Reactivar` y MUST ocultar `Personas`.

## ADDED Requirements

### Requirement: REQ-HLD-NEW — Botón Personas por habilidad activa

Cada fila activa de `Pages/Organizacion/Habilidades/Index.cshtml` MUST exponer un botón `Personas`, con icono `ti ti-users` y clases `btn-primary btn-icon btn-sm rounded-circle`, que navegue a `Pages/Organizacion/Habilidades/Personas` con el id de la habilidad.

#### Scenario: Navegación desde una fila activa

* **GIVEN** una habilidad activa en `Habilidades/Index`
* **WHEN** el usuario selecciona `Personas`
* **THEN** el enlace MUST incluir el identificador correcto y apuntar a la página de personas.

### Requirement: REQ-HLD-NEW-VISIBILITY — Visibilidad sin gating de rol

El botón MUST renderizarse solo cuando `!Model.IsDeletedView` y MUST ser accesible para cualquier usuario autenticado, sin exigir rol `Administrador`.

#### Scenario: Segmento eliminado o usuario autenticado no administrador

* **GIVEN** una fila eliminada o un usuario autenticado sin rol administrador en activas
* **WHEN** se renderiza el listado
* **THEN** el primer caso MUST ocultar el botón y el segundo MUST mostrarlo.

### Requirement: REQ-HLD-NEW-POSITION — Orden de acciones

El botón `Personas` MUST ubicarse en la columna `Acciones`, entre `Cargos` y `Editar`.

#### Scenario: Orden visual estable

* **GIVEN** una fila activa con todas sus acciones
* **WHEN** se renderiza la columna `Acciones`
* **THEN** `Personas` MUST aparecer después de `Cargos` y antes de `Editar`.
