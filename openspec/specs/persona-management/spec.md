# Especificación de Persona Management

## Propósito

El sistema DEBE administrar Personas como entidades independientes con datos básicos, identificación, contacto y estado activo/inactivo. Este corte NO DEBE incluir Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`.

## Requisitos

### Requisito: Datos Administrables de Persona

El sistema MUST administrar Personas con datos básicos, identificación, contacto y estado activo/inactivo. Este corte MUST NOT incluir Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`. Las respuestas MUST ser modelos seguros para consumidores y MUST NOT exponer entidades de dominio, entidades de persistencia, auditoría interna ni navegaciones excluidas.

#### Escenario: Consultar detalle de persona

- **DADO** que existe una Persona persistida
- **CUANDO** se consulta su detalle administrativo
- **ENTONCES** el sistema DEBE devolver sus datos básicos, identificación, contacto y estado
- **Y** NO DEBE incluir Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`.

#### Escenario: Listar personas

- **DADO** que existen Personas persistidas
- **CUANDO** se solicita el listado administrativo
- **ENTONCES** el sistema DEBE devolver una colección de Personas con contrato consumer-safe.

### Requisito: Alta de Persona

El sistema MUST permitir crear Personas con datos válidos. `Legajo` MUST ser requerido y único entre Personas activas. `Email` y documento MAY omitirse, pero si se informan MUST respetar formato/longitud y unicidad activa.

#### Escenario: Crear persona válida

- **DADO** que no existe una Persona activa con el mismo `Legajo`, `Email` ni documento informado
- **CUANDO** se crea una Persona con datos válidos
- **ENTONCES** el sistema DEBE persistirla como activa
- **Y** DEBE devolver su identificador y datos administrables.

#### Escenario: Rechazar datos obligatorios faltantes

- **DADO** una solicitud de creación de Persona
- **CUANDO** falta un dato obligatorio como `Legajo` o nombre requerido por el contrato
- **ENTONCES** el sistema DEBE rechazar la solicitud sin persistir cambios.

### Requisito: Actualización de Persona

El sistema MUST permitir actualizar datos propios de Persona sin modificar relaciones fuera de alcance. La actualización MUST preservar la unicidad activa de `Legajo`, `Email` y documento.

#### Escenario: Actualizar contacto

- **DADO** que existe una Persona activa
- **CUANDO** se actualizan datos de contacto válidos
- **ENTONCES** el sistema DEBE persistir los cambios
- **Y** NO DEBE alterar relaciones excluidas.

#### Escenario: Rechazar duplicados activos

- **DADO** que existe otra Persona activa con el mismo `Legajo`, `Email` o documento
- **CUANDO** se intenta crear, actualizar o reactivar una Persona con esos valores
- **ENTONCES** el sistema DEBE rechazar la operación con un conflicto claro.

### Requisito: Ciclo de Vida de Persona

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

### Requisito: Exclusiones del Primer Corte

El sistema MUST NOT crear, modificar, consultar ni exponer comportamiento de Postulantes, Ocupaciones, Habilidades o `PersonaHabilidad` desde el módulo administrativo de Personas.

#### Escenario: No exponer relaciones excluidas

- **DADO** que una Persona tiene relaciones persistidas fuera de este corte
- **CUANDO** se usa cualquier operación administrativa de Personas
- **ENTONCES** la operación NO DEBE incluir ni modificar esas relaciones.
