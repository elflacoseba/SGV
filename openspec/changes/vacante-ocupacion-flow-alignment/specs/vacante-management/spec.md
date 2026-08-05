# Spec Delta: vacante-management

## Propósito del delta

Alinear el flujo `Puesto → Vacante → Ocupacion` introduciendo validaciones cruzadas en `VacanteServicioComandos`: (N1) rechazar la creación de Vacante cuando el Puesto ya tiene una `Ocupacion` activa, y (N2) crear automáticamente la `Ocupacion` al transicionar una Vacante a `Cubierta`. Se aclara además (N4) que la disponibilidad del Puesto para una nueva Vacante queda condicionada al ciclo de vida de la `Ocupacion` derivada, no sólo al `FechaCierre` de la Vacante.

## Cambios respecto a la spec vigente

### REQUISITOS MODIFICADOS (modified)

#### Requisito: Crear Vacante (MODIFIED)

**Cambio**: se agrega un check de negocio previo a la persistencia: si el `PuestoId` tiene una `Ocupacion` activa (`EsVigente = true`, `IsDeleted = 0`), la creación DEBE rechazarse con `409 Conflict` y código `PuestoOcupado`, antes de evaluar la constraint de BD de vacante abierta.

**Antes**: sólo se validaba `PuestoConVacanteAbierta` (constraint parcial sobre `FechaCierre IS NULL`).

**Ahora**: se valida primero `PuestoOcupado` (Ocupacion activa) y luego `PuestoConVacanteAbierta`.

##### Escenario: Puesto con Ocupacion activa (N1)

- **DADO** que existe una `Ocupacion` con `EsVigente = true` para el `PuestoId` indicado
- **Y** no existe vacante abierta para ese `PuestoId`
- **CUANDO** un usuario con rol `Administrador` o `GestorVacantes` solicita `POST /api/v1/vacantes`
- **ENTONCES** el sistema DEBE responder `409 Conflict` con código `PuestoOcupado`
- **Y** la vacante NO DEBE persistirse.

##### Escenario: Creación exitosa (inalterado)

- **DADO** que no existe vacante abierta ni `Ocupacion` activa para el `PuestoId`
- **Y** `EstadoVacanteId` referencia un estado no terminal
- **CUANDO** un `Administrador` o `GestorVacantes` solicita `POST /api/v1/vacantes`
- **ENTONCES** el sistema DEBE persistir la vacante y responder `201 Created`.

> Los escenarios `PuestoId inexistente`, `EstadoVacanteId inválido`, `Mutación sin permiso`, `Estado inicial terminal rechazado`, `Puesto con vacante abierta` y `Carrera concurrente` se mantienen sin cambios.

#### Requisito: Cambiar estado de Vacante con historial (MODIFIED)

**Cambio**: la transición a `Cubierta` DEBE recibir `PersonaId` (identificador de la Postulación ganadora) y DEBE crear automáticamente una `Ocupacion` vinculada con `VacanteId` seteado y `EsVigente = true`, en la misma transacción que el cambio de estado y el histórico.

**Antes**: la transición a `Cubierta` sólo seteaba `FechaCierre` y registraba `HistorialEstadoVacante`.

**Ahora**: además setea `FechaCierre` + histórico, materializa la `Ocupacion` derivada.

##### Escenario: Transición a Cubierta crea Ocupacion (N2)

- **DADO** una Vacante `Abierta` con una Postulación ganadora cuyo `PersonaId` está disponible
- **CUANDO** un `Administrador` o `GestorVacantes` solicita `PATCH /api/v1/vacantes/{id}/estado` con destino `Cubierta` y `PersonaId`
- **ENTONCES** el sistema DEBE setear `FechaCierre` y registrar `HistorialEstadoVacante`
- **Y** DEBE crear una `Ocupacion` con `VacanteId` igual al id de la Vacante, `PuestoId` igual al de la Vacante, `PersonaId` recibido y `EsVigente = true`
- **Y** la creación de la `Ocupacion` DEBE ser atómica respecto del cambio de estado.

##### Escenario: Cubrir sin PersonaId es rechazado

- **DADO** una Vacante `Abierta`
- **CUANDO** se solicita transición a `Cubierta` sin `PersonaId`
- **ENTONCES** el sistema DEBE responder `400 Bad Request` con `ErrorCategoria.Validation` y `FieldErrors["personaId"]`
- **Y** NO DEBE mutar la Vacante ni crear `Ocupacion`.

