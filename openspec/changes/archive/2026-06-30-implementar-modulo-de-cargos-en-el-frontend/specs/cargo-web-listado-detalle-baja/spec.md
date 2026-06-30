# Especificación de listado, detalle y baja web de cargos

## Purpose

Definir el primer slice autenticado de `Cargos` en `SGV.Web` para consultar cargos activos, ver su detalle readonly y ejecutar baja lógica sin expandirse a create, edit, skills, eliminados o reactivación.

## Requirements

### Requirement: Acceso autenticado al módulo de cargos

El sistema MUST exponer páginas Razor protegidas para listado y detalle de `Cargos` dentro del shell autenticado.

#### Scenario: Usuario autenticado abre el módulo

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega al módulo `Cargos`
- THEN la aplicación MUST responder con el listado dentro del shell autenticado
- AND la vista MUST mostrar el título del módulo.

#### Scenario: Usuario anónimo intenta acceder

- GIVEN un usuario no autenticado
- WHEN solicita la URL del listado o del detalle de cargos
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`.

### Requirement: Listado visible de cargos activos

El sistema MUST renderizar una tabla de cargos activos usando el patrón visual del shell, MUST consultar exclusivamente el contrato existente de lectura activa y MUST exponer por fila solo acciones de detalle y baja lógica. La interfaz MUST NOT mostrar create, edit, skills, eliminados ni reactivación en este slice.

#### Scenario: Carga inicial del listado

- GIVEN un usuario autenticado abre `Cargos`
- WHEN la página termina de cargar
- THEN la tabla MUST mostrar cargos activos devueltos por el backend
- AND cada fila MUST ofrecer acciones visibles de detalle y baja.

#### Scenario: Listado sin resultados activos

- GIVEN que la consulta de cargos activos no devuelve filas
- WHEN el usuario abre el listado
- THEN la interfaz MUST mostrar un estado vacío entendible
- AND MUST seguir sin exponer acciones fuera del alcance definido.

### Requirement: Detalle readonly con retorno seguro

El sistema MUST mostrar en detalle los datos legibles del cargo en modo solo lectura, MUST ofrecer una acción visible para volver al listado y MUST mostrar un estado recuperable cuando el cargo solicitado no pueda consultarse.

#### Scenario: Apertura de detalle existente

- GIVEN un cargo activo existente
- WHEN el usuario abre su detalle desde el listado
- THEN la página MUST mostrar sus datos en modo solo lectura
- AND MUST ofrecer una acción visible para volver al listado.

#### Scenario: Cargo no disponible en detalle

- GIVEN un identificador de cargo que ya no puede consultarse como activo
- WHEN el usuario abre la pantalla de detalle
- THEN la interfaz MUST mostrar un mensaje visible de no disponible o error recuperable
- AND MUST ofrecer un camino claro para volver al listado.

### Requirement: Baja lógica confirmada con feedback de conflicto

El sistema MUST solicitar confirmación antes de ejecutar la baja lógica, MUST remover el cargo del listado activo cuando la operación sea exitosa y MUST traducir rechazos por conflicto a feedback claro y accionable.

#### Scenario: Usuario cancela la confirmación

- GIVEN una fila con acción de baja visible
- WHEN el usuario inicia la baja y cancela la confirmación
- THEN la aplicación MUST NOT ejecutar la eliminación
- AND la fila MUST permanecer visible en el listado.

#### Scenario: Baja lógica exitosa

- GIVEN un cargo activo eliminable visible en la tabla
- WHEN el usuario confirma la baja y el backend responde éxito
- THEN la interfaz MUST volver al listado activo con confirmación visible
- AND el cargo eliminado MUST dejar de verse.

#### Scenario: Baja rechazada por conflicto

- GIVEN un cargo activo cuya baja es rechazada por dependencias
- WHEN el usuario confirma la baja
- THEN la interfaz MUST mostrar un mensaje claro que indique el conflicto
- AND el cargo MUST permanecer visible para el usuario.
