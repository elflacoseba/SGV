# Delta para identity-user-role-management

## Propósito

Extender la administración de usuarios y roles para soportar paginación segmentada, soft-delete, edición conjunta de credenciales y roles, y taxonomía de errores consistente con la regla #125 — preservando el vínculo 1:1 con `Persona`, el catálogo fijo de roles (`Administrador`, `GestorVacantes`, `Consultor`) y la autorización por acción.

## Modificaciones

- Se agrega `GET /api/v1/usuarios/consulta` para el listado paginado y segmentado.
- Se agrega `PUT /api/v1/usuarios/{id}` para editar `UserName`, `Email` y roles en una sola operación.
- Se agrega `DELETE /api/v1/usuarios/{id}` (soft-delete vía `IsDeleted`).
- Se agrega `PATCH /api/v1/usuarios/{id}/reactivar` con validación de `Persona` activa.
- Se mantiene `PUT /api/v1/usuarios/{userId}/roles` para reemplazo de roles.
- Se mantiene `POST /api/v1/usuarios` para alta de usuario.
- Los errores de usuarios se reportan con la taxonomía `ErrorCategoria` (regla #125).

## ADDED Requirements

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

## Out of scope

- No agrega lockout/unlock de cuentas, multi-tenant, login/OAuth/refresh.
- No agrega cambio de contraseña desde la UI ni historial de login.
- No agrega `[Obsolete]` removal de los enums de error heredados — queda para `sdd-archive` del change #125.
