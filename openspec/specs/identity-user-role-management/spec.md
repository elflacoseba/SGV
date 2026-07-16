# Especificación de Identity User Role Management

## Propósito

Administrar usuarios autenticables SGV vinculados a Personas existentes, con un catálogo fijo de roles (Administrador, GestorVacantes, Consultor) y autenticación mediante Identity como preocupación de Infraestructura.

## Requisitos

### Requirement: Usuario Vinculado a Persona Existente

El sistema MUST crear y administrar usuarios autenticables solo cuando estén asociados a una `Persona` existente. Un usuario MUST NOT existir como cuenta standalone sin Persona asociada.

#### Escenario: Crear usuario para Persona existente

- **DADO** que existe una Persona registrada
- **CUANDO** se solicita crear un usuario para esa Persona con credenciales válidas
- **ENTONCES** el sistema MUST crear el usuario vinculado a esa Persona
- **Y** el vínculo MUST ser observable desde las operaciones administrativas de usuarios.

#### Escenario: Rechazar usuario sin Persona válida

- **DADO** que no existe una Persona para el identificador informado
- **CUANDO** se solicita crear un usuario
- **ENTONCES** el sistema MUST rechazar la operación sin crear la cuenta.

### Requirement: Catálogo Fijo de Roles

El sistema MUST reconocer únicamente los roles `Administrador`, `GestorVacantes` y `Consultor` en este primer corte. Los consumidores MUST NOT crear, renombrar ni eliminar roles mediante operaciones de SGV.

#### Escenario: Consultar roles disponibles

- **DADO** que el sistema expone roles asignables
- **CUANDO** se consultan los roles disponibles
- **ENTONCES** el sistema MUST devolver solo `Administrador`, `GestorVacantes` y `Consultor`.

#### Escenario: Rechazar rol fuera del catálogo

- **DADO** una solicitud que referencia un rol distinto del catálogo fijo
- **CUANDO** se intenta usarlo para un usuario
- **ENTONCES** el sistema MUST rechazar la solicitud como rol no soportado.

### Requirement: Asignación de Roles a Usuarios

El sistema MUST permitir asignar a un usuario existente uno o más roles del catálogo fijo. Toda asignación MUST respetar el catálogo aprobado y MUST NOT introducir roles nuevos por efecto lateral.

#### Escenario: Asignar rol válido

- **DADO** que existe un usuario vinculado a una Persona
- **CUANDO** se le asigna el rol `GestorVacantes`
- **ENTONCES** el usuario MUST quedar asociado a ese rol.

#### Escenario: Rechazar asignación a usuario inexistente

- **DADO** que no existe el usuario objetivo
- **CUANDO** se solicita asignarle un rol válido
- **ENTONCES** el sistema MUST rechazar la operación sin modificar asignaciones.

### Requirement: Paginación y segmentación de Usuarios

`GET /api/v1/usuarios/consulta?page=&pageSize=&search=&sort=&status=activas|eliminadas` MUST estar disponible para cualquier usuario autenticado. `search` MUST aplicar sobre `UserName|Email|Nombres|Apellidos`. `status` omitido o inválido MUST caer a `activas`. Respuesta MUST ser `PagedResult<UsuarioDto>` (incluyendo `Nombres`/`Apellidos` y roles).

#### Scenario: Listar usuarios con paginación, búsqueda y orden server-side

- **DADO** usuarios activos persistidos
- **CUANDO** se solicita `/consulta?search=juan&sort=apellidos_asc&p=1`
- **ENTONCES** MUST responder `200` con `PagedResult<UsuarioDto>` paginado, excluyendo inactivos, con la búsqueda y el orden aplicados antes de `Skip/Take`.

#### Scenario: Paginación o status inválidos se normalizan

- **DADO** usuarios en ambos segmentos en persistencia
- **CUANDO** se consulta `/consulta?status=archivo&page=0&pageSize=500`
- **ENTONCES** MUST caer a `activas` con `page=1` y `pageSize` limitado a `100`.

#### Scenario: Búsqueda sin coincidencias devuelve página vacía

- **DADO** un usuario autenticado consulta un segmento válido
- **CUANDO** `search` no coincide con ningún usuario del segmento
- **ENTONCES** MUST responder `200` con `items` vacíos y `totalCount=0`.

### Requirement: Consulta paginada libre de N+1 en roles

`/consulta` MUST proyectar roles junto con datos básicos en una sola query (sin invocar `UserManager.GetRolesAsync` por cada fila del bucle). La query MUST devolver `UsuarioDto` con `Roles` ya poblado.

