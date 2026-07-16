# Especificación: Eliminación física de cuentas de usuario

## Propósito

Definir la operación de borrado físico de la cuenta Identity de un usuario del SGV —incluyendo sus relaciones técnicas en `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins` y `AspNetUserTokens`— preservando intactos los datos de la `Persona` asociada y el historial de `Auditorias`. La capacidad se aplica a cuentas activas y bloqueadas; nunca a uno mismo.

## Requisitos

### Requirement: Eliminación física de la cuenta Identity

`DELETE /api/v1/usuarios/{id}` MUST exigir rol `Administrador` y MUST eliminar físicamente la fila correspondiente en `AspNetUsers` mediante `UserManager.DeleteAsync`, junto con todas sus filas asociadas en `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins` y `AspNetUserTokens`. La `Persona` vinculada y los registros de `Auditorias` que la referencian MUST permanecer inalterados.

#### Scenario: Eliminación física exitosa sobre cuenta activa

- **DADO** un usuario activo y su `Persona` asociada existente
- **CUANDO** un `Administrador` envía `DELETE /api/v1/usuarios/{id}` con credenciales válidas
- **ENTONCES** MUST responder `200`
- **Y** la fila de `AspNetUsers` MUST dejar de existir
- **Y** la `Persona` y las `Auditorias` MUST persistir sin cambios.

#### Scenario: Eliminación física exitosa sobre cuenta bloqueada

- **DADO** un usuario con `LockoutEnd` vigente (bloqueado)
- **CUANDO** un `Administrador` envía `DELETE`
- **ENTONCES** MUST responder `200` y la fila MUST eliminarse, sin requerir desbloqueo previo.

#### Scenario: Auto-eliminación prohibida

- **DADO** un `Administrador` autenticado cuyo `id` coincide con el objetivo
- **CUANDO** intenta ejecutar `DELETE` sobre sí mismo
- **ENTONCES** MUST responder `403 Forbidden` con código `AutoEliminacion`
- **Y** la fila MUST permanecer intacta.

#### Scenario: Eliminación sobre usuario inexistente

- **DADO** un `id` que no existe en `AspNetUsers`
- **CUANDO** un `Administrador` envía `DELETE`
- **ENTONCES** MUST responder `404 Not Found` con código `UsuarioNoEncontrado`.

### Requirement: Cascadas técnicas y conservación de negocio

La eliminación MUST propagarse automáticamente a las tablas técnicas de Identity mediante FK con `ON DELETE CASCADE`. La tabla `Auditorias` MUST NOT tener FK enforced hacia `AspNetUsers` para preservar el historial aún cuando la cuenta ya no exista.

#### Scenario: Relaciones técnicas purgadas tras delete

- **DADO** un usuario con roles, claims y tokens asociados
- **CUANDO** se completa `DELETE`
- **ENTONCES** las filas en `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins` y `AspNetUserTokens` MUST haberse purgado.

#### Scenario: Persona y Auditoria sobreviven al delete

- **DADO** un usuario con `Persona` vinculada y al menos una `Auditoria` registrada
- **CUANDO** se completa `DELETE`
- **ENTONCES** la fila de `Personas` MUST continuar existiendo
- **Y** las filas de `Auditorias` que referencian al `UserId` MUST permanecer consultables.

### Requirement: Usuarios eliminados fuera de toda consulta operativa

Un usuario eliminado físicamente MUST NOT aparecer en `GET /api/v1/usuarios/consulta` para ningún segmento (`activos` ni `bloqueadas`), MUST NOT ser accesible por `GET /api/v1/usuarios/{id}` (responde `404`) y MUST NOT permitir `POST /bloquear` ni `POST /desbloquear`.

#### Scenario: Listado no incluye usuarios eliminados físicamente

- **DADO** un usuario eliminado físicamente
- **CUANDO** cualquier autenticado consulta `/consulta` con cualquier `status`
- **ENTONCES** MUST responder `200` sin incluir al usuario eliminado.

#### Scenario: Detalle y comandos sobre id eliminado devuelven 404

- **DADO** un `id` previamente eliminado
- **CUANDO** un `Administrador` invoca `GET /{id}`, `POST /bloquear` o `POST /desbloquear`
- **ENTONCES** MUST responder `404 Not Found` con `UsuarioNoEncontrado`.

### Requirement: Idempotencia y errores de la eliminación

Eliminar dos veces el mismo `id` MUST producir la segunda respuesta `404` sin efectos colaterales. Errores de transporte recuperables (timeouts, 5xx transitorios) MUST generar `ErrorCategoria.Transport` con código legible y reintento seguro desde la UI.

#### Scenario: Doble delete deja estado consistente

- **DADO** un usuario recién eliminado
- **CUANDO** un `Administrador` repite `DELETE` con el mismo `id`
- **ENTONCES** MUST responder `404` y MUST no afectar otras tablas.

#### Scenario: Falla transitoria de transporte

- **DADO** un `DELETE` que sufre timeout recuperable
- **CUANDO** el cliente reintenta
- **ENTONCES** la respuesta MUST tiparse como `ErrorCategoria.Transport` con un código accionable y sin marcar al usuario como eliminado si la transacción no commiteó.