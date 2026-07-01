# Especificación de Gestión de Cargos

## Propósito

Gestión del catálogo maestro de Cargos: crear, consultar, actualizar campos editables, desactivar y reactivar, con `Codigo` estable e inmutable tras la creación.

## Requisitos

### Requisito: Crear Cargo

El sistema DEBE permitir crear un Cargo proporcionando `Codigo` (identificador estable), `Nombre`, `NivelId` y opcionalmente `Descripcion`.

#### Escenario: Creación exitosa

- **DADO** que no existe un Cargo activo con el `Codigo` proporcionado
- **Y** el `NivelId` referencia un NivelCargo válido
- **CUANDO** se solicita crear un Cargo
- **ENTONCES** el sistema DEBE persistir el Cargo con estado activo
- **Y** devolver los datos del Cargo creado.

#### Escenario: Codigo duplicado en Cargo activo

- **DADO** que existe un Cargo activo con el `Codigo` "DIR01"
- **CUANDO** se solicita crear otro Cargo con `Codigo` "DIR01"
- **ENTONCES** el sistema DEBE rechazar la operación
- **Y** devolver un error de conflicto indicando que el `Codigo` ya está en uso.

#### Escenario: NivelId inexistente

- **DADO** que no existe un NivelCargo con el `NivelId` proporcionado
- **CUANDO** se solicita crear un Cargo con ese `NivelId`
- **ENTONCES** el sistema DEBE rechazar la operación con error de validación.

### Requisito: Consultar Cargos

El sistema DEBE mantener los endpoints de lectura existentes y DEBE excluir los Cargos desactivados de las consultas activas por defecto.

#### Escenario: Listar Cargos activos

- **DADO** que existen Cargos activos y desactivados
- **CUANDO** se solicita la lista de Cargos
- **ENTONCES** el sistema DEBE devolver solo los Cargos activos.

#### Escenario: Obtener Cargo por identificador

- **DADO** que existe un Cargo activo con un identificador dado
- **CUANDO** se solicita el Cargo por su identificador
- **ENTONCES** el sistema DEBE devolver los datos del Cargo.

### Requisito: Actualizar Cargo

El sistema DEBE permitir actualizar los campos editables `Codigo`, `Nombre`, `NivelId` y `Descripcion` de un Cargo existente. Cuando el `Codigo` cambie, el sistema DEBE aplicar la misma regla de unicidad de activos usada en create y devolver el `CargoDto` actualizado cuando la operación sea válida.

#### Escenario: Actualización exitosa con Codigo único

- **DADO** un Cargo activo existente y otro conjunto de Cargos activos con códigos distintos
- **CUANDO** un usuario autenticado solicita `PUT /api/v1/cargos/{id}` con un nuevo `Codigo` único y datos válidos
- **ENTONCES** el sistema DEBE persistir el cambio
- **Y** DEBE responder `200 OK` con el `CargoDto` actualizado.

#### Escenario: Actualización exitosa sin cambiar Codigo

- **DADO** un Cargo activo existente con `Codigo` vigente
- **CUANDO** un usuario autenticado solicita actualizar solo `Nombre`, `Descripcion` o `NivelId`
- **ENTONCES** el sistema DEBE mantener el `Codigo` existente
- **Y** DEBE responder `200 OK` con el `CargoDto` actualizado.

#### Escenario: Codigo requerido en update

- **DADO** un Cargo activo existente
- **CUANDO** un usuario autenticado solicita actualizarlo con `Codigo` nulo, vacío o en blanco
- **ENTONCES** el sistema DEBE rechazar la operación con `400 Bad Request`
- **Y** DEBE devolver un error de validación indicando que `Codigo` es requerido.

#### Escenario: Codigo duplicado contra otro Cargo activo

- **DADO** un Cargo activo existente con `Codigo` "DIR01" distinto del Cargo editado
- **CUANDO** un usuario autenticado solicita actualizar otro Cargo activo usando `Codigo` "DIR01"
- **ENTONCES** el sistema DEBE rechazar la operación con `409 Conflict`
- **Y** DEBE devolver un mensaje claro indicando que el `Codigo` ya está en uso por un Cargo activo.

#### Escenario: Codigo repetido de un Cargo eliminado

