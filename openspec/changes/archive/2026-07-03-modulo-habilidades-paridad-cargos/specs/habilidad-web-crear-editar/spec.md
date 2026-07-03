# Especificación web de create y edit de habilidades

## Propósito

Definir los flujos autenticados de alta y edición de `Habilidades` en `SGV.Web`, reutilizando el patrón de `Cargos` pero respetando la inmutabilidad de `Codigo` y el alcance real del catálogo maestro.

## Requirements

### Requirement: Acceso autenticado a create y edit de habilidades

El sistema MUST exponer páginas Razor protegidas para create y edit de `Habilidades` dentro del shell autenticado y MUST ofrecer una acción visible para volver al listado.

#### Scenario: Usuario autenticado abre create

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega a `/organizacion/habilidades/crear`
- THEN la aplicación MUST responder con un formulario vacío dentro del shell autenticado.

#### Scenario: Habilidad activa existente en edit

- GIVEN una Habilidad activa existente y un usuario autenticado
- WHEN navega a su URL de edición
- THEN la aplicación MUST mostrar el formulario prellenado con los datos actuales.

#### Scenario: Habilidad inexistente o eliminada en edit

- GIVEN un identificador que no puede consultarse como habilidad activa
- WHEN un usuario autenticado abre la pantalla de edit
- THEN la interfaz MUST mostrar un estado recuperable de no disponible
- AND MUST impedir el guardado desde esa vista.

### Requirement: Campos visibles y Codigo inmutable en edición

El formulario MUST mostrar `Codigo`, `Nombre`, `Categoria` y `Descripcion`. En create, `Codigo` MUST ser editable. En edit, `Codigo` MUST permanecer visible pero readonly o deshabilitado. La pantalla MUST NOT mostrar ni capturar un campo de nivel dentro del catálogo maestro.

#### Scenario: Create muestra campos editables

- GIVEN un usuario autenticado abre create
- WHEN la pantalla termina de cargar correctamente
- THEN la interfaz MUST mostrar `Codigo`, `Nombre`, `Categoria` y `Descripcion`
- AND MUST NOT mostrar un campo, dropdown o selector de nivel
- AND MUST permitir editar `Codigo` antes del guardado.

#### Scenario: Edit refleja la inmutabilidad de Codigo

- GIVEN una habilidad activa existente
- WHEN un usuario autenticado abre edit
- THEN la interfaz MUST mostrar `Codigo` con su valor actual
- AND MUST impedir su modificación visual durante la edición.

### Requirement: Guardado con PRG y feedback accionable

El sistema MUST aplicar PRG tras create o edit exitosos y MUST traducir validaciones, conflictos de `Codigo` activo y fallos de disponibilidad del backend a feedback claro y accionable.

#### Scenario: Create exitoso

- GIVEN datos válidos para una nueva Habilidad
- WHEN el usuario confirma create y el backend persiste la operación
- THEN la shell MUST redirigir al detail de la Habilidad creada
- AND MUST mostrar un mensaje visible de éxito.

#### Scenario: Edit exitoso

- GIVEN una Habilidad activa existente y datos válidos
- WHEN el usuario confirma edit y el backend persiste la operación
- THEN la shell MUST redirigir nuevamente a edit o detalle con confirmación visible.

#### Scenario: Conflicto por Codigo activo duplicado

- GIVEN un intento de create con `Codigo` ya usado por otra Habilidad activa
- WHEN el backend responde conflicto
- THEN la interfaz MUST mostrar un mensaje claro indicando el campo afectado
- AND MUST permitir corregir el formulario sin perder el resto de los datos.

#### Scenario: Backend no disponible durante el guardado

- GIVEN un formulario válido listo para enviarse
- WHEN create o edit falla porque el backend no está disponible
- THEN la interfaz MUST mostrar un error visible de disponibilidad
- AND MUST ofrecer una acción concreta de reintento.

## Out of scope

- No agrega edición del `Codigo` después de crear la habilidad.
- No extiende el contrato de persistencia de `skills` con datos de nivel en este cambio.
- No muestra ni persiste un nivel de habilidad en el catálogo maestro porque el modelo de dominio no tiene `NivelId` propio en la entidad `Habilidad`; el nivel vive en la asociación con un cargo o persona.
- No incluye asignaciones `habilidad↔cargo` ni `habilidad↔persona`.
