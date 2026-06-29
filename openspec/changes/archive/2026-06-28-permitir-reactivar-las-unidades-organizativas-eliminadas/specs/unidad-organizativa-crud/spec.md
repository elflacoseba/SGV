# Delta for unidad-organizativa-crud

## ADDED Requirements

### Requirement: Reactivación de unidades organizativas

El sistema MUST permitir reactivar una unidad organizativa eliminada mediante el contrato existente `PATCH /api/v1/unidades-organizativas/{id}/reactivar`. La reactivación MUST restaurar la visibilidad en consultas activas solo si no existe conflicto de código activo y, cuando la unidad tenga padre, ese padre sigue activo.

#### Scenario: Reactivación exitosa

- GIVEN una unidad organizativa eliminada y sin conflictos activos
- WHEN un cliente solicita su reactivación
- THEN el sistema MUST restaurar su estado activo
- AND MUST devolver el contrato actualizado de la unidad.

#### Scenario: Conflicto por código activo duplicado

- GIVEN una unidad organizativa eliminada cuyo `Codigo` ya está en uso por otra unidad activa
- WHEN un cliente solicita reactivarla
- THEN el sistema MUST rechazar la operación con conflicto predecible
- AND MUST mantener la unidad eliminada.

#### Scenario: Conflicto por padre inactivo o eliminado

- GIVEN una unidad organizativa eliminada con `UnidadPadreId` asignado y un padre inactivo o eliminado
- WHEN un cliente solicita reactivarla
- THEN el sistema MUST rechazar la operación con conflicto predecible
- AND MUST mantener la unidad eliminada.

#### Scenario: Unidad inexistente para reactivar

- GIVEN un identificador sin unidad organizativa asociada
- WHEN un cliente solicita reactivarlo
- THEN el sistema MUST responder que la unidad no existe.
