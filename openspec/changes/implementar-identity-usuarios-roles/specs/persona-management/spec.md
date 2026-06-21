# Delta for Persona Management

## MODIFIED Requirements

### Requirement: Ciclo de Vida de Persona

El sistema MUST permitir desactivar y reactivar Personas mediante baja lógica. Las consultas activas MUST excluir Personas inactivas por defecto. Una Persona con usuario autenticable asociado MUST conservar el vínculo histórico; cualquier restricción operativa sobre su desactivación MUST ser explícita y MUST NOT crear usuarios sin Persona.
(Previously: el requisito cubría baja/reactivación lógica sin describir el impacto de usuarios autenticables vinculados.)

#### Escenario: Desactivar persona

- **DADO** que existe una Persona activa
- **CUANDO** se solicita su desactivación
- **ENTONCES** el sistema DEBE marcarla como inactiva sin eliminación física.

#### Escenario: Reactivar persona sin conflicto

- **DADO** que existe una Persona inactiva sin conflictos activos de `Legajo`, `Email` ni documento
- **CUANDO** se solicita su reactivación
- **ENTONCES** el sistema DEBE restaurar su estado activo.

#### Escenario: Preservar vínculo de usuario al desactivar Persona

- **DADO** que una Persona tiene un usuario autenticable asociado
- **CUANDO** la Persona se desactiva o reactiva
- **ENTONCES** el sistema MUST preservar la asociación Persona-usuario
- **Y** MUST NOT convertir el usuario en una cuenta standalone.
