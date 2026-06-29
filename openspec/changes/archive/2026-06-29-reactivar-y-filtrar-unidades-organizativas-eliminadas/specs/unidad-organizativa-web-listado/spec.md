# Delta for unidad-organizativa-web-listado

## MODIFIED Requirements

### Requirement: Tabla consultable de unidades organizativas

El sistema MUST renderizar el listado con la base visual `Complete Custom Table`, MUST consultar datos mediante `GET /api/v1/unidades-organizativas/consulta`, MUST permitir alternar entre la vista de unidades activas y la vista de unidades eliminadas, y MUST preservar búsqueda, paginación y affordances de ordenamiento dentro de la vista seleccionada. La vista inicial MUST ser la de activas y la tabla MUST NOT mezclar activas y eliminadas en una misma grilla.
(Previously: el listado solo consultaba y mostraba unidades activas, sin filtro explícito de estado.)

#### Scenario: Carga inicial del listado

- GIVEN un usuario autenticado abre la página por primera vez
- WHEN el listado se renderiza
- THEN la interfaz MUST mostrar por defecto la vista de unidades activas
- AND MUST mostrar buscador, paginación, ordenamiento y una acción visible para cambiar a eliminadas.

#### Scenario: Búsqueda sin resultados en la vista seleccionada

- GIVEN un usuario autenticado en la vista activa o eliminada
- WHEN busca un texto que no coincide con ninguna unidad de esa vista
- THEN la interfaz MUST mostrar un estado vacío entendible
- AND MUST NOT mostrar filas de otra vista como si fueran vigentes.

#### Scenario: Cambio de página en la vista seleccionada

- GIVEN un resultado con más de una página disponible en la vista activa o eliminada
- WHEN el usuario navega a otra página del listado
- THEN la interfaz MUST mostrar el subconjunto correspondiente a esa página
- AND MUST mantener el filtro de estado seleccionado.

#### Scenario: Ordenamiento de la página visible

- GIVEN una página con varias filas visibles y una columna ordenable
- WHEN el usuario ordena por esa columna
- THEN la interfaz MUST reordenar las filas visibles según el criterio elegido
- AND MUST mantener al usuario en la misma página y en la misma vista del listado.

#### Scenario: Error al consultar el listado

- GIVEN que la consulta del listado falla
- WHEN el usuario abre o refresca la página
- THEN la interfaz MUST mostrar un mensaje de error visible
- AND MUST permanecer utilizable para reintentar la consulta conservando la vista seleccionada.

### Requirement: Reactivación visible desde el flujo de listado

La shell web MUST ofrecer en la vista de unidades eliminadas una acción visible por fila para reactivar unidades organizativas. Tras una reactivación exitosa, la shell MUST devolver al usuario al listado de unidades activas con confirmación visible. Si la reactivación falla, la shell MUST conservar la vista de eliminadas y MUST mostrar un mensaje claro y accionable.
(Previously: la shell solo ofrecía reactivación visible para la última unidad eliminada dentro del flujo del listado activo.)

#### Scenario: Vista de eliminadas con acción por fila

- GIVEN un usuario autenticado cambia a la vista de unidades eliminadas
- WHEN la tabla termina de renderizarse
- THEN cada fila eliminada MUST mostrar una acción visible de reactivación
- AND la tabla MUST NOT mostrar filas activas en esa vista.

#### Scenario: Reactivación exitosa desde la vista de eliminadas

- GIVEN una unidad eliminada visible en la vista de eliminadas
- WHEN el usuario confirma la restauración y el backend responde éxito
- THEN la shell MUST invocar el contrato existente de reactivación
- AND MUST redirigir al listado de activas con mensaje de éxito visible.

#### Scenario: Conflicto al reactivar desde la vista de eliminadas

- GIVEN una unidad eliminada visible en la vista de eliminadas
- WHEN el backend rechaza la restauración por conflicto
- THEN la shell MUST mostrar un mensaje claro y accionable
- AND MUST conservar la vista de eliminadas para reintentar o salir del flujo.
