# Delta for unidad-organizativa-web-listado

## ADDED Requirements

### Requirement: Reactivación visible desde el flujo de listado

La shell web MUST ofrecer una acción visible para reactivar la última unidad eliminada desde el flujo del listado, sin romper búsqueda, paginación, orden ni la restricción de que la tabla principal solo muestra unidades activas.

#### Scenario: Eliminación exitosa con siguiente paso visible

- GIVEN una unidad organizativa visible en el listado
- WHEN el usuario la elimina correctamente
- THEN la página MUST mostrar confirmación visible
- AND MUST ofrecer una acción visible para reactivarla preservando el contexto del listado.

#### Scenario: Reactivación exitosa desde el flujo de listado

- GIVEN una unidad recién eliminada con acción visible de reactivación
- WHEN el usuario confirma la restauración
- THEN la shell MUST invocar el contrato existente de reactivación
- AND MUST devolver al usuario al listado con mensaje de éxito y contexto preservado.

#### Scenario: Conflicto al reactivar desde el listado

- GIVEN una acción visible de reactivación para una unidad eliminada
- WHEN el backend rechaza la restauración por conflicto
- THEN la shell MUST mostrar un mensaje claro y accionable
- AND MUST conservar el contexto del listado para reintentar o salir del flujo.
