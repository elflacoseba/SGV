# Delta para cargo-management

## MODIFIED Requirements

### Requirement: Actualizar Cargo

El sistema MUST permitir actualizar los campos editables `Codigo`, `Nombre`, `NivelId` y `Descripcion` de un Cargo existente. Cuando el `Codigo` cambie, el sistema MUST aplicar la misma regla de unicidad de activos usada en create y devolver el `CargoDto` actualizado cuando la operación sea válida.
(Previously: el update solo permitía `Nombre`, `NivelId` y `Descripcion`, y `Codigo` era inmutable tras la creación.)

#### Scenario: Actualización exitosa con Codigo único

- GIVEN un Cargo activo existente y otro conjunto de Cargos activos con códigos distintos
- WHEN un usuario autenticado solicita `PUT /api/v1/cargos/{id}` con un nuevo `Codigo` único y datos válidos
- THEN el sistema MUST persistir el cambio
- AND MUST responder `200 OK` con el `CargoDto` actualizado.

#### Scenario: Actualización exitosa sin cambiar Codigo

- GIVEN un Cargo activo existente con `Codigo` vigente
- WHEN un usuario autenticado solicita actualizar solo `Nombre`, `Descripcion` o `NivelId`
- THEN el sistema MUST mantener el `Codigo` existente
- AND MUST responder `200 OK` con el `CargoDto` actualizado.

#### Scenario: Codigo requerido en update

- GIVEN un Cargo activo existente
- WHEN un usuario autenticado solicita actualizarlo con `Codigo` nulo, vacío o en blanco
- THEN el sistema MUST rechazar la operación con `400 Bad Request`
- AND MUST devolver un error de validación indicando que `Codigo` es requerido.

#### Scenario: Codigo duplicado contra otro Cargo activo

- GIVEN un Cargo activo existente con `Codigo` "DIR01" distinto del Cargo editado
- WHEN un usuario autenticado solicita actualizar otro Cargo activo usando `Codigo` "DIR01"
- THEN el sistema MUST rechazar la operación con `409 Conflict`
- AND MUST devolver un mensaje claro indicando que el `Codigo` ya está en uso por un Cargo activo.

#### Scenario: Codigo repetido de un Cargo eliminado

- GIVEN un Cargo eliminado lógicamente con `Codigo` "DIR01" y ningún Cargo activo con ese código
- WHEN un usuario autenticado solicita actualizar un Cargo activo usando `Codigo` "DIR01"
- THEN el sistema MUST permitir la operación
- AND MUST responder `200 OK` con el `CargoDto` actualizado.

#### Scenario: Actualizar Cargo inexistente

- GIVEN que no existe un Cargo activo con el identificador proporcionado
- WHEN un usuario autenticado solicita actualizar el Cargo
- THEN el sistema MUST devolver error de no encontrado.

## ADDED Requirements

### Requirement: Unicidad activa de Codigo en update

El sistema MUST aplicar en update la misma regla de unicidad de `Codigo` activo usada en create, respetando la unicidad solo entre registros activos y apoyándose en la restricción persistente vigente.

#### Scenario: Update comparte la misma regla que create

- GIVEN que create rechaza códigos duplicados entre Cargos activos
- WHEN un usuario autenticado intenta actualizar un Cargo activo con un `Codigo` que ya usa otro Cargo activo
- THEN el sistema MUST rechazar el update bajo la misma regla de unicidad activa
- AND MUST devolver un conflicto consistente con create.

#### Scenario: Update ignora registros inactivos para unicidad activa

- GIVEN que existe un Cargo inactivo con un `Codigo` determinado y no hay un activo con ese mismo valor
- WHEN un usuario autenticado actualiza un Cargo activo usando ese `Codigo`
- THEN el sistema MUST aceptar la operación
- AND MUST mantener la unicidad solo entre registros activos.
