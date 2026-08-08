# Spec Delta: web-ocupaciones-crear-editar

## Propósito del delta

Reflejar en la shell web las nuevas reglas del flujo `Puesto → Vacante → Ocupacion`: (N3) el alta directa de `Ocupacion` desde el formulario `Create` queda restringida a Puestos con Vacante abierta, y (Q2) la reactivación de una `Ocupacion` debe rechazarse si su `Vacante` vinculada está `Cancelada`. Se documenta además el nuevo flujo normal: crear Vacante → transicionar a `Cubierta` → la `Ocupacion` aparece automáticamente; el alta directa desde el formulario `Create` deja de ser el camino principal y queda sujeta a N3.

## Cambios respecto a la spec vigente

### REQUISITOS MODIFICADOS (modified)

#### Requisito: REQ-OCC-FORM-001 — Crear Ocupación (MODIFIED)

**Cambio**: el `Create` ahora debe esperar que el `PuestoId` seleccionado tenga una Vacante abierta. Si no la tiene, la API responde `409 Conflict` con código `PuestoSinVacanteAbierta` y el formulario debe mostrar el conflicto sin perder datos.

**Antes**: el alta directa sólo validaba `PersonaYPuestoOcupados` y `PuestoOcupado` (409 de unicidad).

**Ahora**: previo a la unicidad, el `PuestoId` debe pasar el check `ExistsAbiertaByPuestoAsync`; si falla, se devuelve `PuestoSinVacanteAbierta`.

##### Escenario: Alta válida con Vacante abierta (inalterado)

- **DADO** catálogos cargados, datos válidos y una Vacante abierta para el `PuestoId`
- **CUANDO** se envía el formulario
- **ENTONCES** SHALL invocar Create y persistir la Ocupación.

##### Escenario: Puesto sin Vacante abierta (N3)

- **DADO** que el `PuestoId` seleccionado no tiene ninguna Vacante abierta
- **CUANDO** se envía el formulario `Create`
- **ENTONCES** la API SHALL responder `409 Conflict` con código `PuestoSinVacanteAbierta`
- **Y** el formulario SHALL mostrar el conflicto junto al selector `PuestoId`
- **Y** NO SHALL mostrar éxito ni perder los demás inputs.

##### Escenario: Catálogo no disponible (inalterado)

- Se mantiene el escenario vigente.

##### Escenario: Usuario no-admin (inalterado)

- Se mantiene el escenario vigente (403 / `Forbid`).

#### Requisito: REQ-OCC-FORM-008 — Reactivación con colisión explícita (MODIFIED)

**Cambio**: la reactivación debe rechazarse también cuando la `Vacante` vinculada a la `Ocupacion` está `Cancelada`, además de las colisiones de unicidad existentes.

**Antes**: sólo se trataban `PersonaYPuestoOcupados` y `PuestoOcupado` (409 unicidad).

**Ahora**: si la `Ocupacion` tiene `VacanteId` no nulo y esa `Vacante` está `Cancelada`, la reactivación se rechaza con `409 Conflict`.

##### Escenario: Reactivación válida (inalterado)

- **DADO** no existen colisiones vigentes y la `Vacante` vinculada (si existe) NO está `Cancelada`
- **CUANDO** se reactiva
- **ENTONCES** SHALL quedar `Vigente` tras PRG.

##### Escenario: Colisión del par (inalterado)

- Se mantiene el escenario `Colisión del par` (`PersonaYPuestoOcupados`).

##### Escenario: Colisión del Puesto (inalterado)

- Se mantiene el escenario `Colisión del Puesto` (`PuestoOcupado`).

##### Escenario: Vacante Cancelada bloquea reactivación (Q2)

- **DADO** una `Ocupacion` cuya `Vacante` vinculada (mismo `VacanteId`) está en estado `Cancelada`
- **CUANDO** se confirma Reactivar
- **ENTONCES** la API SHALL responder `409 Conflict`
- **Y** Details SHALL mostrar el conflicto manteniendo el estado histórico
- **Y** NO SHALL mutar la `Ocupacion`.

#### Requisito: REQ-OCC-FORM-005 — Conflictos de unicidad visibles (MODIFIED)

**Cambio**: se agrega `PuestoSinVacanteAbierta` como tercer código 409 visible en `Create`, además de `PersonaYPuestoOcupados` y `PuestoOcupado`.

**Antes**: dos códigos 409 distinguibles.

**Ahora**: tres códigos 409 distinguibles.

##### Escenario: Puesto sin vacante abierta (N3)

- **DADO** el `PuestoId` no tiene Vacante abierta
- **CUANDO** se intenta guardar el `Create`
- **ENTONCES** SHALL mostrar el conflicto `PuestoSinVacanteAbierta` junto al selector `PuestoId`.

##### Escenario: Sin falso éxito (extensible)

- **DADO** cualquiera de los 409 (`PersonaYPuestoOcupados`, `PuestoOcupado`, `PuestoSinVacanteAbierta`)
- **CUANDO** se re-renderiza
- **ENTONCES** SHALL conservar datos y no mostrar éxito.

> Los escenarios `Persona y Puesto duplicados` y `Puesto ocupado` se mantienen sin cambios.

### REQUISITOS NUEVOS (added)

#### Requisito: REQ-OCC-FORM-009 — Flujo normal documentado

El formulario `Create` SHALL documentar al usuario Administrador que el flujo normal de alta de `Ocupacion` es el automatizado: crear Vacante → transicionar a `Cubierta` (que materializa la `Ocupacion`). El alta manual vía `Create` queda restringida al caso en que el `Puesto` ya tiene Vacante abierta (N3) y representa una excepción operativa, no el camino principal.

##### Escenario: Hints de flujo en `Create`

- **DADO** un Administrador abriendo `Create`
- **CUANDO** se renderiza el formulario
- **ENTONCES** SHALL mostrar un hint indicando que el alta directa requiere Vacante abierta para el Puesto
- **Y** SHALL enlazar al módulo de Vacantes para el flujo principal.

##### Escenario: `Create` no sustituye al flujo automatizado

- **DADO** un Puesto sin Vacante abierta
- **CUANDO** el Administrador intenta el alta directa
- **ENTONCES** SHALL recibir `PuestoSinVacanteAbierta` y ser derivado al flujo Vacante → Cubierta.

## Escenarios de aceptación

- N3: `POST /api/v1/ocupaciones` desde el `Create` web contra Puesto sin Vacante abierta → `409 PuestoSinVacanteAbierta` visible en el formulario.
- Q2: `PATCH .../reactivar` de `Ocupacion` con `Vacante` `Cancelada` → `409` visible en Details, sin mutación.
- Hint de flujo normal presente en `Create` (Vacante → Cubierta → Ocupacion automática).
- `OcupacionServicioComandosTests` (línea 47-64) adaptado: el test de `CrearAsync` directo espera `409 PuestoSinVacanteAbierta`.