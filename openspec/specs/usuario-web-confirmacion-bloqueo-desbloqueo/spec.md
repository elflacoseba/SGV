# Especificación: Confirmación modal obligatoria al bloquear o desbloquear un usuario web

## Propósito

`Index.cshtml` y `Details.cshtml` de `SGV.Web/Pages/Seguridad/Usuarios` exponen acciones
administrativas de `Bloquear` y `Desbloquear` que envían el formulario directo a
`?handler=Bloquear` / `?handler=Desbloquear`. `Eliminar` ya exige un modal irreversible
(`#confirm-delete-modal`, `REQ-ULD-05`); esta spec replica ese mismo patrón UX para
`Bloquear` y `Desbloquear` en ambas vistas, exigiendo un modal de confirmación previo al
POST, sin tocar backend, antifence server-side, PRG, segmentación ni semántica de error
`AutoBloqueo` ya cubiertas por `usuario-lockout-administrativo`, `usuario-web-listado-detalle-baja`
y `identity-user-role-management`.

El slice UX añade dos modales separados (uno por acción), accesibles, que posponen el
submit de cada form diferido hasta confirmación explícita del `Administrador`, conservan
el `@Html.AntiForgeryToken()` y los hidden inputs de contexto (`page`, `search`, `sort`,
`status`) ya existentes, y reutilizan el PRG vigente de `OnPostBloquearAsync` /
`OnPostDesbloquearAsync` (`RedirectToIndex` con TempData de feedback).

## Requisitos

### Requirement: REQ-UCB-01 Confirmación modal al Bloquear desde Index

El click sobre un botón `data-usuario-bloquear-button` en `Index.cshtml` MUST abrir el modal
`#confirm-bloquear-modal` con título "Bloquear usuario" y MUST diferir el submit del form
`data-usuario-bloquear-form` hasta que el `Administrador` confirme con el botón
`[data-usuario-bloquear-confirm]`. Cerrar el modal con `Esc`, clic en backdrop o
`Cancelar` MUST NOT ejecutar el POST; al cerrar sin confirmar, el foco MUST volver al botón
disparador original.

#### Scenario: Confirmar dispara el POST a Bloquear con antiforgery y contexto preservado

- **DADO** un `Administrador` autenticado en `activas` viendo una fila con usuario
  activo distinto de sí mismo
- **CUANDO** hace click en `data-usuario-bloquear-button` y luego click en
  `[data-usuario-bloquear-confirm]`
- **ENTONCES** MUST ejecutarse un único `POST /seguridad/usuarios?handler=Bloquear`
  con el antiforgery token válido y los hidden inputs `id`, `page`, `search`, `sort`,
  `status`
- **Y** el submit MUST respetar el PRG existente (`RedirectToIndex` con `TempData`).

#### Scenario: Cancelar no ejecuta POST

- **DADO** el modal abierto sobre una fila de `activas`
- **CUANDO** el `Administrador` presiona `Cancelar`, `Esc` o el backdrop
- **ENTONCES** MUST NO emitirse ningún `POST`
- **Y** el foco MUST volver al botón `data-usuario-bloquear-button` original.

#### Scenario: Doble click en el botón no dispara dos POST

- **DADO** el botón `data-usuario-bloquear-button` activado
- **CUANDO** se hace doble click rápido antes del cambio de foco
- **ENTONCES** el handler `window.__pendingBloquearTrigger` MUST almacenar una sola
  referencia al form diferido y el modal MUST abrirse una única vez.

### Requirement: REQ-UCB-02 Confirmación modal al Desbloquear desde Index

El click sobre `data-usuario-desbloquear-button` en `Index.cshtml` (segmento `bloqueadas`)
MUST abrir el modal `#confirm-desbloquear-modal` con título "Desbloquear usuario" y aplicar
las mismas reglas y diferimiento que REQ-UCB-01 sobre `data-usuario-desbloquear-form`.

#### Scenario: Confirmar dispara el POST a Desbloquear

- **DADO** un `Administrador` en `bloqueadas` con un usuario distinto de sí mismo
- **CUANDO** confirma el modal de desbloqueo
- **ENTONCES** MUST ejecutarse un único `POST ?handler=Desbloquear` con antiforgery y
  contexto preservados
- **Y** el PRG vigente MUST redirigir a `activas` con feedback visible.