#### Scenario: Listado sin N+1

- **DADO** N usuarios en el segmento consultado
- **CUANDO** un autenticado solicita `/consulta`
- **ENTONCES** el sistema MUST ejecutar una sola query agregada (verificable por test que asserte que `GetRolesAsync` no se invoca dentro del bucle).

### Requirement: Edición de un usuario existente

`PUT /api/v1/usuarios/{id}` MUST exigir rol `Administrador` y MUST permitir actualizar `UserName`, `Email` y roles en una sola operación. `UserName`/`Email` MUST respetar unicidad.

#### Scenario: Edición exitosa

- **DADO** un usuario existente
- **CUANDO** un `Administrador` envía `PUT` con datos válidos
- **ENTONCES** MUST responder `200`, persistir cambios y reflejar la proyección con roles actualizados.

#### Scenario: Conflicto por UserName duplicado

- **DADO** otro usuario con el mismo `UserName`
- **CUANDO** un `Administrador` intenta renombrar
- **ENTONCES** MUST responder `409 Conflict` con `ErrorCategoria.Conflict` y mensaje del campo afectado.

#### Scenario: Concurrencia con otro Administrador

- **DADO** dos `Administradores` editando el mismo usuario en paralelo
- **CUANDO** ambos guardan cambios casi simultáneamente
- **ENTONCES** la respuesta MUST ser coherente con la última escritura persistida
- **Y** MUST informarse al cliente si la edición quedó invalidada por otro cambio.

### Requirement: Baja lógica de un usuario

`DELETE /api/v1/usuarios/{id}` MUST exigir rol `Administrador`, MUST marcar `IsDeleted=1` en `AspNetUsers` (sin borrado físico) y MUST respetar la columna generada `ActiveUserNameUnique` para que el `UserName` quede libre para reactivaciones futuras sin colisionar.

#### Scenario: Baja lógica exitosa

- **DADO** un usuario activo
- **CUANDO** un `Administrador` envía `DELETE`
- **ENTONCES** MUST responder `200`, marcar `IsDeleted=1`, dejarlo fuera del segmento `activas` y exponerlo en `eliminadas`.

#### Scenario: Auto-baja prohibida

- **DADO** un `Administrador` autenticado que coincide con el `id` objetivo
- **CUANDO** intenta ejecutar `DELETE` sobre sí mismo
- **ENTONCES** MUST responder `403 Forbidden` (o `ErrorCategoria.Conflict`) sin aplicar la baja.

### Requirement: Reactivación lógica de un usuario con validación de Persona activa

`PATCH /api/v1/usuarios/{id}/reactivar` MUST exigir rol `Administrador`, MUST poner `IsDeleted=0`, MUST verificar que la `PersonaId` asociada exista y esté `IsDeleted=0`. Si la `Persona` está inactiva o no existe, MUST responder `ErrorCategoria.Conflict` con código `PersonaInactiva`.

#### Scenario: Reactivación exitosa

- **DADO** un usuario eliminado cuya `Persona` vinculada está activa
- **CUANDO** un `Administrador` envía `PATCH /reactivar`
- **ENTONCES** MUST responder `200`, exponer al usuario en `activas` y liberar el `UserName` para reasignación única.

#### Scenario: Reactivación fallida por Persona inactiva

- **DADO** un usuario eliminado cuya `Persona` vinculada tiene `IsDeleted=1`
- **CUANDO** un `Administrador` envía `PATCH /reactivar`
- **ENTONCES** MUST responder `409 Conflict` con `ErrorCategoria.Conflict`, código `PersonaInactiva` y mensaje accionable.

### Requirement: Taxonomía de errores en operaciones de usuarios

Las operaciones de `UsuariosController` MUST reportar sus errores mediante la taxonomía `ErrorCategoria` con códigos por dominio (`PersonaInactiva`, `RolNoSoportado`, `UserNameDuplicado`, `EmailDuplicado`, `AutoBaja`, `PersonaRequerida`) consistente con la regla #125.

#### Scenario: Errores discriminados por categoria

- **DADO** cualquier endpoint de `UsuariosController`
- **CUANDO** se produce un fallo de dominio
- **ENTONCES** la respuesta MUST tipar `ErrorCategoria` (`Conflict`, `Validation`, `NotFound`, `Unauthorized`, `Transport`) y MUST incluir un código de dominio legible por la UI.
