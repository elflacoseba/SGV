# Especificación: Confirmación modal obligatoria al bloquear o desbloquear un usuario web

## Propósito

`Index.cshtml` y `Details.cshtml` de `SGV.Web/Pages/Seguridad/Usuarios` exponen acciones
administrativas de `Bloquear`, `Desbloquear` y `Eliminar`. `Eliminar` ya exige confirmación
SweetAlert2 (`wireUsuarioDeleteConfirmation`, `REQ-ULD-05`); esta spec replica ese mismo
patrón UX para `Bloquear` y `Desbloquear` en ambas vistas, exigiendo confirmación vía
SweetAlert2 previa al POST, sin tocar backend, antifence server-side, PRG, segmentación
ni semántica de error `AutoBloqueo`, ya cubiertas por `usuario-lockout-administrativo`,
`usuario-web-listado-detalle-baja` e `identity-user-role-management`.

El slice UX expone 3 funciones SweetAlert2 separadas (una por acción) en
`src/SGV.Web/wwwroot/js/pages/usuarios-index.js`: `wireUsuarioBloquearConfirmation`,
`wireUsuarioDesbloquearConfirmation` y `wireUsuarioDeleteConfirmation`. Cada función
difiere el submit de su form hasta confirmación explícita del `Administrador`, conserva
el `@Html.AntiForgeryToken()` y los hidden inputs de contexto (`page`, `search`, `sort`,
`status`) ya existentes, y reutiliza el PRG vigente de `OnPostBloquearAsync` /
`OnPostDesbloquearAsync` (`RedirectToIndex` con TempData de feedback).

## Requisitos

### Requirement: REQ-UCB-01 Confirmación modal al Bloquear desde Index

`wireUsuarioBloquearConfirmation` en `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` MUST abrir
`Swal.fire` con título `Bloquear usuario`, texto `este usuario`, icono `warning`, cancelación,
botones `Bloquear`/`Cancelar` y `reverseButtons: true`; MUST enviar el submit del form
`data-usuario-bloquear-form` solo si `result.isConfirmed === true`. Cerrar con `Esc`, clic en
backdrop o `Cancelar` MUST NOT ejecutar el POST.

#### Scenario: Confirmar bloquea

- **DADO** un administrador en `activas` y una fila ajena
- **CUANDO** abre la alerta y pulsa `Bloquear`
- **ENTONCES** MUST emitirse un POST `?handler=Bloquear` con antiforgery/contexto y PRG con feedback.

#### Scenario: Cancelar no bloquea

- **DADO** la alerta abierta
- **CUANDO** pulsa `Cancelar`, `Esc` o backdrop
- **ENTONCES** MUST NOT enviarse el form ni redirigirse.

#### Scenario: Doble click no duplica

- **DADO** una confirmación pendiente
- **CUANDO** repite rápidamente el click
- **ENTONCES** MUST existir una alerta activa y como máximo un POST.

### Requirement: REQ-UCB-02 Confirmación modal al Desbloquear desde Index

`wireUsuarioDesbloquearConfirmation` en `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` MUST usar
igual configuración que REQ-UCB-01 con título `Desbloquear usuario`, texto `este usuario`,
botón `Desbloquear` y `customClass.confirmButton: 'btn btn-success'`; MUST enviar solo si
`result.isConfirmed === true`.

#### Scenario: Confirmar desbloquea

- **DADO** un administrador en `bloqueadas` y una fila ajena
- **CUANDO** confirma `Desbloquear`
- **ENTONCES** MUST emitirse un POST `?handler=Desbloquear`; el PRG MUST volver a `activas` con feedback.

#### Scenario: Cancelar no desbloquea

- **DADO** la alerta abierta
- **CUANDO** la descarta por botón, `Esc` o backdrop
- **ENTONCES** MUST NOT emitirse POST.

### Requirement: REQ-UCB-03 Replicar la confirmación en Details.cshtml

`Details.cshtml` MUST reutilizar el wiring SweetAlert2 de `usuarios-index.js` y MUST NOT
depender de ningún partial Bootstrap. El bootstrap del script (autoinvocado cuando hay DOM
+ `window.Swal`) registra handlers sobre el `document` y cada `wire*Confirmation` hace
early-return si no encuentra el form correspondiente, por lo que es idempotente aunque
Details solo renderice un subset de los forms.

#### Scenario: Details confirma Bloquear

- **DADO** el detalle activo de un usuario ajeno
- **CUANDO** confirma `Bloquear`
- **ENTONCES** MUST emitirse un POST `?handler=Bloquear`.

#### Scenario: Details confirma Desbloquear

- **DADO** el detalle bloqueado de un usuario ajeno
- **CUANDO** confirma `Desbloquear`
- **ENTONCES** MUST emitirse un POST `?handler=Desbloquear`.

### Requirement: REQ-UCB-04 Privacidad: sin PII en el cuerpo del modal

