# Especificación de listado, detalle, baja y reactivación web de usuarios

## Purpose

Definir el slice autenticado de `Usuarios` en `SGV.Web` para listar cuentas activas y eliminadas de forma segmentada, ver el detalle readonly, ejecutar baja lógica y reactivar con PRG — sin expandirse a alta, edición de credenciales ni gestión de roles.

## Requirements

### Requirement: REQ-ULD-01 Acceso autenticado al módulo de usuarios

`SGV.Web` MUST exponer el módulo `Seguridad/Usuarios` solo a usuarios autenticados; los anónimos MUST ser redirigidos a `/auth/sign-in`.

#### Scenario: Usuario autenticado abre el módulo

- **DADO** un usuario autenticado en la shell
- **CUANDO** accede al módulo `Usuarios`
- **ENTONCES** la aplicación MUST responder con el listado dentro del shell autenticado.

#### Scenario: Usuario anónimo intenta acceder

- **DADO** un usuario no autenticado
- **CUANDO** solicita `/seguridad/usuarios` o un detalle
- **ENTONCES** MUST redirigirlo a `/auth/sign-in`.

### Requirement: REQ-ULD-02 Listado segmentado server-side con búsqueda y orden

`Index` MUST consumir `GET /api/v1/usuarios/consulta` con `status=activas|eliminadas`. MUST mostrar `activas` por defecto, MUST preservar `search` y `sort` al alternar y resetear `p=1`. La grilla MUST mostrar `UserName`, `Email`, `Nombres`, `Apellidos` y roles por fila.

#### Scenario: Carga inicial en activas

- **DADO** un autenticado abre `Usuarios`
- **CUANDO** la página termina de cargar
- **ENTONCES** MUST renderizar la vista `activas` por defecto con datos de `UsuarioDto` (incluyendo `Nombres`/`Apellidos` y roles).

#### Scenario: Cambio a eliminadas preserva contexto

- **DADO** un usuario en `activas` con `search` y `sort` aplicados
- **CUANDO** alterna a `eliminadas`
- **ENTONCES** MUST preservar `search` y `sort`
- **Y** MUST reiniciar `p` a `1`.

#### Scenario: Búsqueda sin coincidencias

- **DADO** un segmento válido cargado
- **CUANDO** la búsqueda no devuelve coincidencias
- **ENTONCES** MUST mostrar estado vacío entendible y mantener visible el selector de segmento.

### Requirement: REQ-ULD-03 Acciones contextuales por segmento y gating admin

En `activas`, MUST exponer `Detalle` a cualquier autenticado y `Editar`/`Eliminar` solo a `Administrador`. En `eliminadas`, MUST ocultar `Detalle`/`Editar`/`Eliminar` y MUST exponer solo `Reactivar` a `Administrador`.

#### Scenario: Usuario sin rol admin ve solo lectura

- **DADO** un autenticado sin rol `Administrador`
- **CUANDO** abre `Index` en `activas`
- **ENTONCES** MUST ocultar `Editar` y `Eliminar`
- **Y** MUST conservar `Detalle` por fila.

#### Scenario: Vista eliminadas solo expone reactivación

- **DADO** un autenticado en `status=eliminadas`
- **CUANDO** la tabla termina de renderizarse
- **ENTONCES** MUST ocultar `Detalle`, `Editar`, `Crear` y `Eliminar`
- **Y** MUST exponer `Reactivar` por fila solo para `Administrador`.

### Requirement: REQ-ULD-04 Detalle readonly con retorno seguro

El detalle MUST mostrar `UsuarioDto` en modo solo lectura —incluyendo `Nombres`/`Apellidos`, `Persona` vinculada y roles— y MUST ofrecer retorno al listado preservando `p`/`search`/`sort`/`status`. Un identificador no consultable MUST producir estado recuperable con retorno claro al listado.

#### Scenario: Detalle existente muestra datos readonly

- **DADO** un usuario activo existente
- **CUANDO** un autenticado abre su detalle
- **ENTONCES** MUST mostrar todos los campos legibles en solo lectura
- **Y** MUST ofrecer retorno al listado preservando filtros.

#### Scenario: Detalle no disponible

- **DADO** un identificador no consultable
- **CUANDO** el usuario abre el detalle
- **ENTONCES** MUST mostrar estado recuperable
- **Y** MUST ofrecer retorno claro al listado.

### Requirement: REQ-ULD-05 Baja lógica confirmada con feedback

`?handler=Delete` MUST exigir rol `Administrador` (Forbid en caso contrario), MUST invocar `DELETE /api/v1/usuarios/{id}`, MUST registrar `LastDeletedId` en `TempData` desde `activas` y MUST traducir rechazos por conflicto a feedback accionable.

#### Scenario: Baja lógica exitosa

- **DADO** un usuario activo eliminable
- **CUANDO** un `Administrador` confirma la baja y la API responde éxito
- **ENTONCES** MUST volver al listado `activas` con confirmación visible
- **Y** el usuario MUST dejar de verse en `activas`.

#### Scenario: Baja rechazada por conflicto

- **DADO** un usuario cuya baja es rechazada (p. ej. por dependencias)
- **CUANDO** un `Administrador` confirma la baja
- **ENTONCES** MUST mostrar feedback claro del conflicto
- **Y** el usuario MUST permanecer visible en `activas`.

#### Scenario: Auto-baja prohibida

- **DADO** un `Administrador` con sesión iniciada
- **CUANDO** intenta ejecutar su propia baja desde el listado
- **ENTONCES** el backend MUST rechazarlo
- **Y** la interfaz MUST mostrar feedback accionable de auto-baja no permitida.

### Requirement: REQ-ULD-06 Reactivación con PRG y feedback de persona inactiva

`?handler=Reactivate` MUST exigir rol `Administrador`, MUST invocar `PATCH /api/v1/usuarios/{id}/reactivar`, MUST redirigir a `activas` cuando la operación es exitosa y MUST conservar `eliminadas` con feedback accionable cuando falla (incluido el caso `PersonaInactiva`).

#### Scenario: Reactivación exitosa vuelve a activas

- **DADO** un usuario eliminado visible en `eliminadas`
- **CUANDO** un `Administrador` confirma `?handler=Reactivate` y la API responde éxito
- **ENTONCES** MUST redirigir a `activas` con confirmación visible
- **Y** MUST limpiar el CTA rápido de `LastDeletedId`.

#### Scenario: Reactivación fallida por Persona inactiva

- **DADO** un usuario eliminado cuya `Persona` vinculada está `IsDeleted=1`
- **CUANDO** un `Administrador` confirma `?handler=Reactivate`
- **ENTONCES** MUST permanecer en `eliminadas`
- **Y** MUST mostrar banner claro con código `PersonaInactiva` y acción sugerida.

### Requirement: REQ-ULD-07 Preservación de contexto en PRG

La página MUST preservar `status`, `search`, `sort` y `p` en links, formularios, redirects y `TempData`, junto con `StatusMessage`, `StatusKind` y `LastDeletedId`.

#### Scenario: PRG preserva filtros y TempData

- **DADO** un usuario navega, ordena, busca o ejecuta `Delete`/`Reactivate`
- **CUANDO** la página construye links, hidden inputs y mensajes post-redirect
- **ENTONCES** `status` MUST preservarse en orden, paginación, búsqueda, POSTs y alertas.

## Out of scope

- No incluye alta (`Create`) ni edición (`Edit`) — cubierto por `usuario-web-crear-editar`.
- No incluye cambio de contraseña, lockout/unlock, CRUD de roles ni gestión de sesiones.
