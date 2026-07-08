# Delta para `unidad-organizativa-crud`

## MODIFIED Requirements

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
