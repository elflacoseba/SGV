# Especificación de listado, detalle, baja y reactivación web de usuarios

## Purpose

Definir el slice autenticado de `Usuarios` en `SGV.Web` para listar cuentas activas y bloqueadas de forma segmentada, ver el detalle readonly, ejecutar bloqueo, desbloqueo y eliminación física con PRG — sin expandirse a alta, edición de credenciales ni gestión de roles.

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

### Requirement: REQ-ULD-04 Detalle readonly con persona enriquecida y retorno seguro

El detalle MUST mostrar `UsuarioDto` en solo lectura — incluyendo `Nombres`/`Apellidos`, roles y una card de persona enriquecida que replica el árbol DOM de la card preseleccionada de Editar Usuario (`card border mb-0` con `data-usuario-persona-card`, `card-body`, `dl.row.mb-0` y `dt.col-sm-3`/`dd.col-sm-9`) cuando `IPersonaApiClient.GetByIdAsync` devuelve un `PersonaDto`. La card MUST renderizar `Apellidos`+`Nombres`, `Legajo` opcional, `Documento` (`TipoDocumento NumeroDocumento` vía `FormatDocumento`), `Email`, `Teléfono` y el badge de Estado (`badge-soft-success` cuando `IsActive=true`, `badge-soft-secondary` cuando `IsActive=false`). El `<a href="/personas/detalle/{PersonaId}">` MUST conservarse como título clickable de la card. Cuando `GetByIdAsync` devuelve `null` (404) o lanza `HttpRequestException`, el detalle MUST caer al fallback plano "Apellidos, Nombres" derivado del `UsuarioDto` **sin** marcar `IsNotFound`, **sin** renderizar los botones `Quitar`/`Cambiar` ni el modal `#usuario-persona-buscador-modal`. La vista MUST ofrecer retorno al listado preservando `p`/`search`/`sort`/`status`. Un identificador del usuario no consultable MUST producir estado recuperable con retorno claro al listado.

#### Scenario: Detalle existente muestra campos legibles y retorno preservado

- **DADO** un usuario activo o bloqueado existente
- **CUANDO** un autenticado abre su detalle
- **ENTONCES** MUST mostrarse los campos legibles del `UsuarioDto` en solo lectura
- **Y** MUST ofrecerse retorno al listado preservando `p`/`search`/`sort`/`status`.

#### Scenario: Identificador no consultable produce estado recuperable

- **DADO** un identificador de usuario inexistente o eliminado
- **CUANDO** un autenticado abre su detalle
- **ENTONCES** MUST mostrarse estado recuperable con retorno claro al listado.

#### Scenario: Persona enriquecida visible cuando el API devuelve DTO

- **DADO** un usuario con `PersonaId` válido y `IPersonaApiClient.GetByIdAsync` que devuelve un `PersonaDto` con `Apellidos`, `Nombres`, `Legajo`, `TipoDocumento`, `NumeroDocumento`, `Email`, `Telefono` e `IsActive`
- **CUANDO** el `OnGetAsync` termina y la vista renderiza
- **ENTONCES** la sección "Persona vinculada" MUST renderizar la card enriquecida con los siete campos del DTO
- **Y** MUST aplicarse `data-usuario-persona-card` con `dl.row.mb-0` y los `dt.col-sm-3`/`dd.col-sm-9`
- **Y** MUST renderizarse `badge-soft-success` cuando `IsActive=true` o `badge-soft-secondary` cuando `IsActive=false`
- **Y** el `<a href="/personas/detalle/{PersonaId}">` MUST permanecer como título clickable.

#### Scenario: Fallback plano cuando el API devuelve 404

- **DADO** un usuario con `PersonaId` y `IPersonaApiClient.GetByIdAsync` que devuelve `null` (404)
- **CUANDO** el `OnGetAsync` termina y la vista renderiza
- **ENTONCES** la sección "Persona vinculada" MUST mostrar el texto plano "Apellidos, Nombres" derivado del `UsuarioDto`
- **Y** `IsNotFound` MUST permanecer en `false` (el detalle del usuario se renderiza completo, no el estado recuperable).

#### Scenario: Fallback plano sin IsNotFound ante error de transporte

