# Especificación: Tests de Concurrencia para Cubrir Vacante

## Propósito

Agregar dos tests `[MySqlFact]` que ejercitan la carrera entre dos coberturas concurrentes para la misma vacante. Cubre la defensa TOCTOU (`ExistsActiveByVacanteAsync` en memoria) y la defensa atómica de la constraint única `IX_Ocupaciones_VacanteIdUnique` (en DB). Decisión D-4.

## Requisitos

### Requisito: Test de carrera TOCTOU para ExistsActiveByVacanteAsync

El test `CubrirVacante_Concurrencia_TOC_TOU_SoloUnaCoberturaExitosa` (en `tests/SGV.Tests/Api/Vacantes/VacantesCubrirConcurrencyTests.cs`) DEBE lanzar dos `POST /api/v1/ocupaciones` con el mismo `VacanteId` en paralelo y verificar que exactamente una termina con `2xx` y la otra con `409 Conflict` cuyo código sea `OcupacionErrorCodigo.VacanteYaCubierta`.

#### Escenario: TOCTOU — una cobertura, una rechazada

- **DADO** una vacante `Abierta` con `VacanteId = V` y dos usuarios con capacidad de crear Ocupaciones
- **Y** ningún test crea previamente una Ocupación para `V`
- **CUANDO** se lanzan 2 `POST /api/v1/ocupaciones` en paralelo con `VacanteId = V`
- **ENTONCES** el test DEBE esperar ambas respuestas
- **Y** exactamente una DEBE responder `2xx` con una `Ocupacion` creada
- **Y** la otra DEBE responder `409 Conflict` con `OcupacionErrorCodigo.VacanteYaCubierta`.

### Requisito: Test de carrera de transición atómica para doble cobertura

El test `CubrirVacante_Concurrencia_DobleCobertura_ConstraintUnica` DEBE lanzar dos operaciones `Cubrir` concurrentes contra la misma vacante y verificar que la segunda encuentra la vacante ya `Cubierta` y es rechazada con `EstadoTerminalInmutable` (409).

#### Escenario: Doble cobertura atómica — la segunda es rechazada por estado terminal

- **DADO** una vacante `Abierta` con `VacanteId = V`
- **CUANDO** se lanzan 2 operaciones `Cubrir` (vía `OcupacionServicioComandos.CrearAsync` con `VacanteId = V`) en paralelo
- **ENTONCES** exactamente una DEBE persistir la Ocupación y la transición a `Cubierta`
- **Y** la otra DEBE fallar con `VacanteErrorCodigo.EstadoTerminalInmutable` (409).

### Requisito: Tests marcados [MySqlFact]

Ambos tests DEBEN estar decorados con `[MySqlFact]` (o `[MySqlTheory]`) para que se skipeen limpiamente cuando MySQL no está disponible, consistente con la convención del resto de la suite `[MySqlFact]`.

#### Escenario: Skip limpio sin MySQL

- **DADO** que no hay MySQL alcanzable en el ambiente
- **CUANDO** se ejecuta `dotnet test SGV.slnx`
- **ENTONCES** estos dos tests DEBEN reportarse como `Skipped` (no `Failed`).
