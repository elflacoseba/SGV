# Delta for vacante-management

## Purpose

Cerrar la ventana TOCTOU de la regla "una sola vacante abierta por puesto" (`VacanteServicioComandos.CrearAsync`) con defense-in-depth en BD: unique constraint parcial sobre `PuestoId` filtrado por `FechaCierre IS NULL` y `IsDeleted = 0`. El pre-check `ExistsAbiertaByPuestoAsync` se conserva; la BD es fuente de verdad ante carrera. Cierra la desviación D-3.2 del change archivado `2026-07-30-feature-implementar-modulo-vacantes`.

## ADDED Requirements

### Requirement: Unicidad de vacante abierta por puesto (defense-in-depth en BD)

El sistema DEBE garantizar, mediante unique constraint parcial en BD sobre `PuestoId` filtrado por `FechaCierre IS NULL` y `IsDeleted = 0`, que nunca coexistan dos vacantes abiertas para el mismo puesto. La columna calculada que soporta la constraint DEBE evaluar a `NULL` para vacantes cerradas o soft-deleted, de modo que MySQL las ignore del unique index.

#### Scenario: Una vacante abierta por puesto no viola la constraint

- **DADO** que no existe vacante abierta para un `PuestoId`
- **CUANDO** se persiste una nueva vacante abierta para ese puesto
- **ENTONCES** la constraint DEBE aceptar la inserción sin regresión observable.

#### Scenario: Vacante cerrada deja de violar la constraint

- **DADO** una vacante abierta con `FechaCierre IS NULL` para un `PuestoId`
- **CUANDO** se transiciona a estado terminal seteando `FechaCierre`
- **ENTONCES** la columna calculada DEBE evaluar a `NULL`
- **Y** DEBE ser posible abrir una nueva vacante para el mismo `PuestoId`.

#### Scenario: Vacante soft-deleted deja de violar la constraint

- **DADO** una vacante abierta con `IsDeleted = 0` para un `PuestoId`
- **CUANDO** se elimina lógicamente seteando `IsDeleted = 1`
- **ENTONCES** la columna calculada DEBE evaluar a `NULL`
- **Y** DEBE ser posible abrir una nueva vacante para el mismo `PuestoId`.

#### Scenario: Reabrir vacante cerrada con puesto abierto es rechazado por la BD

- **DADO** una vacante cerrada para un `PuestoId` que ya posee otra vacante abierta
- **CUANDO** por error se reabre seteando `FechaCierre = NULL`
- **ENTONCES** la BD DEBE rechazar por violación de unique constraint
- **Y** el rechazo DEBE mapearse a `VacanteErrorCodigo.PuestoConVacanteAbierta`.

## MODIFIED Requirements

### Requirement: Crear Vacante

El sistema DEBE permitir abrir una vacante indicando `PuestoId`, `EstadoVacanteId` (inicial), `FechaApertura`, `Motivo` y opcionalmente `Observaciones`. Solo el rol `Administrador` o `GestorVacantes` DEBE poder invocar la mutación. El `EstadoVacanteId` inicial NO DEBE referenciar un estado terminal (`EsTerminal = true`); si lo hace, la operación DEBE rechazarse con `400 Bad Request`. La unicidad "una sola vacante abierta por puesto" DEBE reforzarse con unique constraint parcial en BD; ante carrera concurrente la BD es fuente de verdad, y la aplicación DEBE mapear la `DbUpdateException` de constraint violation al código `VacanteErrorCodigo.PuestoConVacanteAbierta` respondiendo `409 Conflict`.

(Previously: la unicidad se verificaba únicamente en la capa de aplicación vía `ExistsAbiertaByPuestoAsync`, aceptando la ventana TOCTOU de la desviación D-3.2. La constraint en BD agrega defense-in-depth.)

#### Escenario: Creación exitosa

- **DADO** que no existe vacante abierta para el `PuestoId` indicado
- **Y** `EstadoVacanteId` referencia un estado de vacante existente no terminal
- **CUANDO** un usuario con rol `Administrador` o `GestorVacantes` solicita `POST /api/v1/vacantes`
- **ENTONCES** el sistema DEBE responder `201 Created` con el `VacanteDto` creado.

#### Escenario: PuestoId inexistente

- **DADO** que no existe un Puesto con el `PuestoId` proporcionado
- **CUANDO** se solicita crear la vacante
- **ENTONCES** el sistema DEBE rechazar con `400 Bad Request` y `ErrorCategoria.ValidationError`.

#### Escenario: EstadoVacanteId inválido

- **DADO** que `EstadoVacanteId` no referencia un estado sembrado
- **CUANDO** se solicita crear la vacante
- **ENTONCES** el sistema DEBE rechazar con error de validación.

#### Escenario: Mutación sin permiso

- **DADO** un usuario autenticado sin rol `Administrador` ni `GestorVacantes`
- **CUANDO** solicita `POST /api/v1/vacantes`
- **ENTONCES** la API DEBE responder `403 Forbidden`.

#### Escenario: Estado inicial terminal rechazado

- **DADO** que el `EstadoVacanteId` referencia un estado con `EsTerminal = true` (Cubierta o Cancelada)
- **CUANDO** un usuario con rol `Administrador` o `GestorVacantes` solicita `POST /api/v1/vacantes`
- **ENTONCES** el sistema DEBE responder `400 Bad Request`
- **Y** DEBE incluir `ErrorCategoria.Validation` y código `VacanteErrorCodigo.EstadoTerminalInmutable`
- **Y** la vacante NO DEBE persistirse.

#### Escenario: Puesto con vacante abierta

- **DADO** que ya existe una vacante abierta (`FechaCierre IS NULL`, `IsDeleted = 0`) para el `PuestoId`
- **CUANDO** se solicita `POST /api/v1/vacantes` para el mismo `PuestoId`
- **ENTONCES** el sistema DEBE responder `409 Conflict` con código `VacanteErrorCodigo.PuestoConVacanteAbierta`
- **Y** la nueva vacante NO DEBE persistirse.

#### Escenario: Carrera concurrente para el mismo PuestoId

- **DADO** que no existe vacante abierta para un `PuestoId`
- **CUANDO** dos solicitudes `POST /api/v1/vacantes` concurren simultáneamente para ese `PuestoId`
- **ENTONCES** exactamente una DEBE recibir `201 Created`
- **Y** la otra DEBE recibir `409 Conflict` con código `VacanteErrorCodigo.PuestoConVacanteAbierta`
- **Y** NO DEBE persistirse más de una vacante abierta para ese `PuestoId`.