- **DADO** un usuario con `PersonaId` y `IPersonaApiClient.GetByIdAsync` que lanza `HttpRequestException` u otro error clasificado por `TransportFailureClassifier.IsTransportFailure`
- **CUANDO** el `OnGetAsync` termina y la vista renderiza
- **ENTONCES** `IsNotFound` MUST quedar en `false`
- **Y** la card MUST caer al display plano "Apellidos, Nombres"
- **Y** el detalle MUST renderizarse completo (no el estado recuperable).

#### Scenario: Detalle sin controles de selección de persona

- **DADO** cualquier render del detalle de usuario con o sin persona vinculada
- **CUANDO** la vista termina
- **ENTONCES** la página MUST NOT contener los atributos `data-usuario-persona-quitar` ni `data-usuario-persona-buscar`
- **Y** MUST NOT existir el elemento `#usuario-persona-buscador-modal`.

### Requirement: REQ-ULD-05 Eliminación física confirmada con modal irreversible

`?handler=Delete` MUST exigir rol `Administrador` e invocar `DELETE /api/v1/usuarios/{id}` únicamente después de confirmación vía SweetAlert2. `wireUsuarioDeleteConfirmation` en `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` MUST abrir `Swal.fire` con título `Eliminar usuario`, texto `Esta acción eliminará este usuario de forma permanente. No se puede deshacer.`, icono `warning`, cancelación visible, botones `Eliminar definitivamente`/`Cancelar` y `reverseButtons: true`; MUST enviar solo cuando `result.isConfirmed === true`. Éxitos y rechazos (`AutoEliminacion`, `UsuarioNoEncontrado`, transporte) MUST conservar PRG y feedback accionable.

#### Scenario: Click abre confirmación irreversible

- **DADO** un administrador ante una fila activa ajena
- **CUANDO** pulsa `Eliminar`
- **ENTONCES** MUST abrirse SweetAlert2 con la advertencia y acciones especificadas.

#### Scenario: Confirmar elimina y redirige

- **DADO** la alerta abierta para un usuario eliminable
- **CUANDO** pulsa `Eliminar definitivamente` y la API responde `204`
- **ENTONCES** MUST emitirse un POST `?handler=Delete`, guardarse `TempData` `El usuario se eliminó correctamente.` y redirigirse a `status=activas`.

#### Scenario: Descartar no elimina

- **DADO** la alerta abierta
- **CUANDO** pulsa `Cancelar`, `Esc` o backdrop
- **ENTONCES** MUST NOT enviarse el form ni invocarse la API.

#### Scenario: La fila propia oculta Eliminar

- **DADO** un administrador autenticado
- **CUANDO** se renderiza su fila
- **ENTONCES** `data-usuario-delete-button` MUST NOT existir.

#### Scenario: La confirmación no expone PII

- **DADO** una fila con username, email, nombres y apellidos
- **CUANDO** abre la alerta
- **ENTONCES** título/texto MUST usar solo `este usuario` y la advertencia general.

#### Scenario: AutoEliminacion conserva feedback

- **DADO** un POST manual sobre el usuario autenticado
- **CUANDO** backend rechaza con `403 AutoEliminacion`
- **ENTONCES** el PRG MUST publicar en `TempData` `No puede eliminar su propio usuario.` sin eliminar datos.

### Requirement: REQ-ULD-07 Preservación de contexto en PRG

La página MUST preservar `status`, `search`, `sort` y `p` en links, formularios, redirects y `TempData`, junto con `StatusMessage` y `StatusKind`. `LastDeletedId` deja de usarse.

#### Scenario: PRG preserva filtros y TempData

- **DADO** un usuario navega, ordena, busca o ejecuta `Bloquear`/`Desbloquear`/`Delete`
- **CUANDO** la página construye links, hidden inputs y mensajes post-redirect
- **ENTONCES** `status` MUST preservarse en orden, paginación, búsqueda, POSTs y alertas.

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

## Out of scope

- No incluye alta (`Create`) ni edición (`Edit`) — cubierto por `usuario-web-crear-editar`.
- No incluye cambio de contraseña, lockout/unlock, CRUD de roles ni gestión de sesiones.