#### Scenario: Cancelar no ejecuta desbloqueo

- **DADO** el modal `#confirm-desbloquear-modal` abierto
- **CUANDO** se cancela por cualquier vía (`Esc`, backdrop, `Cancelar`)
- **ENTONCES** MUST NO emitirse `POST` y el foco MUST volver al disparador.

### Requirement: REQ-UCB-03 Replicar la confirmación en Details.cshtml via partial compartido

`Details.cshtml` MUST diferir los submits de `data-usuario-bloquear-form` y
`data-usuario-desbloquear-form` con los mismos `#confirm-bloquear-modal` y
`#confirm-desbloquear-modal`, instanciados por un partial compartido
`_ConfirmarAccionUsuarioModal.cshtml` ubicado bajo
`src/SGV.Web/Pages/Seguridad/Usuarios/Shared/_ConfirmarAccionUsuarioModal.cshtml`
(o `Pages/Shared/` si el routing del proyecto lo prefiere) para evitar duplicación.

#### Scenario: Details Bloquear exige confirmación

- **DADO** un `Administrador` en `Details` de un usuario activo distinto de sí mismo
- **CUANDO** hace click en `data-usuario-bloquear-form` y confirma el modal
- **ENTONCES** MUST emitirse un único `POST ?handler=Bloquear` con antiforgery y
  contexto preservados.

#### Scenario: Details Desbloquear exige confirmación

- **DADO** un `Administrador` en `Details` de un usuario bloqueado distinto de sí mismo
- **CUANDO** confirma el modal de desbloqueo
- **ENTONCES** MUST emitirse un único `POST ?handler=Desbloquear`.

### Requirement: REQ-UCB-04 Privacidad: sin PII en el cuerpo del modal

Los cuerpos de `#confirm-bloquear-modal` y `#confirm-desbloquear-modal` MUST NOT incluir
`UserName`, `Email`, `Nombres` ni `Apellidos` del usuario objetivo; la confirmación se
reduce a "este usuario", igual que el modal de Eliminar vigente.

#### Scenario: El modal no expone campos personales

- **DADO** una fila renderizada en `activas` con `UserName="jperez"`,
  `Email="jperez@x"`, `Nombres="Juan"`, `Apellidos="Pérez"`
- **CUANDO** se abre el modal desde el botón Bloquear/Desbloquear
- **ENTONCES** el DOM resultante MUST NOT contener las cadenas `jperez`,
  `jperez@x`, `Juan` ni `Pérez`.

### Requirement: REQ-UCB-05 Accesibilidad AA de los modales

Cada modal MUST tener `aria-labelledby` apuntando a su título, `aria-hidden="true"` cuando
está cerrado, MUST cerrarse con la tecla `Esc` y con click sobre el backdrop, MUST
restaurar el foco en el botón disparador al cerrarse y MUST exponer el foco inicial en un
control lógico del modal (el botón de cierre o el `Cancelar`).

#### Scenario: Apertura por teclado y cierre con Esc devuelve foco

- **DADO** el botón disparador enfocado por teclado
- **CUANDO** se pulsa `Enter` para abrir el modal
- **ENTONCES** MUST renderizarse visible con `aria-hidden="false"`
- **Y** `Tab` MUST recorrer los controles en orden lógico (cerrar → cancelar → confirmar)
- **Y** al pulsar `Esc` MUST cerrarse y devolver foco al disparador.

### Requirement: REQ-UCB-06 Antiforgery y PRG preservados

Cada form diferido MUST mantener `@Html.AntiForgeryToken()` y los hidden inputs de
contexto; el submit diferido MUST respetar el PRG existente de `OnPostBloquearAsync` /
`OnPostDesbloquearAsync` (redirect al segmento resultante con `TempData` accionable).

#### Scenario: POST tras confirmar llega al handler con token válido y redirige

- **DADO** el modal confirmado
- **CUANDO** se emite el `POST` diferido
- **ENTONCES** el handler MUST recibir un antiforgery válido
- **Y** MUST ejecutar `RedirectToIndex` con `TempData` de feedback (éxito o error)

### Requirement: REQ-UCB-07 Idempotencia ante doble click

El handler que dispara el submit diferido MUST prevenir POSTs duplicados generados por
doble click en el botón de confirmación del modal (`[data-usuario-bloquear-confirm]` /
`[data-usuario-desbloquear-confirm]`), deshabilitando el botón antes de invocar
`trigger.submit()` y/o limpiando `window.__pending*Trigger`.

