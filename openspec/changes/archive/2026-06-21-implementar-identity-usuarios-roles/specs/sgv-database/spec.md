# Delta for SGV Database

## ADDED Requirements

### Requirement: Vínculo Obligatorio Usuario-Persona

El sistema MUST persistir cada usuario autenticable con una referencia obligatoria a una Persona existente. La base de datos MUST rechazar usuarios cuyo vínculo apunte a una Persona inexistente o ausente.

#### Escenario: Persistir usuario con Persona existente

- **DADO** que existe una Persona persistida
- **CUANDO** se persiste un usuario autenticable vinculado a esa Persona
- **ENTONCES** la base de datos MUST aceptar el registro
- **Y** el vínculo MUST poder consultarse de forma consistente.

#### Escenario: Rechazar usuario sin Persona existente

- **DADO** que no existe la Persona referenciada
- **CUANDO** se intenta persistir un usuario autenticable con esa referencia
- **ENTONCES** la base de datos MUST rechazar el cambio.

### Requirement: Seed de Roles Fijos de Identity

El sistema MUST conservar como roles asignables únicamente `Administrador`, `GestorVacantes` y `Consultor` para este corte. El estado persistido MUST NOT permitir que las operaciones SGV dependan de roles fuera de ese catálogo.

#### Escenario: Roles fijos disponibles

- **DADO** una base de datos inicializada
- **CUANDO** se consultan los roles asignables de SGV
- **ENTONCES** MUST existir `Administrador`, `GestorVacantes` y `Consultor`
- **Y** no DEBE requerirse ningún rol adicional para operar este corte.

#### Escenario: Asignación respeta catálogo persistido

- **DADO** un usuario autenticable existente
- **CUANDO** se intenta persistir una asignación a un rol fuera del catálogo fijo
- **ENTONCES** el sistema MUST rechazar o impedir esa asignación.
