# Delta Spec: web-ocupaciones-crear-editar — invertir-flujo-cubrir

## MODIFIED Requirements

### Requirement: REQ-OCC-FORM-001 — Crear Ocupación

CUANDO un Administrador abre `Create`, SHALL disponer de `PersonaId`, `PuestoId`, `FechaInicio`, `TipoAsignacion` y `Observaciones`, con selectores alimentados por catálogos existentes.

**Modificación por N3**: el `Create` DEBE verificar que el `PuestoId` seleccionado tenga una Vacante abierta. Si no la tiene, la API responde `409 Conflict` con código `PuestoSinVacanteAbierta` y el formulario DEBE mostrar el conflicto junto al selector `PuestoId`.

**Modificación por `invertir-flujo-cubrir`**: el `Create` ACEPTA un query param opcional `?vacanteId={guid}`. Cuando viene:

- El `PuestoId` se resuelve automáticamente desde la Vacante (vía consulta al cliente de Vacantes) y se bloquea el dropdown (idéntico al comportamiento vigente con `?puestoId={guid}` solo).
- Antes de renderear el form, la página DEBE validar el estado de la Vacante:
  - `Abierta` o `En Selección` → el form se renderea con un hint informativo "Esta Ocupación cubrirá la Vacante del Puesto X."
  - `Cubierta` → NO se renderea el form; se muestra el error **"Esta Vacante ya está cubierta."**
  - `Cancelada` → NO se renderea el form; se muestra el error **"Esta Vacante está cancelada y no puede cubrirse."**
  - Inexistente (404) → NO se renderea el form; se muestra el error **"La Vacante no existe."**
- El action del POST es el mismo; el backend `OcupacionServicioComandos.CrearAsync` decide qué hacer con `VacanteId`. El POST a `/api/v1/ocupaciones` incluye `VacanteId` en el payload.

(Previously: el `Create` sólo soportaba `?puestoId={guid}` y `?personaId`; el flujo de Cubrir dependía de `PATCH /vacantes/{id}/estado` con `PersonaId`, inalcanzable desde el form.)

#### Escenarios

#### Scenario: Alta válida
- GIVEN catálogos cargados y datos válidos y una Vacante abierta para el `PuestoId`
- WHEN se envía el formulario
- THEN SHALL invocar Create y persistir la Ocupación.

#### Scenario: Puesto sin Vacante abierta (N3)
- GIVEN que el `PuestoId` seleccionado no tiene ninguna Vacante abierta
- WHEN se envía el formulario `Create`
- THEN la API SHALL responder `409 Conflict` con código `PuestoSinVacanteAbierta`
- Y el formulario SHALL mostrar el conflicto junto al selector `PuestoId`
- Y NO SHALL mostrar éxito ni perder los demás inputs.

#### Scenario: Catálogo no disponible
- GIVEN falla un catálogo
- WHEN carga Create
- THEN SHALL mostrar estado recuperable e impedir una selección inválida.

#### Scenario: Usuario no-admin
- GIVEN un autenticado sin rol Administrador
- WHEN accede o publica
- THEN SHALL redirigir a 403 o responder `Forbid` sin mutación.

#### Scenario: `?vacanteId` con Vacante Abierta — form rendereado y Puesto bloqueado
- **DADO** query param `?vacanteId={id}` donde la Vacante está `Abierta`
- **CUANDO** el admin entra a `/organizacion/ocupaciones/crear?vacanteId={id}`
- **ENTONCES** el form se renderea con el `PuestoId` precargado desde la Vacante y bloqueado
- **Y** se muestra un hint informativo "Esta Ocupación cubrirá la Vacante del Puesto X." (donde X es el nombre/código del Puesto de la Vacante).

#### Scenario: `?vacanteId` con Vacante Cubierta — error legible
- **DADO** query param `?vacanteId={id}` donde la Vacante está `Cubierta`
- **CUANDO** el admin entra a `/organizacion/ocupaciones/crear?vacanteId={id}`
- **ENTONCES** NO se renderea el form
- **Y** se muestra el mensaje **"Esta Vacante ya está cubierta."**

#### Scenario: `?vacanteId` con Vacante Cancelada — error legible
- **DADO** query param `?vacanteId={id}` donde la Vacante está `Cancelada`
- **CUANDO** el admin entra a `/organizacion/ocupaciones/crear?vacanteId={id}`
- **ENTONCES** NO se renderea el form
- **Y** se muestra el mensaje **"Esta Vacante está cancelada y no puede cubrirse."**