#### Scenario: Doble click sobre Confirmar produce un solo POST

- **DADO** el modal abierto con su `Confirmar` habilitado
- **CUANDO** se hace doble click rápido sobre `Confirmar`
- **ENTONCES** el handler backend (`OnPostBloquearAsync` u `OnPostDesbloquearAsync`)
  MUST recibir un único request y la auditoría MUST registrar un solo evento.

### Requirement: REQ-UCB-08 Persistencia de contexto en PRG

Confirmar un modal MUST preservar `status`, `p`, `search` y `sort` ya existentes en los
hidden inputs; el segmento al que se redirige tras éxito MUST ser el consistente con el
handler vigente (`bloqueadas` al bloquear, `activas` al desbloquear).

#### Scenario: Bloquear desde activas preserva filtros y redirige a bloqueadas

- **DADO** un `Administrador` en `activas`, `p=3`, `search="juan"`, `sort="user_asc"`
- **CUANDO** confirma el modal de Bloquear y la API responde éxito
- **ENTONCES** el PRG MUST redirigir a `bloqueadas`, `p=1`, con el resto de filtros
  preservados en hidden inputs del listado resultante y `TempData` de éxito visible.

### Requirement: REQ-UCB-09 No regresión de AutoBloqueo y antifence de UI

El botón Bloquear MUST seguir sin renderizarse para la fila del admin autenticado, igual
que con `EsAutoAccion` actual, y el fence server-side MUST seguir activo como defensa en
profundidad: un POST manual a `?handler=Bloquear` con `id` propio MUST ser rechazado por
`OnPostBloquearAsync` con feedback `AutoBloqueo`.

#### Scenario: Admin no ve su propio botón Bloquear y el fence sigue activo

- **DADO** un `Administrador` que abre `Index` autenticado
- **CUANDO** renderiza su propia fila
- **ENTONCES** MUST NO existir el botón `data-usuario-bloquear-button` para esa fila
- **Y** un `POST` manual a `?handler=Bloquear` con su propio `id` MUST ser rechazado
  por `OnPostBloquearAsync` con feedback `AutoBloqueo` (sin lanzar excepción).

### Requirement: REQ-UCB-10 Tests previos a la implementación (strict_tdd)

Con `strict_tdd: true`, los tests smoke web MUST escribirse antes del código de UI y
cubrir al menos: Index dispara modal Bloquear, Index dispara modal Desbloquear, Details
dispara modal Bloquear, Details dispara modal Desbloquear, no exposición de PII,
accesibilidad AA mínima, e idempotencia ante doble click.

## Decisiones de especificación

- **Open Q1 — Un modal parametrizado vs dos separados.** Se zanja con **dos modales
  separados** (`#confirm-bloquear-modal` y `#confirm-desbloquear-modal`) en línea con
  el patrón vigente de `#confirm-delete-modal` (ya tres acciones, cada una con su
  propio modal). Razones: claridad de copy ("Bloquear usuario" / "Desbloquear
  usuario"), accesibilidad AA más simple al no depender de runtime templating, y
  paridad estructural con la arquitectura de modales vigente.
- **Open Q2 — Variables globales separadas vs mapa indexado.** Se zanja con **dos
  variables globales separadas** (`window.__pendingBloquearTrigger` y
  `window.__pendingDesbloquearTrigger`), replicando el patrón existente de
  `window.__pendingDeleteTrigger`. Razones: minimizar la superficie de cambio,
  coherencia con el código vigente de `Index.cshtml:282-300` y menor riesgo de
  regresión. Un mapa indexado por acción es una evolución razonable pero pertenece a
  una iteración futura fuera del alcance de esta spec.

## Consideraciones fuera de alcance

- Cancelar `Bloquear` o `Desbloquear` por timeout del modal (autocierre). El modal
  actual exige confirmación explícita o `Cancelar`/`Esc`.
- Reemplazar `window.__pending*Trigger` por un mapa o por `Bootstrap.Modal`
  programación imperativa; pertenece a una iteración de refactor posterior.
- Internacionalización del copy del modal (más allá de español vigente).
- Animaciones de entrada/salida personalizadas del modal (se usa el comportamiento
  default de Bootstrap 5).
