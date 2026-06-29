# Especificación de detalle y edición web de unidades organizativas

## Purpose

Agregar el primer flujo administrativo de create/detail/edit/reactivar en `SGV.Web` para unidades organizativas, reutilizando el backend actual y sin ampliar alcance a árbol.

## Requirements

### Requirement: Acceso autenticado a create, detail y edit

El sistema MUST exponer páginas Razor protegidas para crear, ver detalle y editar unidades organizativas dentro del shell autenticado.

#### Scenario: Usuario autenticado abre una pantalla de create/detail/edit

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega a create, detail o edit de `Unidades Organizativas`
- THEN la aplicación MUST responder con la pantalla solicitada dentro del shell autenticado
- AND MUST mostrar una acción visible para volver al listado.

#### Scenario: Usuario anónimo intenta acceder a una pantalla protegida

- GIVEN un usuario no autenticado
- WHEN solicita la URL de create, detail o edit
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`.

#### Scenario: La unidad solicitada ya no existe

- GIVEN una unidad organizativa inexistente o eliminada
- WHEN un usuario abre detail o edit para ese identificador
- THEN la interfaz MUST mostrar un estado recuperable de no disponible
- AND MUST ofrecer un camino claro para volver al listado.

### Requirement: Datos visibles y editables de la unidad organizativa

El sistema MUST mostrar en create/edit los campos `codigo`, `nombre`, `descripcion`, `vigenteDesde`, `vigenteHasta`, `tipoUnidadOrganizativa` y `unidadPadre`; en detail MUST mostrarlos en modo solo lectura, incluyendo el contexto legible de la unidad padre actual.

#### Scenario: Create carga catálogos necesarios

- GIVEN un usuario autenticado abre create
- WHEN la pantalla termina de cargar
- THEN la interfaz MUST mostrar un formulario vacío
- AND MUST ofrecer opciones seleccionables para tipo y unidad padre.

#### Scenario: Detail o edit muestran el padre actual

- GIVEN una unidad organizativa existente con unidad padre asignada
- WHEN un usuario abre detail o edit
- THEN la interfaz MUST mostrar el padre actual de forma legible
- AND en edit MUST permitir reemplazarlo o dejar la unidad sin padre.

### Requirement: Guardado con feedback accionable

El sistema MUST reutilizar los contratos actuales de create, update y cambio de padre, y MUST traducir validaciones y conflictos del backend a mensajes claros y accionables para administradores.

#### Scenario: Create exitoso

- GIVEN datos válidos para una nueva unidad organizativa
- WHEN el usuario confirma create
- THEN la interfaz MUST persistir la unidad
- AND MUST mostrar confirmación visible con un siguiente paso útil.

#### Scenario: Edit exitoso con cambio de padre

- GIVEN una unidad existente y datos editables válidos con nuevo padre válido
- WHEN el usuario confirma edit
- THEN la interfaz MUST reflejar los cambios persistidos
- AND MUST mostrar el nuevo padre como parte del resultado.

#### Scenario: Error de validación por campo

- GIVEN un formulario con datos inválidos
- WHEN el backend responde errores de validación
- THEN la interfaz MUST asociar los errores a los campos correspondientes
- AND MUST conservar los datos ingresados que permitan corregir el formulario.

#### Scenario: Conflicto de negocio o fallo parcial

- GIVEN un intento de create o edit rechazado por código duplicado, jerarquía inválida o imposibilidad de completar todos los cambios pedidos
- WHEN la operación finaliza con conflicto
- THEN la interfaz MUST explicar qué cambio no pudo aplicarse
- AND MUST indicar una acción concreta para corregir o reintentar.

### Requirement: Estado recuperable para unidades eliminadas

Cuando detail o edit se abren con un identificador que ya no aparece en lecturas activas, la shell web MUST mostrar un estado recuperable con opción visible de reactivación usando el contrato existente, en lugar de limitarse a un estado terminal de no disponible.

#### Scenario: Detail muestra una acción de reactivar

- GIVEN un identificador de unidad eliminada solicitado en detail
- WHEN la lectura activa no devuelve datos
- THEN la página MUST mostrar un estado recuperable
- AND MUST ofrecer una acción visible para reactivar y volver al listado.

#### Scenario: Edit bloquea edición y ofrece reactivación

- GIVEN un identificador de unidad eliminada solicitado en edit
- WHEN la lectura activa no devuelve datos
- THEN la página MUST NOT mostrar el formulario editable como si la unidad siguiera activa
- AND MUST ofrecer una acción visible para reactivarla.

#### Scenario: Reactivación exitosa desde detail o edit

- GIVEN una unidad eliminada abierta en estado recuperable
- WHEN el usuario solicita reactivarla y el backend responde éxito
- THEN la shell MUST mostrar confirmación visible
- AND MUST redirigir a una vista coherente de la unidad reactivada preservando el contexto de retorno.

#### Scenario: Reactivación rechazada desde detail o edit

- GIVEN una unidad eliminada abierta en estado recuperable
- WHEN el backend rechaza la restauración por conflicto o inexistencia
- THEN la shell MUST mostrar feedback claro y accionable
- AND MUST seguir ofreciendo una salida segura al listado.