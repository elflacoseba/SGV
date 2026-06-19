# Delta para Base de Datos SGV

## ADDED Requirements

### Requirement: Catálogo Habilidades Administrable

El sistema MUST persistir Habilidades como catálogo maestro administrable con `Codigo`, `Nombre`, `Categoria`, `Descripcion` opcional y estado activo/inactivo. `Codigo` MUST ser inmutable tras la creación y único entre habilidades activas. La persistencia MUST conservar compatibilidad con MySQL/Pomelo y MUST NOT requerir asignaciones nuevas a cargos o personas en esta porción.

#### Scenario: Persistir Habilidad activa

- **DADO** que no existe una Habilidad activa con el mismo `Codigo`
- **CUANDO** se guarda una nueva Habilidad válida
- **ENTONCES** la base de datos MUST persistirla como activa
- **Y** sus consultas activas MUST poder recuperarla.

#### Scenario: Rechazar Codigo activo duplicado

- **DADO** que existe una Habilidad activa con un `Codigo`
- **CUANDO** se intenta guardar otra Habilidad activa con el mismo `Codigo`
- **ENTONCES** la aplicación o la base de datos MUST rechazar el cambio.

#### Scenario: Permitir reutilización tras baja lógica

- **DADO** que una Habilidad con un `Codigo` fue desactivada
- **CUANDO** se crea o reactiva una Habilidad con ese `Codigo`
- **ENTONCES** el sistema MAY permitirlo si no existe otra Habilidad activa con ese `Codigo`.

#### Scenario: Baja lógica sin eliminación física

- **DADO** que existe una Habilidad activa
- **CUANDO** se solicita desactivarla
- **ENTONCES** el sistema MUST marcarla inactiva sin eliminar físicamente el registro
- **Y** MUST excluirla de consultas activas por defecto.

#### Scenario: Desactivar Habilidad referenciada

- **DADO** que una Habilidad tiene referencias existentes desde cargos o personas
- **CUANDO** se solicita desactivarla
- **ENTONCES** el sistema MUST NOT eliminar ni modificar esas relaciones
- **Y** las reglas de asignación quedan fuera del alcance de este cambio.

#### Scenario: Preservar estrategia MySQL para unicidad activa

- **DADO** que MySQL no ofrece índices filtrados equivalentes
- **CUANDO** se define la unicidad activa de `Codigo`
- **ENTONCES** el contrato MUST usar una estrategia compatible con MySQL/Pomelo, como columna generada o equivalente verificado.
