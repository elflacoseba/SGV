# Delta para `identity-user-role-management`

> Reemplaza "baja lógica / reactivación" por eliminación física y bloqueo administrativo, y ajusta el segmento de listado a `activas|bloqueadas`. El observable de cookie web ante bloqueo/eliminación queda cubierto en `sgv-web-authentication`.

## MODIFIED Requirements

### Requirement: Paginación y segmentación de Usuarios

`GET /api/v1/usuarios/consulta?page=&pageSize=&search=&sort=&status=activas|bloqueadas` MUST estar disponible para cualquier usuario autenticado. `search` MUST aplicar sobre `UserName|Email|Nombres|Apellidos`. `status` omitido o inválido MUST caer a `activas`. Respuesta MUST ser `PagedResult<UsuarioDto>` (con `Nombres`/`Apellidos` y roles). `bloqueadas` MUST incluir a todo usuario con `LockoutEnd` futuro vigente; `activas` MUST excluir eliminados físicamente y a todo aquel con lockout vigente.
(Previously: `status=activas|eliminadas` definidos por `IsDeleted`.)

#### Scenario: Listar con paginación, búsqueda y orden server-side

- **DADO** usuarios activos y bloqueados persistidos
- **CUANDO** se solicita `/consulta?search=juan&sort=apellidos_asc&p=1&status=bloqueadas`
- **ENTONCES** MUST responder `200` con `PagedResult<UsuarioDto>` paginado, sólo con `LockoutEnd` futuro vigente, con búsqueda y orden aplicados antes de `Skip/Take`.

#### Scenario: Paginación o status inválidos se normalizan

- **DADO** usuarios en ambos segmentos
- **CUANDO** se consulta `/consulta?status=archivo&page=0&pageSize=500`
- **ENTONCES** MUST caer a `activas` con `page=1` y `pageSize` ≤ `100`.

#### Scenario: Búsqueda sin coincidencias devuelve página vacía

- **DADO** un autenticado consulta un segmento válido
- **CUANDO** `search` no coincide
- **ENTONCES** MUST responder `200` con `items` vacíos y `totalCount=0`.

### Requirement: Eliminación física de un usuario

`DELETE /api/v1/usuarios/{id}` MUST exigir rol `Administrador` y MUST ejecutar la eliminación física definida en `usuario-delete-fisico` (borrado de `AspNetUsers` y cascadas técnicas; conserva `Persona` y `Auditorias`). El endpoint MUST rechazar auto-eliminación e inexistentes según lo definido allí.
(Previously: `IsDeleted=1` con `ActiveUserNameUnique`.)

#### Scenario: Eliminación física exitosa

- **DADO** un usuario activo o bloqueado
- **CUANDO** un `Administrador` envía `DELETE`
- **ENTONCES** MUST responder `200`, eliminar físicamente la fila y conservar `Persona` y `Auditorias`.

#### Scenario: Auto-eliminación prohibida

- **DADO** un `Administrador` cuyo `id` coincide con el objetivo
- **CUANDO** intenta `DELETE` sobre sí mismo
- **ENTONCES** MUST responder `403` con código `AutoEliminacion` sin aplicar la baja.

### Requirement: Taxonomía de errores en operaciones de usuarios

Las operaciones de `UsuariosController` MUST reportar errores vía `ErrorCategoria` con códigos por dominio: `PersonaInactiva`, `RolNoSoportado`, `UserNameDuplicado`, `EmailDuplicado`, `AutoBaja`, `AutoBloqueo`, `AutoEliminacion`, `UsuarioBloqueado`, `UsuarioNoEncontrado`, `PersonaRequerida`. Bloqueo, desbloqueo y eliminación extienden este catálogo.
(Previously: sin `AutoBloqueo`, `AutoEliminacion`, `UsuarioBloqueado`, `UsuarioNoEncontrado`.)

#### Scenario: Errores discriminados por categoria

- **DADO** cualquier endpoint de `UsuariosController`
- **CUANDO** se produce un fallo de dominio
- **ENTONCES** la respuesta MUST tipar `ErrorCategoria` (`Conflict`, `Validation`, `NotFound`, `Unauthorized`, `Transport`) y MUST incluir un código de dominio legible.

## REMOVED Requirements

### Requirement: Baja lógica de un usuario

(Reason: se reemplaza por eliminación física cubierta en `usuario-delete-fisico`; `IsDeleted` y `ActiveUserNameUnique` dejan de existir.)
(Migration: `DELETE /api/v1/usuarios/{id}` documenta el nuevo comportamiento; los clientes migran a borrado físico irreversible.)

### Requirement: Reactivación lógica de un usuario con validación de Persona activa

(Reason: sin soft-delete no hay "reactivación"; el desbloqueo se trata en `usuario-lockout-administrativo` y la validación de `Persona` activa deja de aplicar.)
(Migration: clientes que dependían de `PATCH /reactivar` deben migrar a `POST /desbloquear`.)

## ADDED Requirements

### Requirement: Invalidación inmediata de credenciales activas tras bloqueo o eliminación

Bloquear o eliminar una cuenta MUST cortar de inmediato el acceso del JWT bearer y de la cookie web ya emitidos, sin esperar `exp` ni logout. Una llamada API con JWT válido dentro de `exp` MUST responder `401`; la API MUST NOT emitir un nuevo JWT durante el lockout ni tras eliminación. Los observables de cookie se cubren en `sgv-web-authentication`.

#### Scenario: 401 inmediato tras bloqueo o eliminación

- **DADO** usuario autenticado con JWT vigente
- **CUANDO** `Administrador` ejecuta `POST /bloquear` o `DELETE` sobre esa cuenta
- **ENTONCES** la siguiente llamada API con ese JWT MUST responder `401`, sin esperar `exp`.

#### Scenario: Desbloqueo exige login fresco

- **DADO** usuario bloqueado con JWT emitido antes del bloqueo
- **CUANDO** `Administrador` ejecuta `POST /desbloquear` y el usuario reintenta con el JWT previo
- **ENTONCES** el JWT MUST seguir rechazado; el acceso MUST restaurarse solo tras un login fresco.