# Delta: Confirmación de bloqueo y desbloqueo con SweetAlert2

## MODIFIED Requirements

### Requirement: REQ-UCB-01 Confirmación modal al Bloquear desde Index (MODIFIED)

`wireUsuarioBloquearConfirmation` en `src/SGV.Web/wwwroot/js/pages/usuarios-index.js:1` MUST abrir `Swal.fire` con título `Bloquear usuario`, texto `este usuario`, icono `warning`, cancelación, botones `Bloquear`/`Cancelar` y `reverseButtons: true`; MUST enviar solo si `result.isConfirmed === true`.

(Previously: confirmación Bootstrap.)

#### Scenario: Confirmar bloquea
**DADO** un administrador en `activas` y una fila ajena
**CUANDO** abre la alerta y pulsa `Bloquear`
**ENTONCES** MUST emitirse un POST `?handler=Bloquear` con antiforgery/contexto y PRG con feedback.

#### Scenario: Cancelar no bloquea
**DADO** la alerta abierta
**CUANDO** pulsa `Cancelar`, `Esc` o backdrop
**ENTONCES** MUST NOT enviarse el form ni redirigirse.

#### Scenario: Doble click no duplica
**DADO** una confirmación pendiente
**CUANDO** repite rápidamente el click
**ENTONCES** MUST existir una alerta activa y como máximo un POST.

### Requirement: REQ-UCB-02 Confirmación modal al Desbloquear desde Index (MODIFIED)

`wireUsuarioDesbloquearConfirmation` MUST usar igual configuración con título `Desbloquear usuario`, texto `este usuario` y botón `Desbloquear`.

(Previously: confirmación Bootstrap.)

#### Scenario: Confirmar desbloquea
**DADO** un administrador en `bloqueadas` y una fila ajena
**CUANDO** confirma `Desbloquear`
**ENTONCES** MUST emitirse un POST `?handler=Desbloquear`; el PRG MUST volver a `activas` con feedback.

#### Scenario: Cancelar no desbloquea
**DADO** la alerta abierta
**CUANDO** la descarta por botón, `Esc` o backdrop
**ENTONCES** MUST NOT emitirse POST.

### Requirement: REQ-UCB-03 Replicar la confirmación en Details.cshtml via partial compartido (MODIFIED)

`src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml:107-150` MUST reutilizar el wiring SweetAlert2 y MUST NOT depender del partial Bootstrap.

(Previously: partial Bootstrap.)

#### Scenario: Details confirma Bloquear
**DADO** el detalle activo de un usuario ajeno
**CUANDO** confirma `Bloquear`
**ENTONCES** MUST emitirse un POST `?handler=Bloquear`.

#### Scenario: Details confirma Desbloquear
**DADO** el detalle bloqueado de un usuario ajeno
**CUANDO** confirma `Desbloquear`
**ENTONCES** MUST emitirse un POST `?handler=Desbloquear`.

### Requirement: REQ-UCB-04 Privacidad: sin PII en el cuerpo del modal (MODIFIED)

Las alertas MUST usar solo `este usuario`; MUST NOT interpolar username, email, nombres ni apellidos.

(Previously: HTML Bootstrap.)

#### Scenario: Sin PII
**DADO** Juan Pérez, `jperez@x`
**CUANDO** abre Bloquear o Desbloquear
**ENTONCES** título/texto MUST NOT contener esos datos.

### Requirement: REQ-UCB-05 Accesibilidad AA de los modales (MODIFIED)

SweetAlert2 MUST ofrecer nombre accesible, teclado, cierre con `Esc`/backdrop y restauración del foco.

(Previously: accesibilidad Bootstrap.)

#### Scenario: Teclado y foco
**DADO** el disparador enfocado
**CUANDO** abre con `Enter` y descarta con `Esc`
**ENTONCES** MUST cerrarse sin POST y restaurar el foco.

### Requirement: REQ-UCB-06 Antiforgery y PRG preservados (MODIFIED)

La migración MUST preservar token, hidden inputs y handlers PRG.

(Previously: submit Bootstrap.)

#### Scenario: Antiforgery y redirect
**DADO** una confirmación aceptada
**CUANDO** se envía el form
**ENTONCES** MUST validar antiforgery y redirigir con `TempData`.

### Requirement: REQ-UCB-07 Idempotencia ante doble click (MODIFIED)

El wiring MUST impedir submits duplicados durante confirmación o navegación.

(Previously: guard Bootstrap.)

#### Scenario: Doble confirmación, un POST
**DADO** una operación confirmada
**CUANDO** ocurren clicks repetidos
**ENTONCES** backend/auditoría MUST observar una operación.

### Requirement: REQ-UCB-08 Persistencia de contexto en PRG (MODIFIED)

El submit MUST preservar `status`, `p`, `search`, `sort`; Bloquear MUST ir a `bloqueadas` y Desbloquear a `activas`.

(Previously: contexto Bootstrap.)

#### Scenario: Contexto preservado
**DADO** `p=3`, `search=juan`, `sort=user_asc`
**CUANDO** confirma Bloquear con éxito
**ENTONCES** MUST mostrar `bloqueadas`, `p=1`, filtros y feedback.

### Requirement: REQ-UCB-09 No regresión de AutoBloqueo y antifence de UI (MODIFIED)

La fila propia MUST NOT renderizar Bloquear/Desbloquear; el auto-bloqueo manual MUST rechazarse con `AutoBloqueo`.

(Previously: autoacción fallaba por RIS-002.)

#### Scenario: Autoacción bloqueada
**DADO** un administrador en su fila
**CUANDO** renderiza acciones o fuerza el POST
**ENTONCES** botones MUST NOT existir y el POST MUST rechazarse con feedback `AutoBloqueo`.

### Requirement: REQ-UCB-10 Tests previos a la implementación (strict_tdd) (MODIFIED)

Tests MUST preceder al código y cubrir wiring Index/Details, confirmación, descarte, PII, accesibilidad, autoacción e idempotencia.

(Previously: tests Bootstrap.)

#### Scenario: Harness SweetAlert2
**DADO** tests JS/smoke inicialmente rojos
**CUANDO** ejercitan confirmado, cancelado y descartado
**ENTONCES** MUST probarse submit solo al confirmar y ausencia de IDs Bootstrap.
