# Delta para SGV Database

## ADDED Requirements

### Requirement: Personas Administrables Persistidas

El sistema MUST persistir Personas como registros administrables con datos básicos, identificación, contacto y estado activo/inactivo. La persistencia MUST conservar baja lógica y MUST permitir consultas administrativas sin cargar ni exponer Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`.

#### Scenario: Persistir persona activa

- **DADO** que no existe una Persona activa con los mismos identificadores únicos informados
- **CUANDO** se guarda una nueva Persona válida
- **ENTONCES** la base de datos DEBE persistirla como activa
- **Y** sus datos propios DEBEN poder recuperarse sin relaciones excluidas.

#### Scenario: Baja lógica de persona

- **DADO** que existe una Persona activa
- **CUANDO** se solicita desactivarla
- **ENTONCES** la persistencia DEBE conservar el registro y marcarlo como inactivo
- **Y** las consultas activas por defecto DEBEN excluirlo.

#### Scenario: Reactivar persona persistida

- **DADO** que existe una Persona inactiva
- **CUANDO** se solicita reactivarla sin conflictos únicos activos
- **ENTONCES** la persistencia DEBE restaurar el estado activo.

### Requirement: Unicidad Activa de Persona en MySQL

El sistema MUST impedir duplicados activos de `Legajo`, `Email` y documento de Persona. La estrategia MUST ser compatible con MySQL/Pomelo, como columnas generadas, índices únicos equivalentes o el mecanismo ya modelado. La aplicación MUST traducir violaciones únicas a conflictos claros para consumidores.

#### Scenario: Rechazar legajo activo duplicado

- **DADO** que existe una Persona activa con un `Legajo`
- **CUANDO** se guarda otra Persona activa con el mismo `Legajo`
- **ENTONCES** la aplicación o la base de datos DEBE rechazar el cambio como conflicto de `Legajo`.

#### Scenario: Rechazar email activo duplicado

- **DADO** que existe una Persona activa con un `Email`
- **CUANDO** se guarda otra Persona activa con el mismo `Email`
- **ENTONCES** la aplicación o la base de datos DEBE rechazar el cambio como conflicto de `Email`.

#### Scenario: Rechazar documento activo duplicado

- **DADO** que existe una Persona activa con tipo y número de documento
- **CUANDO** se guarda otra Persona activa con el mismo documento
- **ENTONCES** la aplicación o la base de datos DEBE rechazar el cambio como conflicto de documento.

#### Scenario: Permitir reutilización tras baja lógica

- **DADO** que una Persona con `Legajo`, `Email` o documento fue desactivada
- **CUANDO** se crea o reactiva una Persona con esos valores
- **ENTONCES** el sistema PUEDE permitirlo si no existe otra Persona activa con los mismos valores.
