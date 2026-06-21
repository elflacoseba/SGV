# Delta para Base de Datos SGV

## MODIFIED Requirements

### Requirement: Habilidades Requeridas

El sistema MUST persistir `CargoHabilidades` con unicidad por par Cargo-Habilidad y un `NivelId` obligatorio que referencie `NivelesHabilidad`. La fila MUST eliminarse físicamente al quitar la asociación.
(Anteriormente: el requisito no explicitaba FK de nivel, unicidad por par ni borrado físico.)

#### Scenario: Persistir asociación con nivel

- **DADO** un Cargo, una Habilidad y un Nivel válidos
- **CUANDO** se guarda la asociación
- **ENTONCES** la fila DEBE persistir con su `NivelId`
- **Y** no DEBEN existir duplicados para el mismo par Cargo-Habilidad.

#### Scenario: Eliminar asociación físicamente

- **DADO** que existe una fila en `CargoHabilidades`
- **CUANDO** se quita la habilidad del Cargo
- **ENTONCES** MySQL DEBE eliminar la fila
- **Y** no DEBE conservarse un marcador lógico.

### Requirement: Habilidades de Personas

El sistema MUST persistir `PersonaHabilidades` con unicidad por par Persona-Habilidad y un `NivelId` obligatorio que referencie `NivelesHabilidad`. La fila MUST eliminarse físicamente al quitar la asociación.
(Anteriormente: el requisito no explicitaba FK de nivel, unicidad por par ni borrado físico.)

#### Scenario: Persistir asociación con nivel

- **DADO** una Persona, una Habilidad y un Nivel válidos
- **CUANDO** se guarda la asociación
- **ENTONCES** la fila DEBE persistir con su `NivelId`
- **Y** no DEBEN existir duplicados para el mismo par Persona-Habilidad.

#### Scenario: Eliminar asociación físicamente

- **DADO** que existe una fila en `PersonaHabilidades`
- **CUANDO** se quita la habilidad de la Persona
- **ENTONCES** MySQL DEBE eliminar la fila
- **Y** no DEBE conservarse un marcador lógico.
