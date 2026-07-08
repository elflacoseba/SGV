# Unidad Organizativa CRUD Specification

## Purpose

Define managed create, update, parent-change, read, soft-delete, and reactivate behavior for organizational units.

## Requirements

### Requirement: Manage Organizational Units

El sistema MUST permitir crear, actualizar, leer, cambiar padre y baja lógica con contratos estables. `POST` MUST aceptar `codigo`; `PUT` MUST NOT exponer `codigo` en `ActualizarUnidadOrganizativaRequest`. Un `codigo` extra en JSON de update queda fuera de contrato y MUST NOT cambiar el valor persistido. Lecturas mantienen `codigo`, `tipoUnidadId` y `tipoUnidadNombre`; no `tipoUnidad`.
(Previously: update permitía cambiar `codigo`.)

#### Scenario: Create organizational unit

- GIVEN datos válidos con código activo único y `tipoUnidadId` existente
- WHEN un cliente crea la unidad
- THEN el sistema MUST persistirla con ese `codigo` y tipo
- AND MUST devolver el contrato creado sin `tipoUnidad`.

#### Scenario: Update organizational unit

- GIVEN una unidad activa existe con `Codigo = "RECT"`
- WHEN un cliente hace `PUT` con campos editables válidos y sin `codigo`
- THEN el sistema MUST persistir los cambios editables
- AND MUST preservar el identificador y `Codigo = "RECT"`.
- AND un `codigo` extra en JSON MUST NOT modificar el código almacenado.

#### Scenario: Read organizational unit

- GIVEN una unidad activa con tipo `Facultad`
- WHEN un cliente la lee
- THEN la respuesta MUST incluir `tipoUnidadId` y `tipoUnidadNombre = "Facultad"`
- AND MUST NOT incluir `tipoUnidad`.

#### Scenario: Soft-delete organizational unit

- GIVEN una unidad activa existe
- WHEN un cliente la elimina
- THEN el sistema MUST marcarla inactiva o eliminada
- AND MUST excluirla de lecturas activas.

### Requirement: Validate Organizational Unit Writes

El sistema MUST rechazar escrituras inválidas antes de confirmar cambios. `codigo` MUST ser requerido/no vacío y máx. 50 solo en create. Update MUST validar solo campos editables: `nombre`, `descripcion`, `tipoUnidadOrganizativaId`, `unidadPadreId`, vigencias y estado. Update MUST NOT validar `codigo` ni consultar duplicados por un valor enviado fuera de contrato. Reactivación MUST seguir validando conflicto por código activo.
(Previously: create y update podían validar/conflictar por `codigo`.)

#### Scenario: Rechazar código activo duplicado

- GIVEN una unidad activa ya usa un código
- WHEN otro create usa ese código o se reactiva una eliminada con ese código
- THEN el sistema MUST rechazar con conflicto predecible
- AND update MUST NOT cambiar ni revalidar `Codigo`.

#### Scenario: Rechazar jerarquía inválida

- GIVEN una unidad existe en una jerarquía
- WHEN el cliente define como padre a sí misma o a una descendiente
- THEN el sistema MUST rechazar la operación
- AND MUST NOT cambiar la jerarquía.

#### Scenario: Rechazar create con tipo inexistente

- GIVEN un Guid inexistente en `TipoUnidadOrganizativa`
- WHEN el cliente hace `POST` con ese `tipoUnidadId`
- THEN la respuesta MUST ser `400 Bad Request`
- AND no se persiste entidad.

#### Scenario: Rechazar create sin tipo

- GIVEN un body sin `tipoUnidadId`
- WHEN el cliente hace `POST`
- THEN la respuesta MUST ser `400 Bad Request`
- AND MUST incluir error `required` para `tipoUnidadId`.

#### Scenario: Rechazar update con tipo inexistente

- GIVEN una unidad existente
- WHEN el cliente hace `PUT` con `tipoUnidadId` inexistente
- THEN la respuesta MUST ser `400 Bad Request`
- AND la unidad MUST NOT modificarse.

#### Scenario: Rechazar create con shape inválido

- GIVEN create con `codigo` vacío, `nombre` largo y fechas inválidas
- WHEN el cliente lo envía
- THEN MUST devolver errores por campo
- AND MUST NOT consultar reglas de negocio ni persistir.

#### Scenario: Rechazar update con shape inválido

