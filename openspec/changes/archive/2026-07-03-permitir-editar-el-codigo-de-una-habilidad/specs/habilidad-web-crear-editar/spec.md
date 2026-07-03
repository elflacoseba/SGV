# Spec Delta: habilidad-web-crear-editar

## Purpose
The authenticated SGV.Web flow creates and edits Habilidades while allowing `Codigo` changes during edit.

## Delta

## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Campos visibles y Codigo inmutable en edición

El formulario de edit MUST permitir modificar `Codigo` junto con `Nombre`, `Categoria` y `Descripcion`, y MUST NOT introducir campos de nivel en esta capability.

- **Cambio**: En edit, `Codigo` deja de ser readonly o deshabilitado y pasa a ser editable con el mismo formulario visible de `Codigo`, `Nombre`, `Categoria` y `Descripcion`; la pantalla sigue sin mostrar ni capturar un campo de nivel.

#### Scenario: Edit muestra Codigo editable

- GIVEN una Habilidad activa existente
- WHEN un usuario autenticado abre la pantalla de edit
- THEN la interfaz MUST mostrar `Codigo` con su valor actual en un input editable
- AND MUST seguir mostrando `Nombre`, `Categoria` y `Descripcion` sin agregar un campo de nivel.

### Requirement: Guardado con PRG y feedback accionable

El flujo de edit MUST conservar PRG en éxito y MUST devolver feedback accionable cuando el rechazo proviene de validaciones o conflictos de `Codigo` activo.

- **Cambio**: Los conflictos de `Codigo` activo y las validaciones de `Codigo` pasan a aplicar también al flujo de edit, preservando PRG en éxito y manteniendo el formulario corregible cuando el guardado es rechazado.

#### Scenario: Edit exitoso con cambio de Codigo mantiene PRG

- GIVEN una Habilidad activa existente y datos válidos
- WHEN el usuario confirma edit con un nuevo `Codigo` válido y el backend persiste la operación
- THEN la shell MUST redirigir nuevamente a edit o detalle con confirmación visible
- AND MUST reflejar el `Codigo` actualizado tras la redirección.

## REMOVED Requirements
