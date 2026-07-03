# Spec Delta: habilidad-management

## Purpose
The skill management capability updates existing Habilidades while allowing `Codigo` changes under active-uniqueness rules.

## Delta

## ADDED Requirements

### Requirement: Edición de Codigo con unicidad activa

La actualización de una Habilidad MUST aplicar el nuevo `Codigo` cuando se provee un valor válido y MUST rechazarlo con conflicto cuando colisiona con otra Habilidad activa.

#### Scenario: Update con Codigo de otra Habilidad activa

- GIVEN una Habilidad activa existente
- AND existe otra Habilidad activa con el `Codigo` solicitado
- WHEN se intenta actualizar la primera Habilidad con ese `Codigo`
- THEN el sistema MUST rechazar la operación con conflicto
- AND MUST conservar sin cambios el estado persistido de la Habilidad editada.

#### Scenario: Update con el mismo Codigo actual

- GIVEN una Habilidad activa existente con un `Codigo` vigente
- WHEN se solicita actualizarla enviando ese mismo `Codigo` junto con otros cambios válidos o sin cambios adicionales
- THEN el sistema MUST aceptar la operación
- AND MUST mantener el mismo `Codigo` sin tratarlo como duplicado.

#### Scenario: Update con Codigo de una Habilidad eliminada

- GIVEN una Habilidad activa existente
- AND existe una Habilidad eliminada con el `Codigo` solicitado
- WHEN se actualiza la Habilidad activa con ese `Codigo`
- THEN el sistema MUST aceptar la operación
- AND MUST persistir el nuevo `Codigo` porque la unicidad solo aplica a registros activos.

## MODIFIED Requirements

### Requirement: Actualizar Habilidad

La operación de update MUST aceptar `Codigo` como campo editable y MUST aplicar las mismas reglas de shape y unicidad activa que el alta.

- **Cambio**: La actualización deja de limitarse a `Nombre`, `Categoria` y `Descripcion`; ahora también acepta `Codigo`, lo valida con el mismo shape requerido en create y lo persiste cuando no viola la unicidad entre Habilidades activas.

El sistema MUST permitir actualizar `Codigo`, `Nombre`, `Categoria` y `Descripcion` de una Habilidad existente. `Codigo` MUST conservar las mismas reglas de shape que en create y MUST seguir siendo único entre habilidades activas.

#### Scenario: Actualización exitosa con cambio de Codigo

- GIVEN una Habilidad activa existente
- WHEN se actualiza con un `Codigo` válido que no pertenece a otra Habilidad activa y con el resto de los campos válidos
- THEN el sistema MUST persistir los cambios
- AND MUST devolver la Habilidad actualizada con el nuevo `Codigo`.

#### Scenario: Actualización exitosa sin cambiar Codigo

- GIVEN una Habilidad activa existente
- WHEN se actualizan `Nombre`, `Categoria` o `Descripcion` manteniendo el mismo `Codigo`
- THEN el sistema MUST persistir los demás cambios
- AND MUST conservar el `Codigo` existente sin alteraciones.

#### Scenario: Codigo inválido en update

- GIVEN una Habilidad activa existente
- WHEN se solicita actualizarla con `Codigo` vacío, demasiado largo o fuera del formato admitido por la regla vigente
- THEN el sistema MUST rechazar la operación por validación
- AND MUST NOT persistir cambios parciales de la actualización.

## REMOVED Requirements
