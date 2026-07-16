# Delta para `usuario-web-listado-detalle-baja`

## MODIFIED Requirements

### Requirement: REQ-ULD-02 Listado segmentado server-side con búsqueda y orden

`Index` MUST consumir `GET /api/v1/usuarios/consulta` con `status=activas|bloqueadas`. MUST mostrar `activas` por defecto, preservar `search`/`sort` al alternar, resetear `p=1` y mostrar `UserName`, `Email`, `Nombres`, `Apellidos` y roles. Eliminados físicamente no aparecen.

#### Scenario: Carga inicial en activas

- **DADO** un autenticado abre `Usuarios`
- **CUANDO** la página termina de cargar
- **ENTONCES** MUST renderizar la vista `activas` con datos de `UsuarioDto`.

#### Scenario: Cambio a bloqueadas preserva contexto

- **DADO** un usuario en `activas` con `search` y `sort` aplicados
- **CUANDO** alterna a `bloqueadas`
- **ENTONCES** MUST preservar `search` y `sort` y reiniciar `p=1`.

### Requirement: REQ-ULD-03 Acciones contextuales por segmento y gating admin

En `activas`, MUST exponer `Detalle` a cualquier autenticado, `Editar` solo a `Administrador` y dos acciones independientes: `Bloquear` y `Eliminar` (irreversible). En `bloqueadas`, MUST ocultar `Detalle`/`Editar`/`Crear`/`Bloquear`/`Eliminar` y exponer solo `Desbloquear` a `Administrador`. Las acciones no permitidas sobre uno mismo MUST ocultarse en la propia fila.

#### Scenario: Usuario sin rol admin ve solo lectura

- **DADO** un autenticado sin rol `Administrador`
- **CUANDO** abre `Index` en `activas`
- **ENTONCES** MUST ocultar `Editar`, `Bloquear` y `Eliminar`, conservando `Detalle`.

#### Scenario: Vista bloqueadas solo expone desbloqueo

- **DADO** un autenticado en `status=bloqueadas`
- **CUANDO** la tabla termina de renderizarse
- **ENTONCES** MUST ocultar `Detalle`/`Editar`/`Crear`/`Bloquear`/`Eliminar` y exponer solo `Desbloquear` a `Administrador`.

### Requirement: REQ-ULD-04 Detalle readonly con retorno seguro

El detalle MUST mostrar `UsuarioDto` en solo lectura (incluyendo `Nombres`/`Apellidos`, `Persona` y roles) y ofrecer retorno preservando `p`/`search`/`sort`/`status`. Identificador no consultable MUST producir estado recuperable con retorno claro al listado.

#### Scenario: Detalle existente o no disponible

- **DADO** un usuario activo o bloqueado existente
- **CUANDO** un autenticado abre su detalle
- **ENTONCES** MUST mostrar campos legibles y ofrecer retorno preservando filtros.
- **Y** **DADO** un identificador inexistente o eliminado
- **CUANDO** abre el detalle
- **ENTONCES** MUST mostrar estado recuperable con retorno claro al listado.

### Requirement: REQ-ULD-05 Eliminación física confirmada con modal irreversible

`?handler=Delete` MUST exigir rol `Administrador` (Forbid en caso contrario), MUST invocar `DELETE /api/v1/usuarios/{id}` y MUST requerir confirmación mediante modal irreversible antes de invocar la API. Tras éxito, MUST volver a `activas` con confirmación visible y omitir `LastDeletedId`. Rechazos (`AutoEliminacion`, `UsuarioNoEncontrado`, transporte) MUST traducirse a feedback accionable.

#### Scenario: Eliminación irreversible exitosa

- **DADO** un usuario activo eliminable distinto del autenticado
- **CUANDO** un `Administrador` confirma el modal y la API responde éxito
- **ENTONCES** MUST volver a `activas` con confirmación visible y el usuario MUST dejar de existir.

### Requirement: REQ-ULD-07 Preservación de contexto en PRG

La página MUST preservar `status`, `search`, `sort` y `p` en links, formularios, redirects y `TempData`, junto con `StatusMessage` y `StatusKind`. `LastDeletedId` deja de usarse.

#### Scenario: PRG preserva filtros y TempData

- **DADO** un usuario navega, ordena, busca o ejecuta `Bloquear`/`Desbloquear`/`Delete`
- **CUANDO** la página construye links, hidden inputs y mensajes post-redirect
- **ENTONCES** `status` MUST preservarse en orden, paginación, búsqueda, POSTs y alertas.

## ADDED Requirements

### Requirement: REQ-ULD-08 Acciones Bloquear y Desbloquear con PRG

`?handler=Bloquear` y `?handler=Desbloquear` MUST exigir rol `Administrador`, MUST invocar `POST /api/v1/usuarios/{id}/bloquear` y `POST /api/v1/usuarios/{id}/desbloquear`, MUST redirigir al segmento resultante (`bloqueadas` o `activas`) cuando la operación es exitosa y MUST conservar el segmento con feedback accionable cuando falla (`AutoBloqueo`, `UsuarioNoEncontrado`).

#### Scenario: Bloqueo y desbloqueo exitosos

- **DADO** un usuario distinto del autenticado
- **CUANDO** un `Administrador` confirma `?handler=Bloquear` o `?handler=Desbloquear` y la API responde éxito
- **ENTONCES** MUST redirigir al segmento resultante (`bloqueadas` o `activas`) con confirmación visible.

#### Scenario: Auto-bloqueo rechazado

- **DADO** un `Administrador` autenticado
- **CUANDO** intenta `?handler=Bloquear` sobre sí mismo
- **ENTONCES** el backend MUST rechazarlo con `AutoBloqueo` y la interfaz MUST mostrar feedback accionable.

## REMOVED Requirements

### Requirement: REQ-ULD-06 Reactivación con PRG y feedback de persona inactiva

(Reason: la reactivación lógica se retira junto con el soft-delete; el desbloqueo se cubre en REQ-ULD-08.)
(Migration: `?handler=Reactivate` → `?handler=Desbloquear`; API `POST /desbloquear`.)