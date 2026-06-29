# Especificación de listado web de unidades organizativas

## Purpose

Definir el primer corte autenticado de `SGV.Web` para consultar, eliminar y reactivar unidades organizativas desde una tabla tipo Inspinia, sin alta, edición, detalle, árbol ni cambio de padre.

## Requirements

### Requirement: Acceso autenticado al listado

El sistema MUST exponer una página Razor protegida para `Unidades Organizativas` dentro del shell autenticado y MUST permitir llegar a ella desde la navegación principal.

#### Scenario: Usuario autenticado abre el listado

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega al módulo `Unidades Organizativas`
- THEN la aplicación MUST responder con la página del listado
- AND la vista MUST mostrar el título del módulo dentro del shell autenticado

#### Scenario: Usuario anónimo intenta acceder al listado

- GIVEN un usuario no autenticado
- WHEN solicita la URL del listado de unidades organizativas
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`

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

### Requirement: Eliminación confirmada con SweetAlert2

El sistema MUST solicitar confirmación con SweetAlert2 antes de ejecutar `DELETE /api/v1/unidades-organizativas/{id}`, MUST refrescar el listado tras una eliminación exitosa, y MUST conservar la fila visible cuando la operación es cancelada o rechazada por el backend.

#### Scenario: Usuario cancela la eliminación

- GIVEN una fila con acción de eliminar visible
- WHEN el usuario inicia la eliminación y cancela la confirmación
- THEN la aplicación MUST NOT ejecutar la eliminación
- AND la fila MUST permanecer visible en el listado

#### Scenario: Eliminación exitosa

- GIVEN una unidad organizativa eliminable visible en la tabla
- WHEN el usuario confirma la eliminación y el backend responde éxito
- THEN la interfaz MUST refrescar el listado
- AND la fila eliminada MUST dejar de verse

#### Scenario: Eliminación rechazada por dependencias

- GIVEN una unidad organizativa visible cuya eliminación es rechazada por el backend
- WHEN el usuario confirma la eliminación
- THEN la interfaz MUST mostrar un feedback de error claro
- AND la fila MUST permanecer visible en el listado

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
