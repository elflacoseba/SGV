# Delta para Asociación de Habilidades a Cargos

## ADDED Requirements

### Requirement: Listar habilidades de un cargo

El sistema MUST exponer las habilidades asociadas a un Cargo como colección administrable y consumer-safe.

#### Scenario: Listado exitoso

- **DADO** que existe un Cargo con habilidades asociadas
- **CUANDO** se consulta `/api/v1/cargos/{cargoId}/skills`
- **ENTONCES** el sistema DEBE devolver solo las asociaciones de ese Cargo
- **Y** cada elemento DEBE incluir `skillId` y `nivelId`.

### Requirement: Asignar o actualizar habilidad de cargo

El sistema MUST permitir crear o actualizar una asociación Cargo-Habilidad con `nivelId` válido de `NivelesHabilidad`.

#### Scenario: Asignación exitosa

- **DADO** que existen el Cargo, la Habilidad y el Nivel requeridos
- **CUANDO** se agrega o actualiza la habilidad del Cargo
- **ENTONCES** el sistema DEBE persistir la asociación con su nivel requerido
- **Y** DEBE devolver la asociación guardada.

#### Scenario: Nivel inválido

- **DADO** que el `nivelId` no referencia un `NivelesHabilidad` existente
- **CUANDO** se intenta guardar la asociación
- **ENTONCES** el sistema DEBE rechazar la operación con error de validación.

### Requirement: Quitar habilidad de cargo

El sistema MUST eliminar físicamente la asociación al quitar una habilidad de un Cargo.

#### Scenario: Eliminación exitosa

- **DADO** que existe una asociación Cargo-Habilidad
- **CUANDO** se solicita quitarla
- **ENTONCES** el sistema DEBE eliminar la fila de asociación
- **Y** la habilidad ya no DEBE aparecer en el subrecurso.
