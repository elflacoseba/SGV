# Spec: habilidad-web-crear-editar

## Purpose

Definir los flujos autenticados de alta y edición de `Habilidades` en `SGV.Web`, reutilizando el patrón de `Cargos` y permitiendo editar `Codigo` dentro del alcance real del catálogo maestro.

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

### Requirement: Edición web de Codigo de una Habilidad

La página de edición MUST permitir cambiar `Codigo` de una Habilidad activa existente y MUST enviar el valor actualizado al backend junto con el resto del formulario.

#### Scenario: Editar Codigo de una Habilidad existente

- GIVEN una Habilidad activa existente con `Codigo` vigente
- WHEN un usuario autenticado edita `Codigo` por otro valor válido y guarda el formulario
- THEN la página MUST confirmar el guardado exitoso
- AND MUST volver a mostrar la Habilidad con el nuevo `Codigo` persistido.

#### Scenario: Editar otros campos sin cambiar Codigo

- GIVEN una Habilidad activa existente con datos válidos
- WHEN un usuario autenticado guarda cambios en `Nombre`, `Categoria` o `Descripcion` sin modificar `Codigo`
- THEN la página MUST confirmar el guardado exitoso
- AND MUST volver a mostrar el mismo `Codigo` previo junto con los demás campos actualizados.

#### Scenario: Codigo inválido en edición

- GIVEN una Habilidad activa existente
- WHEN un usuario autenticado intenta guardar un `Codigo` vacío, demasiado largo o fuera del formato admitido por el formulario
- THEN la página MUST mostrar el error de validación asociado a `Codigo`
- AND MUST conservar el resto de los datos ingresados para su corrección.

#### Scenario: Codigo duplicado de otra Habilidad activa

- GIVEN una Habilidad activa existente
- AND existe otra Habilidad activa con el `Codigo` ingresado
- WHEN un usuario autenticado intenta guardar ese `Codigo` duplicado
- THEN la página MUST mostrar un error de conflicto coherente sobre `Codigo`
- AND MUST conservar el resto del formulario para permitir corregirlo.

#### Scenario: Reutilizar un Codigo liberado por baja lógica

- GIVEN una Habilidad activa en edición
- AND existe una Habilidad eliminada cuyo `Codigo` coincide con el nuevo valor ingresado
- WHEN un usuario autenticado guarda el formulario con ese `Codigo`
- THEN la página MUST aceptar el guardado
- AND MUST mostrar la Habilidad actualizada con el `Codigo` reutilizado.

### Requirement: Campos visibles y Codigo inmutable en edición

El formulario de edit MUST permitir modificar `Codigo` junto con `Nombre`, `Categoria` y `Descripcion`, y MUST NOT introducir campos de nivel en esta capability.

#### Scenario: Create muestra campos editables

- GIVEN un usuario autenticado abre create
- WHEN la pantalla termina de cargar correctamente
- THEN la interfaz MUST mostrar `Codigo`, `Nombre`, `Categoria` y `Descripcion`
- AND MUST NOT mostrar un campo, dropdown o selector de nivel
- AND MUST permitir editar `Codigo` antes del guardado.

#### Scenario: Edit muestra Codigo editable

- GIVEN una habilidad activa existente
- WHEN un usuario autenticado abre edit
- THEN la interfaz MUST mostrar `Codigo` con su valor actual
- AND MUST mostrarlo en un input editable.

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

#### Scenario: Edit exitoso con cambio de Codigo mantiene PRG

- GIVEN una Habilidad activa existente y datos válidos
- WHEN el usuario confirma edit con un nuevo `Codigo` válido y el backend persiste la operación
- THEN la shell MUST redirigir nuevamente a edit o detalle con confirmación visible
- AND MUST reflejar el `Codigo` actualizado tras la redirección.

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

- No extiende el contrato de persistencia de `skills` con datos de nivel en este cambio.
- No muestra ni persiste un nivel de habilidad en el catálogo maestro porque el modelo de dominio no tiene `NivelId` propio en la entidad `Habilidad`; el nivel vive en la asociación con un cargo o persona.
- No incluye asignaciones `habilidad↔cargo` ni `habilidad↔persona`.