- GIVEN update con `descripcion` larga o `tipoUnidadId` vacío enviado
- WHEN el cliente lo envía
- THEN MUST devolver errores por campos editables
- AND MUST NOT modificar la unidad.

### Requirement: Exponer errores de validación por campo

El sistema MUST exponer errores de `CrearUnidadOrganizativaRequest` y `ActualizarUnidadOrganizativaRequest` por campo mediante `ValidationProblemDetails` o equivalente. En update, `codigo` MUST NOT aparecer como campo validable porque no pertenece al request.
(Previously: update podía devolver errores por `codigo`.)

#### Scenario: Responder errores por campo

- GIVEN create con `codigo`/`nombre` vacíos o update con campo editable inválido
- WHEN el cliente envía el request
- THEN la respuesta MUST ser `400 Bad Request` con `errors[field]`
- AND update MUST NOT incluir errores para `codigo`.

#### Scenario: No mezclar errores de validación con conflictos de negocio

- GIVEN un request de shape válida pero conflicto por código en create o reactivación
- WHEN el cliente lo envía
- THEN MUST devolver el error de negocio existente
- AND MUST NOT reportarlo como error de shape.

### Requirement: Mantener frontera de validación

El sistema MUST usar FluentValidation para validaciones de entrada/aplicación de create y update. El dominio MUST conservar invariantes esenciales; el servicio MUST conservar reglas con repositorio: duplicados, existencia de tipo/padre y ciclos.

#### Scenario: Request básico inválido no consulta reglas de negocio

- **GIVEN** un request con `tipoUnidadOrganizativaId` vacío o fechas inválidas
- **WHEN** el sistema valida el request
- **THEN** MUST rechazarlo antes de consultar duplicados, tipo, padre o ciclos
- **AND** MUST NOT persistir cambios.

#### Scenario: Dominio sigue protegiendo invariantes

- **GIVEN** una ruta interna omite validación de request
- **WHEN** construye una unidad con estado inválido
- **THEN** el dominio MUST rechazar la invariante.

### Requirement: Resumen legible de unidad padre en lecturas

El sistema MUST enriquecer `UnidadOrganizativaDto` con `unidadPadreCodigo` y `unidadPadreNombre` para respuestas de lectura, manteniendo `unidadPadreId` como referencia estable y sin exigir consultas adicionales para mostrar detalle o edición web.

#### Scenario: Lectura de unidad con padre

- GIVEN una unidad organizativa activa con padre `RECT` / `Rectorado`
- WHEN un cliente consulta la unidad por id o dentro de `consulta`
- THEN la respuesta MUST incluir `unidadPadreId`
- AND MUST incluir `unidadPadreCodigo = "RECT"` y `unidadPadreNombre = "Rectorado"`.

#### Scenario: Lectura de unidad raíz

- GIVEN una unidad organizativa activa sin padre
- WHEN un cliente consulta la unidad por id o dentro de `consulta`
- THEN la respuesta MUST mantener `unidadPadreId` nulo
- AND `unidadPadreCodigo` y `unidadPadreNombre` MUST ser nulos.

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

### Requirement: Consulta segmentada de unidades organizativas por estado

El sistema MUST permitir que el contrato de consulta de unidades organizativas solicite exactamente uno de dos segmentos: `activas` o `eliminadas`. La consulta MUST devolver activas por defecto, MUST devolver solo eliminadas cuando se solicite esa vista y MUST NOT mezclar ambos conjuntos en una misma respuesta.

#### Scenario: Consulta por defecto devuelve activas

- GIVEN unidades organizativas activas y eliminadas en persistencia
- WHEN un cliente consulta el listado sin indicar vista de eliminadas
- THEN el sistema MUST devolver solo unidades activas
- AND MUST excluir las eliminadas de la respuesta.

#### Scenario: Consulta explícita de eliminadas

- GIVEN unidades organizativas activas y eliminadas en persistencia
- WHEN un cliente consulta el listado solicitando la vista de eliminadas
- THEN el sistema MUST devolver solo unidades eliminadas
- AND MUST excluir las activas de la respuesta.

#### Scenario: Segmentos de lectura no se mezclan

- GIVEN unidades organizativas en ambos estados
- WHEN un cliente consume el contrato de consulta
- THEN cada respuesta MUST corresponder a un único segmento de estado
- AND MUST preservar el contrato de reactivación existente como operación separada.
