# Delta para `unidad-organizativa-web-detalle-edicion`

## MODIFIED Requirements

### Requirement: Datos visibles y editables de la unidad organizativa

El sistema MUST mostrar `codigo`, `nombre`, `descripcion`, vigencias, tipo y padre. En create, `codigo` MUST ser editable y enviarse al alta. En detail, todos los datos MUST verse solo lectura. En edit, `codigo` MUST mostrarse solo lectura o equivalente no editable; el submit MUST enviar solo campos editables y MUST preservar el código original.
(Previously: edit mostraba `codigo` como editable.)

#### Scenario: Create carga catálogos necesarios

- GIVEN un usuario autenticado abre create
- WHEN la pantalla termina de cargar
- THEN la interfaz MUST mostrar un formulario vacío con `codigo` editable
- AND MUST ofrecer opciones seleccionables para tipo y unidad padre.

#### Scenario: Detail o edit muestran el padre actual

- GIVEN una unidad existente con `codigo` y padre asignado
- WHEN un usuario abre detail o edit
- THEN la interfaz MUST mostrar el código y el padre actual de forma legible
- AND en edit MUST impedir cambiar `codigo` y permitir reemplazar o quitar el padre.