#### Scenario: `?vacanteId` inexistente — error legible
- **DADO** query param `?vacanteId={id}` donde el `GET /api/v1/vacantes/{id}` responde `404`
- **CUANDO** el admin entra a `/organizacion/ocupaciones/crear?vacanteId={id}`
- **ENTONCES** NO se renderea el form
- **Y** se muestra el mensaje **"La Vacante no existe."**

#### Scenario: `?vacanteId` enviado — POST con `VacanteId` y redirect a vacante Details
- **DADO** el form cargado desde `?vacanteId={id}` con datos válidos
- **CUANDO** el admin completa Persona + FechaInicio + TipoAsignación y envía
- **ENTONCES** el POST a `/api/v1/ocupaciones` incluye `VacanteId` en el payload
- **Y** la respuesta redirige al `returnUrl` (típicamente `/organizacion/vacantes/detalles/{vacanteId}`)
- **Y** la Ocupación y la transición de Vacante a `Cubierta` se materializan en la misma transacción (ver REQ-OCC-FORM-010).

### Requirement: REQ-OCC-FORM-009 — Flujo normal documentado

El formulario `Create` SHALL documentar al usuario Administrador que el flujo normal de alta de `Ocupacion` es el automatizado: crear Vacante → transicionar a `Cubierta` (que materializa la `Ocupacion`). El alta manual vía `Create` queda restringida al caso en que el `Puesto` ya tiene Vacante abierta (N3) y representa una excepción operativa, no el camino principal.

**Modificación por `invertir-flujo-cubrir`**: cuando el `Create` se abre con `?vacanteId={guid}`, el hint informativo DEBE mencionar la Vacante: si la Vacante tiene código visible, el texto DEBE incluir "Esta Vacante (código Y) del Puesto X"; si no tiene código visible, DEBE decir "Esta Vacante del Puesto X". El hint DEBE aclarar que al enviar el form la Ocupación creada cubrirá la Vacante y la transicionará a `Cubierta` en la misma transacción.

(Previously: el hint sólo describe el caso de `?puestoId` y el flujo "crear Vacante → transicionar a Cubierta", sin mención de un path directo de cobertura desde el form.)

#### Escenarios

#### Scenario: Hints de flujo en `Create` sin `vacanteId`
- GIVEN un Administrador abriendo `Create` (sin `vacanteId`)
- WHEN se renderiza el formulario
- THEN SHALL mostrar un hint indicando que el alta directa requiere Vacante abierta para el Puesto
- Y SHALL enlazar al módulo de Vacantes para el flujo principal.

#### Scenario: `Create` no sustituye al flujo automatizado
- GIVEN un Puesto sin Vacante abierta
- WHEN el Administrador intenta el alta directa
- THEN SHALL recibir `PuestoSinVacanteAbierta` y ser derivado al flujo Vacante → Cubierta.

#### Scenario: Hint con `vacanteId` y Vacante sin código visible
- **DADO** `?vacanteId={id}` con Vacante `Abierta` cuyo `Puesto` se llama "Desarrollo" y la Vacante NO tiene código visible
- **CUANDO** el admin entra al `Create`
- **ENTONCES** el hint DEBE mencionar "Esta Vacante del Puesto Desarrollo" (sin mención de código)
- **Y** DEBE aclarar que enviar cubrirá la Vacante.

#### Scenario: Hint con `vacanteId` y Vacante con código visible
- **DADO** `?vacanteId={id}` con Vacante `Abierta` con código visible "VAC-2026-01" del Puesto "Desarrollo"
- **CUANDO** el admin entra al `Create`
- **ENTONCES** el hint DEBE mencionar "Esta Vacante (código VAC-2026-01) del Puesto Desarrollo"
- **Y** DEBE aclarar que enviar cubrirá la Vacante.

## ADDED Requirements

### Requirement: REQ-OCC-FORM-010 — Crear Ocupación con `VacanteId` (flujo Cubrir)

`OcupacionServicioComandos.CrearAsync` DEBE aceptar `VacanteId?: Guid` en `CrearOcupacionRequest`. Cuando `VacanteId` está setado, el servicio DEBE, en la misma transacción EF:

