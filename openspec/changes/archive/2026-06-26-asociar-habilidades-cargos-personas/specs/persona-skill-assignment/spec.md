# Delta para Asociación de Habilidades a Personas

## ADDED Requirements

### Requirement: Listar habilidades de una persona

El sistema MUST exponer las habilidades asociadas a una Persona como colección administrable y consumer-safe.

#### Scenario: Listado exitoso

- **DADO** que existe una Persona con habilidades asociadas
- **CUANDO** se consulta `/api/v1/personas/{personaId}/skills`
- **ENTONCES** el sistema DEBE devolver solo las asociaciones de esa Persona
- **Y** cada elemento DEBE incluir `skillId` y `nivelId`.

### Requirement: Asignar o actualizar habilidad de persona

El sistema MUST permitir crear o actualizar una asociación Persona-Habilidad con `nivelId` válido de `NivelesHabilidad`.

#### Scenario: Asignación exitosa

- **DADO** que existen la Persona, la Habilidad y el Nivel requeridos
- **CUANDO** se agrega o actualiza la habilidad de la Persona
- **ENTONCES** el sistema DEBE persistir la asociación con su nivel de dominio/proficiencia
- **Y** DEBE devolver la asociación guardada.

#### Scenario: Nivel inválido

- **DADO** que el `nivelId` no referencia un `NivelesHabilidad` existente
- **CUANDO** se intenta guardar la asociación
- **ENTONCES** el sistema DEBE rechazar la operación con error de validación.

### Requirement: Quitar habilidad de persona

El sistema MUST eliminar físicamente la asociación al quitar una habilidad de una Persona.

#### Scenario: Eliminación exitosa

- **DADO** que existe una asociación Persona-Habilidad
- **CUANDO** se solicita quitarla
- **ENTONCES** el sistema DEBE eliminar la fila de asociación
- **Y** la habilidad ya no DEBE aparecer en el subrecurso.
