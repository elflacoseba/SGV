# Especificación de Gestión de Habilidades

## Propósito

Gestionar el catálogo maestro de Habilidades mediante `/api/v1/skills`, conservando lectura pública y agregando creación, actualización de campos editables, desactivación y reactivación. Las asignaciones a cargos o personas quedan fuera de esta porción.

## Requisitos

### Requirement: Crear Habilidad

El sistema MUST permitir crear una Habilidad activa proporcionando `Codigo`, `Nombre`, `Categoria` y opcionalmente `Descripcion`. `Codigo` MUST ser único entre habilidades activas.

#### Scenario: Creación exitosa

- **DADO** que no existe una Habilidad activa con el `Codigo` indicado
- **CUANDO** se solicita crear una Habilidad válida en `/api/v1/skills`
- **ENTONCES** el sistema MUST persistirla activa
- **Y** devolver los datos creados con campos consumer-safe.

#### Scenario: Codigo duplicado activo

- **DADO** que existe una Habilidad activa con `Codigo` "COM01"
- **CUANDO** se solicita crear otra Habilidad activa con `Codigo` "COM01"
- **ENTONCES** el sistema MUST rechazar la operación con conflicto.

### Requirement: Consultar Habilidades

El sistema MUST mantener `GET /api/v1/skills` y `GET /api/v1/skills/{id:guid}` como contrato canónico de lectura, devolviendo habilidades activas por defecto.

#### Scenario: Listar habilidades activas

- **DADO** que existen habilidades activas e inactivas
- **CUANDO** se solicita la colección de skills
- **ENTONCES** el sistema MUST devolver solo habilidades activas.

#### Scenario: Obtener habilidad inexistente o inactiva

- **DADO** que una Habilidad no existe o está inactiva
- **CUANDO** se solicita por identificador
- **ENTONCES** el sistema MUST responder como recurso no encontrado para consultas activas.

### Requirement: Actualizar Habilidad

El sistema MUST permitir actualizar `Nombre`, `Categoria` y `Descripcion` de una Habilidad existente. `Codigo` MUST NOT ser editable tras la creación.

#### Scenario: Actualización exitosa

- **DADO** una Habilidad activa existente
- **CUANDO** se actualizan sus campos editables
- **ENTONCES** el sistema MUST persistir los cambios
- **Y** devolver la Habilidad actualizada.

#### Scenario: Codigo inmutable

- **DADO** una Habilidad existente con `Codigo` "COM01"
- **CUANDO** se intenta cambiar `Codigo`
- **ENTONCES** el sistema MUST NOT permitir la modificación
- **Y** el contrato de actualización MUST NOT incluir `Codigo` como campo editable.

### Requirement: Desactivar y Reactivar Habilidad

El sistema MUST permitir baja lógica y reactivación de Habilidades sin eliminación física. La desactivación MUST NOT modificar asignaciones existentes a cargos o personas; gestionar esas asignaciones queda fuera de alcance.

#### Scenario: Desactivación exitosa

- **DADO** una Habilidad activa, con o sin referencias existentes
- **CUANDO** se solicita desactivarla
- **ENTONCES** el sistema MUST marcarla inactiva sin eliminar el registro
- **Y** MUST NOT alterar relaciones existentes.

#### Scenario: Reactivación sin conflicto

- **DADO** una Habilidad inactiva con `Codigo` "COM01"
- **Y** no existe otra Habilidad activa con ese `Codigo`
- **CUANDO** se solicita reactivarla
- **ENTONCES** el sistema MUST restaurar su estado activo conservando `Codigo`.

#### Scenario: Reactivación con conflicto activo

- **DADO** una Habilidad inactiva con `Codigo` "COM01"
- **Y** existe otra Habilidad activa con `Codigo` "COM01"
- **CUANDO** se solicita reactivarla
- **ENTONCES** el sistema MUST rechazar la operación con conflicto.

### Requirement: Excluir Asignaciones Iniciales

El sistema MUST NOT incluir en esta porción endpoints ni comandos para asignar Habilidades a cargos o personas.

#### Scenario: Operaciones de asignación no disponibles

- **DADO** que el módulo inicial de Habilidades está publicado
- **CUANDO** un cliente revisa el contrato de `/api/v1/skills`
- **ENTONCES** solo MUST encontrar operaciones del catálogo maestro
- **Y** MUST NOT encontrar operaciones de `CargoHabilidad` ni `PersonaHabilidad`.