- Validar que la Vacante existe — si no, `404 Not Found` con código `VacanteNoEncontrada`.
- Validar que la Vacante está `Abierta` o `En Selección` — si está `Cubierta`, `Cancelada` o terminal, `400 Validation` con código `VacanteNoAbierta`.
- Validar que la Vacante NO tiene ya una `Ocupacion` vigente (`EsVigente=true`, `IsDeleted=0`, `VacanteId` igual) — si existe, `409 Conflict` con código `VacanteYaCubierta`.
- Validar coherencia de `PuestoId`: si el request incluye `PuestoId` explícito, DEBE coincidir con el `PuestoId` de la Vacante — si no coincide, `400 Validation` con código `PuestoIdNoCoincideConVacante`. Si el request omite `PuestoId`, se resuelve desde la Vacante.
- Crear la `Ocupacion` con `VacanteId`, `PuestoId` (de la Vacante o validado), `PersonaId`, `FechaInicio`, `TipoAsignacion`, `EsVigente=true`.
- Transicionar la Vacante a `Cubierta` (set `FechaCierre`, insertar `HistorialEstadoVacante`).
- Atomicidad: si la transición de Vacante falla, la `Ocupacion` NO se persiste.

Si `VacanteId` NO está setado, el comportamiento existente (N3: requerir Vacante abierta para el `PuestoId` indicado) se mantiene sin cambios.

#### Scenarios

#### Scenario: Cubrir Vacante Abierta — happy path transaccional
- **DADO** una Vacante `Abierta` sin `Ocupacion` vigente vinculada
- **CUANDO** se invoca `CrearOcupacion` con `VacanteId` válido, `PersonaId` válido, `FechaInicio`, `TipoAsignacion`
- **ENTONCES** se crea la `Ocupacion` con `VacanteId` setado, `EsVigente=true`
- **Y** la Vacante se transiciona a `Cubierta` (seteo de `FechaCierre` e inserción de `HistorialEstadoVacante`) en la misma transacción
- **Y** la API responde `201 Created` con la Ocupación.

#### Scenario: Cubrir Vacante En Selección — también permitido
- **DADO** una Vacante `En Selección` sin `Ocupacion` vigente vinculada
- **CUANDO** se invoca `CrearOcupacion` con `VacanteId`
- **ENTONCES** la Ocupación se crea y la Vacante pasa a `Cubierta` en la misma transacción.

#### Scenario: Cubrir Vacante ya Cubierta — rechazado
- **DADO** una Vacante `Cubierta`
- **CUANDO** se invoca `CrearOcupacion` con `VacanteId`
- **ENTONCES** la API responde `400 Validation` con código `VacanteNoAbierta`
- **Y** no se crea Ocupación ni muta la Vacante.

#### Scenario: Cubrir Vacante con Ocupación vigente ya existente — conflicto
- **DADO** una Vacante `Abierta` que ya tiene una `Ocupacion` con `EsVigente=true` y `VacanteId` coincidente
- **CUANDO** se invoca `CrearOcupacion` con el mismo `VacanteId`
- **ENTONCES** la API responde `409 Conflict` con código `VacanteYaCubierta`
- **Y** no se crea una segunda Ocupación.

#### Scenario: `PuestoId` del request no coincide con la Vacante — rechazado
- **DADO** una Vacante `Abierta` cuyo `PuestoId` es `P1`
- **CUANDO** se invoca `CrearOcupacion` con `VacanteId` de esa Vacante y `PuestoId=P2` (distinto)
- **ENTONCES** la API responde `400 Validation` con código `PuestoIdNoCoincideConVacante`
- **Y** no se crea Ocupación ni muta la Vacante.

#### Scenario: `VacanteId` inexistente — no encontrado
- **DADO** un `VacanteId` que no resuelve ninguna Vacante
- **CUANDO** se invoca `CrearOcupacion` con ese `VacanteId`
- **ENTONCES** la API responde `404 Not Found` con código `VacanteNoEncontrada`.

#### Scenario: Atomicidad — fallo de transición revierte la Ocupación
- **DADO** una `CrearOcupacion` con `VacanteId` válida donde la transición de Vacante a `Cubierta` falla (p. ej. constraint de BD o error de dominio)
- **CUANDO** la transacción se completa
- **ENTONCES** la `Ocupacion` NO DEBE persistirse
- **Y** la Vacante NO DEBE mutar
- **Y** la API responde con el error funcional apropiado (no `201 Created`).

#### Scenario: `PuestoId` omitido se resuelve desde la Vacante
- **DADO** una Vacante `Abierta` con `PuestoId=P1`
- **CUANDO** se invoca `CrearOcupacion` con `VacanteId` y se omite `PuestoId`
- **ENTONCES** el servicio DEBE resolver `PuestoId=P1` desde la Vacante
- **Y** la Ocupación creada DEBE quedar con `PuestoId=P1`.