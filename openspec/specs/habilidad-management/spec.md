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

El sistema MUST mantener `GET /api/v1/skills` y `GET /api/v1/skills/{id:guid}` como contrato legacy de lectura de habilidades activas para usuarios autenticados y MUST agregar `GET /api/v1/skills/consulta` como consulta paginada y filtrada. `status` MUST aceptar `activas|eliminadas`; si se omite o es inválido MUST caer a `activas`. `page < 1` MUST normalizarse a `1`; `pageSize < 1` MUST caer a `20`; `pageSize > 100` MUST limitarse a `100`; `sort` desconocido MUST caer a `codigo_asc`.

#### Scenario: Listar habilidades activas legacy

- **DADO** que existen habilidades activas e inactivas
- **CUANDO** un usuario autenticado solicita `GET /api/v1/skills`
- **ENTONCES** el sistema MUST devolver solo habilidades activas.

#### Scenario: Obtener habilidad inexistente o inactiva

- **DADO** que una Habilidad no existe o está inactiva
- **CUANDO** un usuario autenticado la solicita por identificador
- **ENTONCES** el sistema MUST responder como recurso no encontrado para consultas activas.

#### Scenario: Consulta de eliminadas no mezcla segmentos

- **DADO** habilidades activas e inactivas en persistencia
- **CUANDO** un usuario autenticado consulta `GET /api/v1/skills/consulta?status=eliminadas&search=com&page=2&pageSize=10&sort=nombre_desc`
- **ENTONCES** la respuesta MUST exponer `Items` con habilidades inactivas únicamente que coincidan con `search=com`
- **Y** MUST aplicar normalización con `Page=1`, `PageSize=100`
- **Y** MUST exponer `TotalCount` consistente con el segmento `eliminadas` y `sort=nombre_desc`.

#### Scenario: Paginación o status inválidos se normalizan

- **DADO** habilidades activas e inactivas en persistencia
- **CUANDO** un usuario autenticado consulta `/api/v1/skills/consulta?status=archivo&page=0&pageSize=500`
- **ENTONCES** la respuesta MUST caer a `activas`
- **Y** MUST usar `page=1` y `pageSize=100`.

#### Scenario: Búsqueda sin coincidencias devuelve página vacía

- **DADO** un usuario autenticado consulta un segmento válido
- **CUANDO** `search` no coincide con ninguna habilidad del segmento
- **ENTONCES** el sistema MUST responder `200 OK` con `items` vacíos
- **Y** MUST conservar `totalCount=0` y metadatos de la página solicitada normalizada.

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

### Requirement: Publicar catálogo HTTP de niveles de habilidad

El sistema MUST exponer `GET /api/v1/niveles-habilidad` como catálogo consumer-safe para `NivelHabilidad`, ordenado ascendentemente por `Orden`, sin mezclar reglas de asignación a cargos o personas.

#### Scenario: Catálogo de niveles disponible para web

- **DADO** que existen niveles de habilidad sembrados en persistencia
- **CUANDO** un cliente solicita `GET /api/v1/niveles-habilidad`
- **ENTONCES** el sistema MUST responder `200 OK` con elementos ordenados por `orden`
- **Y** cada elemento MUST exponer solo campos consumer-safe del nivel.

#### Scenario: Catálogo vacío sigue siendo válido

- **DADO** que no existen niveles de habilidad publicados
- **CUANDO** un cliente solicita `GET /api/v1/niveles-habilidad`
- **ENTONCES** el sistema MUST responder `200 OK` con una colección vacía.

### Requirement: Autorización de endpoints de habilidades

`SkillsController` MUST requerir autenticación a nivel de controller. `GET /api/v1/skills`, `GET /api/v1/skills/{id}` y `GET /api/v1/skills/consulta` MUST permitir cualquier usuario autenticado. `POST`, `PUT`, `DELETE` y `PATCH /reactivar` MUST requerir rol `Administrador`.

#### Scenario: Lecturas autenticadas exitosas

- **DADO** un usuario autenticado
- **CUANDO** solicita una lectura de `SkillsController`
- **ENTONCES** la API MUST responder `2xx` con el contrato documentado.

#### Scenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita una lectura o mutación de `SkillsController`
- **ENTONCES** la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- **DADO** una solicitud válida de create, update, delete o reactivate
- **CUANDO** la ejecuta un usuario autenticado sin rol `Administrador`
- **ENTONCES** la API MUST responder `403 Forbidden`
- **Y** si la ejecuta un `Administrador`, MUST conservar su contrato `2xx` vigente.
