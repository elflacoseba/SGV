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

`GET /api/v1/usuarios/consulta?page=&pageSize=&search=&sort=&status=activas|bloqueadas` MUST estar disponible para cualquier usuario autenticado. `search` MUST aplicar sobre `UserName|Email|Nombres|Apellidos`. `status` omitido o inválido MUST caer a `activas`. Respuesta MUST ser `PagedResult<UsuarioDto>` (con `Nombres`/`Apellidos` y roles). `bloqueadas` MUST incluir a todo usuario con `LockoutEnd` futuro vigente; `activas` MUST excluir eliminados físicamente y a todo aquel con lockout vigente.

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

#### Scenario: Eliminación física exitosa

- **DADO** un usuario activo o bloqueado
- **CUANDO** un `Administrador` envía `DELETE`
- **ENTONCES** MUST responder `200`, eliminar físicamente la fila y conservar `Persona` y `Auditorias`.

#### Scenario: Auto-eliminación prohibida

- **DADO** un `Administrador` cuyo `id` coincide con el objetivo
- **CUANDO** intenta `DELETE` sobre sí mismo
- **ENTONCES** MUST responder `403` con código `AutoEliminacion` sin aplicar la baja.

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

### Requirement: Taxonomía de errores en operaciones de usuarios

Las operaciones de `UsuariosController` MUST reportar errores vía `ErrorCategoria` con códigos por dominio: `PersonaInactiva`, `RolNoSoportado`, `UserNameDuplicado`, `EmailDuplicado`, `AutoBaja`, `AutoBloqueo`, `AutoEliminacion`, `UsuarioBloqueado`, `UsuarioNoEncontrado`, `PersonaRequerida`. Bloqueo, desbloqueo y eliminación extienden este catálogo.

#### Scenario: Errores discriminados por categoria

- **DADO** cualquier endpoint de `UsuariosController`
- **CUANDO** se produce un fallo de dominio
- **ENTONCES** la respuesta MUST tipar `ErrorCategoria` (`Conflict`, `Validation`, `NotFound`, `Unauthorized`, `Transport`) y MUST incluir un código de dominio legible.

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