- **DADO** un Cargo eliminado lógicamente con `Codigo` "DIR01" y ningún Cargo activo con ese código
- **CUANDO** un usuario autenticado solicita actualizar un Cargo activo usando `Codigo` "DIR01"
- **ENTONCES** el sistema DEBE permitir la operación
- **Y** DEBE responder `200 OK` con el `CargoDto` actualizado.

#### Escenario: Actualizar Cargo inexistente

- **DADO** que no existe un Cargo activo con el identificador proporcionado
- **CUANDO** un usuario autenticado solicita actualizar el Cargo
- **ENTONCES** el sistema DEBE devolver error de no encontrado.

### Requisito: Unicidad activa de Codigo en update

El sistema DEBE aplicar en update la misma regla de unicidad de `Codigo` activo usada en create, respetando la unicidad solo entre registros activos y apoyándose en la restricción persistente vigente.

#### Escenario: Update comparte la misma regla que create

- **DADO** que create rechaza códigos duplicados entre Cargos activos
- **CUANDO** un usuario autenticado intenta actualizar un Cargo activo con un `Codigo` que ya usa otro Cargo activo
- **ENTONCES** el sistema DEBE rechazar el update bajo la misma regla de unicidad activa
- **Y** DEBE devolver un conflicto consistente con create.

#### Escenario: Update ignora registros inactivos para unicidad activa

- **DADO** que existe un Cargo inactivo con un `Codigo` determinado y no hay un activo con ese mismo valor
- **CUANDO** un usuario autenticado actualiza un Cargo activo usando ese `Codigo`
- **ENTONCES** el sistema DEBE aceptar la operación
- **Y** DEBE mantener la unicidad solo entre registros activos.

### Requisito: Desactivar Cargo

El sistema DEBE permitir desactivar un Cargo mediante baja lógica. La desactivación NO DEBE eliminar físicamente el registro.

#### Escenario: Desactivación exitosa

- **DADO** un Cargo activo
- **CUANDO** se solicita desactivar el Cargo
- **ENTONCES** el sistema DEBE marcar el Cargo como inactivo
- **Y** el Cargo DEBE estar excluido de las consultas activas
- **Y** el registro DEBE persistir en la base de datos.

#### Escenario: Desactivar Cargo inexistente

- **DADO** que no existe un Cargo con el identificador proporcionado
- **CUANDO** se solicita desactivar el Cargo
- **ENTONCES** el sistema DEBE devolver error de no encontrado.

### Requisito: Reactivar Cargo

El sistema DEBE permitir reactivar un Cargo previamente desactivado, conservando el mismo `Codigo`.

#### Escenario: Reactivación exitosa

- **DADO** un Cargo desactivado con `Codigo` "DIR01"
- **Y** no existe otro Cargo activo con `Codigo` "DIR01"
- **CUANDO** se solicita reactivar el Cargo
- **ENTONCES** el sistema DEBE marcar el Cargo como activo
- **Y** el Cargo DEBE conservar su `Codigo` original.

#### Escenario: Reactivar con Codigo conflictivo

- **DADO** un Cargo desactivado con `Codigo` "DIR01"
- **Y** existe un Cargo activo con `Codigo` "DIR01"
- **CUANDO** se solicita reactivar el Cargo desactivado
- **ENTONCES** el sistema DEBE rechazar la operación con error de conflicto.

#### Escenario: Reactivar Cargo inexistente

- **DADO** que no existe un Cargo con el identificador proporcionado
- **CUANDO** se solicita reactivar
- **ENTONCES** el sistema DEBE devolver error de no encontrado.

### Requisito: Contrato de Respuesta Cargo

Las respuestas de la API DEBEN exponer campos consumer-safe y NO DEBEN incluir campos internos de auditoría ni seguimiento de persistencia.

#### Escenario: Respuesta consumer-safe

- **DADO** un Cargo persistido con datos completos
- **CUANDO** se consulta el Cargo por la API
- **ENTONCES** la respuesta DEBE contener `id`, `codigo`, `nombre`, `nivelId`, `nivelNombre` (denormalizado), `descripcion`
- **Y** NO DEBE incluir campos de auditoría como `createdAt`, `isDeleted` o `isActive`.
