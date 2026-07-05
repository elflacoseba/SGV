# Status: MODIFIED — habilidad-web-listado-detalle-baja

## Purpose

Actualizar la spec base del listado web de `Habilidades` para permitir navegación readonly desde filas activas hacia los cargos asociados, sin incorporar edición del vínculo `habilidad↔cargo` dentro de este slice.

## Delta desde la spec base

- **ADDED**: nueva acción `Cargos` en la columna de acciones de `Habilidades/Index` solo para filas activas.
- **ADDED**: navegación a `/organizacion/habilidades/{habilidadId}/cargos` preservando `p`, `search`, `sort` y `status`.
- **REMOVED**: la exclusión total de lectura `habilidad↔cargo` en el listado; el fuera de alcance queda acotado a edición o gestión del vínculo.

## MODIFIED Requirements

### Requirement: Acciones contextuales por segmento

La vista `activas` MUST mostrar `Detalle`, `Cargos`, `Editar` y `Eliminar`. La acción `Cargos` MUST renderizarse solo para filas activas y MUST navegar a `/organizacion/habilidades/{habilidadId}/cargos` preservando `p`, `search`, `sort` y `status` del listado de origen. La vista `eliminadas` MUST ocultar `Detalle`, `Cargos`, `Editar` y `Eliminar` y MUST mostrar solo `Reactivar` por fila.

(Previously: la vista `activas` mostraba solo `Detalle`, `Editar` y `Eliminar`, y la navegación de lectura `habilidad↔cargo` quedaba fuera de alcance.)

#### Scenario: Vista activas muestra acciones del catálogo activo

- GIVEN una habilidad activa visible en la grilla
- WHEN se renderiza la vista `activas`
- THEN la fila MUST exponer `Detalle`, `Cargos`, `Editar` y `Eliminar`.

#### Scenario: Navegación a cargos preserva contexto del listado

- GIVEN un usuario está en `Habilidades/Index` con `p`, `search`, `sort` y `status` vigentes
- WHEN hace click en `Cargos` sobre una fila activa
- THEN la solicitud MUST llegar a `/organizacion/habilidades/{habilidadId}/cargos` con el `habilidadId` correcto
- AND MUST preservar en la URL los valores de `p`, `search`, `sort` y `status`.

#### Scenario: Vista eliminadas muestra solo reactivación

- GIVEN una habilidad eliminada visible en la grilla
- WHEN se renderiza la vista `eliminadas`
- THEN la fila MUST mostrar solo `Reactivar`
- AND MUST ocultar `Detalle`, `Cargos`, `Editar` y `Eliminar`.