##### Escenario: Atomicidad extending a Ocupacion

- **DADO** una transición a `Cubierta` válida
- **CUANDO** la persistencia de la `Ocupacion` derivada falla
- **ENTONCES** el cambio de estado de la Vacante y el histórico DEBEN revertirse (misma transacción).

##### Escenario: Transición a estado no terminal (inalterado)

- Se mantiene el escenario `Transición exitosa a estado no terminal` de la spec vigente.

> Los escenarios `Transición a estado terminal setea FechaCierre` ( Cubierta sin `PersonaId` ahora cubierto arriba), `Estado terminal inmutable` se mantienen; `Atomicidad de vacante e historial` queda absorbida por el escenario de atomicidad extendida.

#### Requisito: Unicidad de vacante abierta por puesto (MODIFIED)

**Cambio**: se aclara que la constraint de BD sigue siendo la fuente de verdad para "una vacante abierta por puesto", pero la **disponibilidad de negocio** del Puesto para una nueva Vacante ahora depende también de la `Ocupacion` derivada (N1): la posición sólo se libera cuando la `Ocupacion` derivada deja de ser activa (`Finalizada` o eliminada lógicamente), no cuando la Vacante transiciona a `Cubierta`.

**Antes**: la posición se liberaba al setear `FechaCierre` (Cubierta/Cancelada).

**Ahora**: `FechaCierre` libera la constraint de BD, pero el check N1 bloquea la nueva creación mientras exista `Ocupacion` activa derivada.

##### Escenario: Cubrir la Vacante no libera la posición

- **DADO** una Vacante `Cubierta` con `Ocupacion` derivada `EsVigente = true`
- **CUANDO** se solicita `POST /api/v1/vacantes` para el mismo `PuestoId`
- **ENTONCES** el sistema DEBE responder `409 Conflict` con código `PuestoOcupado` (N1)
- **Y** la constraint de BD de vacante abierta NO entra en juego porque `FechaCierre` ya está seteada.

##### Escenario: Finalizar la Ocupacion derivada libera la posición

- **DADO** una Vacante `Cubierta` cuya `Ocupacion` derivada fue finalizada (`EsVigente = false`)
- **Y** no existe otra vacante abierta para el `PuestoId`
- **CUANDO** se solicita `POST /api/v1/vacantes` para ese `PuestoId`
- **ENTONCES** el sistema DEBE responder `201 Created`.

> Los escenarios `Una vacante abierta por puesto no viola la constraint`, `Vacante cerrada deja de violar la constraint`, `Vacante soft-deleted deja de violar la constraint` y `Reabrir vacante cerrada con puesto abierto es rechazado por la BD` se mantienen sin cambios.

### REQUISITOS NUEVOS (added)

#### Requisito: Códigos de error de Ocupacion cruzada

El sistema DEBE exponer el código funcional `PuestoOcupado` (Ocupacion activa bloqueando creación de Vacante) en `VacanteErrorCodigo`, distinto de `PuestoConVacanteAbierta` (constraint de BD) y de `OcupacionErrorCodigo.PuestoOcupado` (conflicto de Ocupacion por Puesto ya vigente).

##### Escenario: Discriminación de códigos 409

- **DADO** un Puesto con `Ocupacion` activa y sin vacante abierta
- **CUANDO** se intenta crear una Vacante
- **ENTONCES** el código DEBE ser `PuestoOcupado` (Ocupacion activa), no `PuestoConVacanteAbierta`.

## Escenarios de aceptación

- N1: `POST /api/v1/vacantes` contra Puesto con Ocupacion activa → `409 PuestoOcupado`.
- N2: `PATCH .../estado` a `Cubierta` con `PersonaId` → crea `Ocupacion` con `VacanteId` seteado, `EsVigente=true`, atómico.
- N2 sin `PersonaId` → `400 Validation`.
- N4: nueva `POST` post-`Cubierta` sigue en `409 PuestoOcupado` hasta que la `Ocupacion` se finalice.
- Migración `AddVacanteIdToOcupaciones` idempotente; unique index existente preservado (NULLs múltiples permitidos en MySQL).