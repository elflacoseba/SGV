# Delta para Gestión de Cargos

## MODIFIED Requirements

### Requirement: Gestión de Cargos

El sistema MUST conservar el catálogo maestro de Cargos y, además, exponer la gestión de sus habilidades requeridas como subrecurso. `/api/v1/cargos/{cargoId}/skills` MUST permitir listar, agregar, actualizar y quitar asociaciones Cargo-Habilidad con nivel requerido. `CargoManagement` MUST NOT incluir CRUD de `Habilidades` ni de `NivelesHabilidad`.
(Anteriormente: la gestión de Cargos no explicitaba el subrecurso de habilidades requeridas.)

#### Scenario: Consultar cargo sin habilidades

- **DADO** que existe un Cargo sin habilidades asociadas
- **CUANDO** se consulta el Cargo
- **ENTONCES** el sistema DEBE devolver el Cargo sin inventar asociaciones.

#### Scenario: Gestionar habilidades requeridas

- **DADO** que existe un Cargo válido
- **CUANDO** se administra `/api/v1/cargos/{cargoId}/skills`
- **ENTONCES** el sistema DEBE persistir y devolver las asociaciones con su nivel requerido.
