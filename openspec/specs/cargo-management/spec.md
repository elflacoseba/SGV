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

El sistema DEBE permitir actualizar los campos editables `Nombre`, `NivelId` y `Descripcion` de un Cargo existente. El campo `Codigo` NO DEBE ser libremente mutable.

#### Escenario: Actualización exitosa

- **DADO** un Cargo activo con identificador válido
- **CUANDO** se solicita actualizar `Nombre`, `NivelId` o `Descripcion`
- **ENTONCES** el sistema DEBE persistir los cambios y devolver el Cargo actualizado.

#### Escenario: Actualizar Cargo inexistente

- **DADO** que no existe un Cargo con el identificador proporcionado
- **CUANDO** se solicita actualizar el Cargo
- **ENTONCES** el sistema DEBE devolver error de no encontrado.

#### Escenario: Codigo no modificable

- **DADO** un Cargo existente con `Codigo` "DIR01"
- **CUANDO** se intenta cambiar el `Codigo`
- **ENTONCES** el sistema NO DEBE permitir la modificación del `Codigo`
- **Y** el endpoint de actualización NO DEBE incluir `Codigo` como campo editable.

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