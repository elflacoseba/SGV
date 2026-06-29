# Unidad Organizativa CRUD Specification

## Purpose

Define managed create, update, parent-change, read, soft-delete, and reactivate behavior for organizational units.

## Requirements

### Requirement: Manage Organizational Units

The system MUST allow clients to create, update, read, re-parent, and soft-delete organizational units through stable API contracts. The legacy `tipoUnidad: string` field is removed from the request body and from the response DTO. On writes, a `tipoUnidadId: Guid` referencing an existing `TipoUnidadOrganizativa` row is required. On reads, the response includes both the `tipoUnidadId` and a denormalized `tipoUnidadNombre`.

#### Scenario: Create organizational unit

- **GIVEN** valid organizational unit data with a unique active code AND a `tipoUnidadId` referencing an existing `TipoUnidadOrganizativa`
- **WHEN** a client creates the unit
- **THEN** the system MUST persist it with that foreign key
- **AND** return the created unit contract
- **AND** the response MUST include `tipoUnidadId: <Guid>` and `tipoUnidadNombre: <string>` (denormalized)
- **AND** the response MUST NOT include a `tipoUnidad` string field.

#### Scenario: Update organizational unit

- **GIVEN** an active organizational unit exists
- **WHEN** a client updates editable fields with valid data, optionally including a new `tipoUnidadId`
- **THEN** the system MUST persist the changes
- **AND** preserve its identifier
- **AND** if a `tipoUnidadId` was supplied, the system MUST validate it against the catalog and update the foreign key before commit.

#### Scenario: Read organizational unit

- **GIVEN** an active organizational unit exists with a foreign key to a `TipoUnidadOrganizativa` named `Facultad`
- **WHEN** a client reads the unit
- **THEN** the response is `{ ..., "tipoUnidadId": "<Guid>", "tipoUnidadNombre": "Facultad" }`
- **AND** the response MUST NOT include a `tipoUnidad` string field.

#### Scenario: Soft-delete organizational unit

- **GIVEN** an active organizational unit exists
- **WHEN** a client deletes the unit
- **THEN** the system MUST mark it inactive or deleted
- **AND** exclude it from active read results.

### Requirement: Validate Organizational Unit Writes

El sistema MUST rechazar escrituras inválidas antes de confirmar cambios. FluentValidation MUST validar shape/input antes de reglas con repositorio: `codigo` requerido/no vacío y máx. 50; `nombre` requerido/no vacío y máx. 200; `descripcion` opcional máx. 1000; `tipoUnidadOrganizativaId` requerido/no vacío en create y no vacío si se envía en update; `vigenteHasta` no anterior a `vigenteDesde`. Además de duplicados y jerarquía inválida, el sistema MUST rechazar `tipoUnidadId` inexistente. Una escritura rechazada MUST NOT modificar la base.

#### Scenario: Rechazar código activo duplicado

- **GIVEN** una unidad activa ya usa un código
- **WHEN** el cliente crea o actualiza otra unidad activa con ese código
- **THEN** el sistema MUST rechazarla con error de conflicto predecible.

#### Scenario: Rechazar jerarquía inválida

- **GIVEN** una unidad existe en una jerarquía
- **WHEN** el cliente define como padre a sí misma o a una descendiente
- **THEN** el sistema MUST rechazar la operación
- **AND** MUST NOT cambiar la jerarquía.

#### Scenario: Rechazar create con tipo inexistente

- **GIVEN** un Guid inexistente en `TipoUnidadOrganizativa`
- **WHEN** el cliente hace `POST` con `tipoUnidadId: <random>`
- **THEN** la respuesta MUST ser `400 Bad Request`
- **AND** MUST indicar que el tipo referenciado no existe
- **AND** no se persiste entidad.

#### Scenario: Rechazar create sin tipo

- **GIVEN** un body sin `tipoUnidadId`
- **WHEN** el cliente hace `POST`
- **THEN** la respuesta MUST ser `400 Bad Request`
- **AND** MUST incluir error `required` para `tipoUnidadId`
- **AND** no se persiste entidad.

#### Scenario: Rechazar update con tipo inexistente

- **GIVEN** una unidad existente
- **WHEN** el cliente hace `PUT` con `tipoUnidadId` inexistente
- **THEN** la respuesta MUST ser `400 Bad Request`
- **AND** la unidad MUST NOT modificarse.

#### Scenario: Rechazar create con shape inválido

- **GIVEN** create con `codigo` vacío, `nombre` largo y fechas inválidas
- **WHEN** el cliente lo envía
- **THEN** MUST devolver errores por campo
- **AND** MUST NOT consultar repositorios de negocio ni persistir.

#### Scenario: Rechazar update con shape inválido

- **GIVEN** update con `descripcion` larga o `tipoUnidadId` vacío enviado
- **WHEN** el cliente lo envía
- **THEN** MUST devolver errores por campo
- **AND** MUST NOT modificar la unidad.

### Requirement: Exponer errores de validación por campo

El sistema MUST exponer errores de `CrearUnidadOrganizativaRequest` y `ActualizarUnidadOrganizativaRequest` por campo mediante `ValidationProblemDetails` o equivalente con `errors[field]`.

#### Scenario: Responder errores por campo

- **GIVEN** un request con `codigo` vacío y `nombre` vacío
- **WHEN** el cliente envía create o update
- **THEN** la respuesta MUST ser `400 Bad Request`
- **AND** MUST incluir errores para `codigo` y `nombre`.

#### Scenario: No mezclar errores de validación con conflictos de negocio

- **GIVEN** un request con forma válida pero código activo duplicado
- **WHEN** el cliente envía create o update
- **THEN** MUST devolver el error de negocio existente
- **AND** MUST NOT reportarlo como error de shape.

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
