# Delta para Gestión de Personas

## MODIFIED Requirements

### Requirement: Datos Administrables de Persona

El sistema MUST administrar Personas con datos básicos, identificación, contacto y estado activo/inactivo. Además, MUST exponer el subrecurso `/api/v1/personas/{personaId}/skills` para administrar habilidades de la Persona. Este corte MUST NOT incluir Postulantes ni Ocupaciones.
(Anteriormente: el contrato excluía Habilidades y `PersonaHabilidad` por completo.)

#### Scenario: Consultar detalle de persona

- **DADO** que existe una Persona persistida
- **CUANDO** se consulta su detalle administrativo
- **ENTONCES** el sistema DEBE devolver sus datos básicos, identificación, contacto y estado
- **Y** DEBE permitir acceder a sus habilidades por el subrecurso.

#### Scenario: No mezclar dominios excluidos

- **DADO** una Persona con habilidades asociadas
- **CUANDO** se consulta el detalle administrativo
- **ENTONCES** el sistema NO DEBE incluir Postulantes ni Ocupaciones
- **Y** las habilidades DEBEN gestionarse solo por el subrecurso específico.
