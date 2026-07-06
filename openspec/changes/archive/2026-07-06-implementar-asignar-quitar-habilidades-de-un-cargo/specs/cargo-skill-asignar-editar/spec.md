# Especificación de asignar, editar y quitar habilidades de un cargo

## Purpose

Definir el comportamiento observable del subrecurso `Cargo↔Habilidad` para asignar, actualizar y quitar una habilidad requerida de un `Cargo` sin introducir soft delete ni rediseñar la asociativa existente.

## Requirements

### Requirement 1: Upsert completo del vínculo `CargoHabilidad`

El sistema MUST permitir que `PUT /api/v1/cargos/{cargoId}/skills/{skillId}` cree o actualice una asociación activa usando los campos editables `NivelRequeridoId`, `Ponderacion` y `EsObligatoria` del vínculo para el `skillId` indicado en la ruta.

#### Scenario: Asignar una habilidad a un cargo

- GIVEN un `Cargo`, una `Habilidad` y un `NivelHabilidad` existentes
- WHEN un `Administrador` envía un `PUT` válido con `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`
- THEN la API MUST persistir la asociación activa para ese par `cargoId/skillId`
- AND la respuesta MUST reflejar los valores guardados del vínculo.

#### Scenario: Actualizar una asociación existente de forma idempotente

- GIVEN un `Cargo` que ya tiene asociada esa `Habilidad`
- WHEN un `Administrador` reenvía `PUT` para el mismo par con nuevos valores de `NivelRequeridoId`, `Ponderacion` o `EsObligatoria`
- THEN el sistema MUST dejar una única asociación activa para ese par
- AND la lectura posterior MUST devolver los últimos valores enviados.

### Requirement 2: Baja física del vínculo

El sistema MUST quitar la asociación mediante `DELETE /api/v1/cargos/{cargoId}/skills/{skillId}` con borrado físico, sin semántica de soft delete ni reactivación implícita.

#### Scenario: Quitar una habilidad del cargo

- GIVEN una asociación activa existente entre `Cargo` y `Habilidad`
- WHEN un `Administrador` ejecuta `DELETE`
- THEN la API MUST responder éxito sin devolver el vínculo eliminado
- AND una consulta posterior del mismo par MUST indicar que la asociación ya no existe.

#### Scenario: Reasignar después de una baja previa

- GIVEN que una asociación del mismo par fue quitada físicamente con `DELETE`
- WHEN un `Administrador` vuelve a ejecutar `PUT` para ese par
- THEN el sistema MUST crear una nueva asociación activa
- AND MUST NOT depender de resurrección de una fila oculta o eliminada lógicamente.

### Requirement 3: Rechazo por referencias inexistentes

El sistema MUST rechazar mutaciones cuando la referencia dueña o alguna referencia del vínculo no exista.

#### Scenario: Cargo inexistente

- GIVEN un `cargoId` inexistente
- WHEN un `Administrador` intenta asignar o quitar una habilidad
- THEN la API MUST responder `404 Not Found`
- AND MUST NOT persistir cambios.

#### Scenario: Habilidad inexistente

- GIVEN un `skillId` inexistente
- WHEN un `Administrador` intenta asignarla a un cargo existente
- THEN la API MUST responder `404 Not Found`
- AND MUST NOT persistir cambios.

#### Scenario: Nivel requerido inexistente

- GIVEN un `NivelRequeridoId` inexistente en el payload
- WHEN un `Administrador` ejecuta `PUT`
- THEN la API MUST responder `400 Bad Request`
- AND MUST exponer un error de validación legible.

### Requirement 4: Validación de `Ponderacion`

El sistema MUST tratar `Ponderacion` como un decimal positivo con hasta dos decimales para el vínculo y MUST rechazar valores fuera del rango operativo acordado para la UI.

#### Scenario: Ponderación nula, cero, negativa o mayor a `100.00`

- GIVEN un payload con `Ponderacion` inválida
- WHEN un `Administrador` ejecuta `PUT`
- THEN la API MUST responder `400 Bad Request`
- AND MUST identificar `Ponderacion` como campo inválido.

#### Scenario: Ponderación con precisión inválida

- GIVEN un payload con más de dos decimales en `Ponderacion`
- WHEN un `Administrador` ejecuta `PUT`
- THEN el sistema MUST rechazar la solicitud
- AND MUST NOT redondear ni truncar silenciosamente el valor enviado.

> **Nota**: este cambio mantiene borrado físico en `CargoHabilidad`; por lo tanto no existe un caso de conflicto por “soft delete previo” y la semántica definida es recreación explícita tras la baja.
