# Delta for unidad-organizativa-web-listado

## ADDED Requirements

### Requirement: Acciones de navegación para administrar unidades

El sistema MUST exponer desde el listado una acción visible para crear unidades organizativas y MUST ofrecer por fila acciones visibles para ver detalle y editar, sin degradar búsqueda, paginación ni eliminación.

#### Scenario: El listado ofrece create, detail y edit

- GIVEN un usuario autenticado en el listado de unidades organizativas
- WHEN la tabla termina de renderizarse
- THEN la interfaz MUST mostrar una acción visible de crear
- AND cada fila MUST mostrar acciones visibles de ver detalle y editar.

#### Scenario: Navegación conserva el contexto del listado

- GIVEN un usuario navega desde el listado filtrado u ordenado hacia detail o edit
- WHEN vuelve al listado mediante la navegación provista por la UI
- THEN la página MUST restaurar el contexto visible de búsqueda, página y orden vigente
- AND MUST seguir permitiendo eliminar desde ese mismo contexto.
