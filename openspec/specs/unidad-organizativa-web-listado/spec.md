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

El sistema MUST renderizar el listado con la base visual `Complete Custom Table`, MUST consultar datos mediante `GET /api/v1/unidades-organizativas/consulta`, MUST mostrar búsqueda, paginación y affordances de ordenamiento, y MUST limitar el ordenamiento del primer corte al conjunto visible de filas.

#### Scenario: Carga inicial del listado

- GIVEN un usuario autenticado abre la página por primera vez
- WHEN el listado se renderiza
- THEN la interfaz MUST mostrar una tabla con filas de unidades organizativas
- AND MUST mostrar un buscador, controles de paginación y una acción visible de eliminar por fila

#### Scenario: Búsqueda sin resultados

- GIVEN un usuario autenticado en el listado
- WHEN busca un texto que no coincide con ninguna unidad organizativa activa
- THEN la interfaz MUST mostrar un estado vacío entendible
- AND MUST NOT mostrar filas de datos anteriores como si siguieran vigentes

#### Scenario: Cambio de página

- GIVEN un resultado con más de una página disponible
- WHEN el usuario navega a otra página del listado
- THEN la interfaz MUST mostrar el subconjunto correspondiente a esa página
- AND MUST actualizar el indicador visible de página actual

#### Scenario: Ordenamiento de la página visible

- GIVEN una página con varias filas visibles y una columna ordenable
- WHEN el usuario ordena por esa columna
- THEN la interfaz MUST reordenar las filas visibles según el criterio elegido
- AND MUST mantener al usuario en la misma página del listado

#### Scenario: Error al consultar el listado

- GIVEN que la consulta del listado falla
- WHEN el usuario abre o refresca la página
- THEN la interfaz MUST mostrar un mensaje de error visible
- AND MUST permanecer utilizable para reintentar la consulta

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