Las alertas SweetAlert2 MUST usar solo `este usuario` en el texto; MUST NOT interpolar
`UserName`, `Email`, `Nombres` ni `Apellidos` del usuario objetivo.

#### Scenario: Sin PII

- **DADO** Juan Pérez, `jperez@x`
- **CUANDO** abre Bloquear o Desbloquear
- **ENTONCES** título/texto MUST NOT contener esos datos.

### Requirement: REQ-UCB-05 Accesibilidad AA de los modales

SweetAlert2 MUST ofrecer nombre accesible (título como `aria-label`), teclado (`Esc` cierra),
cierre con backdrop y restauración del foco al disparador al cerrarse. `focusCancel: true`
MUST exponer el foco inicial en `Cancelar` (control lógico primario).

#### Scenario: Teclado y foco

- **DADO** el disparador enfocado
- **CUANDO** abre con `Enter` y descarta con `Esc`
- **ENTONCES** MUST cerrarse sin POST y restaurar el foco.

### Requirement: REQ-UCB-06 Antiforgery y PRG preservados

La migración a SweetAlert2 MUST preservar `@Html.AntiForgeryToken()` y los hidden inputs de
contexto; cada submit diferido (via `form.requestSubmit(button)` o `form.submit()` fallback)
MUST respetar el PRG existente de `OnPostBloquearAsync` / `OnPostDesbloquearAsync`.

#### Scenario: Antiforgery y redirect

- **DADO** una confirmación aceptada
- **CUANDO** se envía el form
- **ENTONCES** MUST validar antiforgery y redirigir con `TempData`.

### Requirement: REQ-UCB-07 Idempotencia ante doble click

El wiring MUST impedir submits duplicados durante confirmación o navegación. SweetAlert2 v11
no encola alerts — si la alerta ya está abierta, un segundo click se ignora.
Backend: la lógica de auditoría no permite dobles transiciones (`Bloqueado=true → Bloqueado=true`
es no-op).

#### Scenario: Doble confirmación, un POST

- **DADO** una operación confirmada
- **CUANDO** ocurren clicks repetidos
- **ENTONCES** backend/auditoría MUST observar una operación.

### Requirement: REQ-UCB-08 Persistencia de contexto en PRG

El submit MUST preservar `status`, `p`, `search` y `sort` via hidden inputs; Bloquear MUST
redirigir a `bloqueadas` y Desbloquear a `activas`.

#### Scenario: Contexto preservado

- **DADO** `p=3`, `search=juan`, `sort=user_asc`
- **CUANDO** confirma Bloquear con éxito
- **ENTONCES** MUST mostrar `bloqueadas`, `p=1`, filtros y feedback.

### Requirement: REQ-UCB-09 No regresión de AutoBloqueo y antifence de UI

La fila propia MUST NOT renderizar Bloquear/Desbloquear (guard `if (!esAuto)` envuelve los
forms); el auto-bloqueo manual via POST MUST rechazarse por `OnPostBloquearAsync` con
feedback `AutoBloqueo`. El cause-root RIS-002 (siembra manual de `NameIdentifier`) está
corregido: `CurrentUserId` retorna el GUID real del JWT, no `UserNameOrEmail`.

#### Scenario: Autoacción bloqueada

- **DADO** un administrador en su fila
- **CUANDO** renderiza acciones o fuerza el POST
- **ENTONCES** botones MUST NOT existir y el POST MUST rechazarse con feedback `AutoBloqueo`.

### Requirement: REQ-UCB-10 Tests previos a la implementación (strict_tdd)

Tests MUST preceder al código y cubrir wiring Index/Details, confirmación, descarte,
PII, accesibilidad, autoacción e idempotencia. El harness JS ejercita las 3 funciones
SweetAlert2 con subprocess Node real y mock de `Swal`, probando confirmado, cancelado,
`Esc` y backdrop.

## Decisiones de especificación

- **SweetAlert2 por acción separada**: 3 funciones independientes
  (`wireUsuarioBloquearConfirmation`, `wireUsuarioDesbloquearConfirmation`,
  `wireUsuarioDeleteConfirmation`) en `usuarios-index.js`, espejo estructural de
  `cargos-index.js`. Cada función difiere el submit de su form `data-usuario-*-form`
  solo si `result.isConfirmed === true`. `focusCancel: true` y
  `showCloseButton: false` para accesibilidad.
- **Preservación de contrato wire**: los atributos `data-usuario-*-form` y
  `data-usuario-*-button` se conservan como contratos de tests. El submit usa
  `form.requestSubmit(button)` (preserva antiforgery + hidden inputs).

## Consideraciones fuera de alcance

- Cancelar `Bloquear` o `Desbloquear` por timeout de la alerta (autocierre). La alerta
  actual exige confirmación explícita o `Cancelar`/`Esc`.
- Internacionalización del copy de SweetAlert2 (más allá de español vigente).
- Animaciones personalizadas de SweetAlert2 (se usa el comportamiento default v11).
