# Delta for unidad-organizativa-crud

## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Validate Organizational Unit Writes

El sistema MUST rechazar escrituras inválidas antes de confirmar cambios. FluentValidation MUST validar shape/input antes de reglas con repositorio: `codigo` requerido/no vacío y máx. 50; `nombre` requerido/no vacío y máx. 200; `descripcion` opcional máx. 1000; `tipoUnidadOrganizativaId` requerido/no vacío en create y no vacío si se envía en update; `vigenteHasta` no anterior a `vigenteDesde`. Además de duplicados y jerarquía inválida, el sistema MUST rechazar `tipoUnidadId` inexistente. Una escritura rechazada MUST NOT modificar la base.
(Previously: validaba duplicados, jerarquía y tipo inexistente/faltante sin FluentValidation, errores por campo ni short-circuit.)

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
