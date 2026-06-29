# Delta for unidad-organizativa-web-detalle-edicion

## ADDED Requirements

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
