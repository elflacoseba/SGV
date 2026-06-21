# Delta para Gestión de Habilidades

## MODIFIED Requirements

### Requirement: Excluir Asignaciones Iniciales

El sistema MUST mantener `/api/v1/skills` como catálogo maestro y MUST NOT mezclar en ese contrato la gestión de asignaciones a Cargos o Personas. Las asociaciones MUST vivir exclusivamente en `/api/v1/cargos/{cargoId}/skills` y `/api/v1/personas/{personaId}/skills`.
(Anteriormente: el contrato solo aclaraba que no había endpoints de asignación en `/api/v1/skills`.)

#### Scenario: Operaciones de catálogo siguen separadas

- **DADO** que un cliente revisa `/api/v1/skills`
- **CUANDO** consulta el contrato disponible
- **ENTONCES** el sistema MUST mostrar solo operaciones del catálogo maestro
- **Y** MUST NOT exponer asociaciones por subrecurso.
