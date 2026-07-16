# Especificación: Bloqueo y desbloqueo administrativo de usuarios

## Propósito

Definir la operación administrativa de bloqueo y desbloqueo de cuentas mediante `LockoutEnd` nativo de ASP.NET Core Identity, utilizada para distinguir usuarios activos de bloqueados y para impedir el acceso al sistema cuando el bloqueo está vigente. Cubre bloqueos administrativos y temporales de Identity por intentos fallidos, sin exponer el origen del bloqueo al contrato público.

## Requisitos

### Requirement: Bloqueo administrativo de un usuario

`POST /api/v1/usuarios/{id}/bloquear` MUST exigir rol `Administrador` y MUST fijar `LockoutEnabled=true` y `LockoutEnd` futuro en UTC mediante `UserManager.SetLockoutEndDateAsync`. La operación MUST registrar un evento de `Auditoria` con código `BloqueoUsuario`.

#### Scenario: Bloqueo exitoso sobre usuario activo

- **DADO** un usuario activo sin lockout vigente
- **CUANDO** un `Administrador` envía `POST /bloquear`
- **ENTONCES** MUST responder `200`, fijar `LockoutEnd` futuro y mover al usuario al segmento `bloqueadas`.

#### Scenario: Bloqueo idempotente

- **DADO** un usuario ya bloqueado con `LockoutEnd` vigente
- **CUANDO** un `Administrador` repite `POST /bloquear`
- **ENTONCES** MUST responder `200` consolidando `LockoutEnd` sin duplicar auditoría.

#### Scenario: Bloqueo de inexistente

- **DADO** un `id` que no existe en `AspNetUsers`
- **CUANDO** un `Administrador` envía `POST /bloquear`
- **ENTONCES** MUST responder `404` con código `UsuarioNoEncontrado`.

### Requirement: Desbloqueo de un usuario bloqueado

`POST /api/v1/usuarios/{id}/desbloquear` MUST exigir rol `Administrador` y MUST limpiar `LockoutEnd` (`null`) manteniendo `LockoutEnabled=true`. MUST registrar `Auditoria` con código `DesbloqueoUsuario`.

#### Scenario: Desbloqueo exitoso

- **DADO** un usuario con `LockoutEnd` futuro
- **CUANDO** un `Administrador` envía `POST /desbloquear`
- **ENTONCES** MUST responder `200`, dejar `LockoutEnd=null` y mover al usuario al segmento `activos`.

#### Scenario: Desbloqueo idempotente

- **DADO** un usuario sin `LockoutEnd`
- **CUANDO** un `Administrador` envía `POST /desbloquear`
- **ENTONCES** MUST responder `200` sin nuevos registros de auditoría.

### Requirement: Prohibición de auto-bloqueo

`POST /bloquear` sobre el `id` del `Administrador` autenticado MUST ser rechazado.

#### Scenario: Admin intenta bloquearse

- **DADO** un `Administrador` autenticado
- **CUANDO** envía `POST /bloquear` sobre su propio `id`
- **ENTONCES** MUST responder `403` con código `AutoBloqueo` y `LockoutEnd` MUST permanecer sin cambios.

### Requirement: Segmentación activa vs bloqueada por lockout vigente

El segmento `bloqueadas` MUST incluir a todo usuario con `LockoutEnabled=true` y `LockoutEnd` estrictamente mayor al instante UTC actual, sin importar si el lockout fue administrativo o por intentos fallidos. El segmento `activos` MUST incluir al resto de usuarios existentes.

#### Scenario: Bloqueo temporal por intentos fallidos aparece en bloqueadas

- **DADO** un usuario con lockout automático aplicado por `MaxFailedAccessAttempts`
- **CUANDO** un autenticado consulta `/consulta?status=bloqueadas`
- **ENTONCES** MUST incluirlo en los resultados.

#### Scenario: Vencimiento de lockout temporal reclasifica al usuario

- **DADO** un usuario bloqueado con `LockoutEnd` futuro
- **CUANDO** el instante actual supera `LockoutEnd`
- **ENTONCES** la siguiente consulta MUST reclasificarlo en `activos`.

### Requirement: Rechazo de login con lockout vigente

`AuthServicio.LoginAsync` MUST invocar `UserManager.IsLockedOutAsync(user)` antes de `CheckPasswordAsync` y MUST retornar fallo cuando el lockout esté vigente.

#### Scenario: Credenciales correctas pero lockout vigente

- **DADO** un usuario con `LockoutEnd` futuro
- **CUANDO** envía credenciales válidas a `POST /api/v1/auth/login`
- **ENTONCES** MUST responder `401` con código `UsuarioBloqueado` sin revelar si la contraseña era correcta.

#### Scenario: Credenciales sin lockout permiten acceso

- **DADO** un usuario sin `LockoutEnd` futuro
- **CUANDO** envía credenciales válidas
- **ENTONCES** MUST responder `200` con token JWT.

### Requirement: Auditoría de bloqueos y desbloqueos

Cada `POST /bloquear` y `POST /desbloquear` exitoso MUST registrar una fila en `Auditorias` con `UserId` afectado, operador (`UserId` autenticado) y código `BloqueoUsuario` o `DesbloqueoUsuario`. Acciones rechazadas por auto-bloqueo o `404` MUST NOT generar entrada.
