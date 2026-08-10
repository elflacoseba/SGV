# Delta Spec: vacante-management — invertir-flujo-cubrir

## MODIFIED Requirements

### Requisito: Cambiar estado de Vacante con historial

El sistema DEBE permitir transicionar el `EstadoVacanteId` persistiendo simultáneamente un registro en `HistorialEstadoVacante`. La transición a estado terminal (`Cubierta`, `Cancelada`) DEBE setear `FechaCierre` automáticamente. `Motivo` es OPCIONAL (PB-3).

**Regla N2 — Cubrir es responsabilidad de la creación de Ocupación ( reposición del flujo)**: la transición a `Cubierta` vía `PATCH /api/v1/vacantes/{id}/estado` NO existe como operación de mutación. El endpoint DEBE rechazarla con `400 Validation`, código funcional `PersonaIdRequeridoParaCubrir` y mensaje de orientación al usuario: **"Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada."** La creación de la Ocupación y el cambio de estado a `Cubierta` se materializan en `OcupacionServicioComandos.CrearAsync` cuando el request incluye `VacanteId` (ver spec `web-ocupaciones-crear-editar`, REQ-OCC-FORM-010). El campo `CambiarEstadoVacanteRequest.PersonaId` queda deprecado y se ignora en el path Cubierta.

(Previously: la transición a `Cubierta` vía `PATCH` creaba automáticamente una `Ocupacion` derivada en la misma transacción, exigiendo `PersonaId` en el request — path inalcanzable desde el frontend actual que no expone `PersonaId`.)

#### Escenario: Transición exitosa a estado no terminal

- **DADO** una vacante en estado `Abierta`
- **CUANDO** un `Administrador` o `GestorVacantes` solicita `PATCH /api/v1/vacantes/{id}/estado` con `EstadoVacanteId=EnSeleccion`
- **ENTONCES** el sistema DEBE persistir el nuevo estado
- **Y** DEBE insertar un registro en `HistorialEstadoVacante`
- **Y** `FechaCierre` DEBE permanecer nula.

#### Escenario: Transición a Cubierta vía PATCH es rechazada y deriva al flujo de Ocupación (N2 invertido)

- **DADO** una Vacante `Abierta`
- **CUANDO** un `Administrador` o `GestorVacantes` solicita `PATCH /api/v1/vacantes/{id}/estado` con `EstadoVacanteId=Cubierta`
- **ENTONCES** el sistema DEBE responder `400 Bad Request` con `ErrorCategoria.Validation`
- **Y** DEBE incluir código funcional `PersonaIdRequeridoParaCubrir`
- **Y** DEBE poblar el mensaje **"Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada."**
- **Y** NO DEBE mutar la Vacante, ni crear `Ocupacion`, ni insertar `HistorialEstadoVacante`.

#### Escenario: Transición a Cubierta vía PATCH envía PersonaId — se ignora y se rechaza igual (deprecación)

- **DADO** una Vacante `Abierta`
- **CUANDO** se solicita `PATCH /api/v1/vacantes/{id}/estado` con `EstadoVacanteId=Cubierta` y `PersonaId` populado
- **ENTONCES** el sistema DEBE responder `400 Validation` con código `PersonaIdRequeridoParaCubrir`
- **Y** el `PersonaId` provisto DEBE ser ignorado sin efecto
- **Y** el mensaje DEBE ser idéntico al del rechazo sin `PersonaId`.

#### Escenario: Transición a estado terminal setea FechaCierre

- **DADO** una vacante abierta
- **CUANDO** se solicita cambiar a `Cancelada` sin `Motivo` (PB-3 asumido opcional)
- **ENTONCES** el sistema DEBE setear `FechaCierre`
- **Y** DEBE registrar el histórico.

#### Escenario: Estado terminal inmutable

- **DADO** una vacante en estado `Cubierta`
- **CUANDO** se solicita cambiar su estado
- **ENTONCES** el sistema DEBE rechazar la operación con `400 Validation` y código `EstadoTerminalInmutable`.

## ADDED Requirements

### Requisito: Detalle de Vacante expone Ocupación derivada

`VacanteDetailDto` DEBE incluir dos campos opcionales que reflejan el estado de cobertura de la Vacante:

- `OcupacionDerivadaId?: Guid` — identificador de la `Ocupacion` vigente (`EsVigente=true`, `IsDeleted=0`) con `VacanteId` igual al id de la Vacante. `null` si no existe.
- `PersonaAsignadaNombre?: string` — nombre/denominación de la `Persona` asignada en esa `Ocupacion` derivada. `null` si no existe `OcupacionDerivadaId`.

El endpoint `GET /api/v1/vacantes/{id}` DEBE hidratar estos campos con una consulta de join a `Ocupaciones` filtrando por vigencia y `VacanteId`. La hidratación DEBE ser defensiva: un estado inconsistente (Vacante `Cubierta` sin `Ocupacion` derivada) DEBE resultar en `OcupacionDerivadaId = null` y `PersonaAsignadaNombre = null`, sin lanzar excepción.

#### Escenario: Detalle de Vacante Cubierta con Ocupación derivada

- **DADO** una Vacante `Cubierta` cuya `Ocupacion` derivada (`EsVigente=true`, `VacanteId=id`) existe y referencia una `Persona` con nombre "Juan Pérez"
- **CUANDO** se invoca `GET /api/v1/vacantes/{id}`
- **ENTONCES** la respuesta DEBE incluir `OcupacionDerivadaId` distinto de `null` e igual al id de la `Ocupacion`
- **Y** `PersonaAsignadaNombre` DEBE ser `"Juan Pérez"`.

#### Escenario: Detalle de Vacante Abierta sin cobertura

- **DADO** una Vacante `Abierta` (sin `Ocupacion` vigente vinculada)
- **CUANDO** se invoca `GET /api/v1/vacantes/{id}`
- **ENTONCES** la respuesta DEBE incluir `OcupacionDerivadaId = null`
- **Y** `PersonaAsignadaNombre` DEBE ser `null`.

#### Escenario: Detalle defensivo de Vacante Cubierta sin Ocupación derivada (estado inconsistente)

- **DADO** una Vacante en estado `Cubierta` pero sin `Ocupacion` vigente con `VacanteId` coincidente (inconsistencia que no debería ocurrir)
- **CUANDO** se invoca `GET /api/v1/vacantes/{id}`
- **ENTONCES** la respuesta DEBE incluir `OcupacionDerivadaId = null` y `PersonaAsignadaNombre = null`
- **Y** el endpoint NO DEBE lanzar excepción.

### Requisito: Atomicidad de la operación Cubrir via `OcupacionServicioComandos.CrearAsync`

La operación de Cubrir una Vacante vive en `OcupacionServicioComandos.CrearAsync` cuando el request incluye `VacanteId`. La creación de la `Ocupacion` y la transición de la Vacante a `Cubierta` (con `HistorialEstadoVacante` y `FechaCierre`) DEBEN ejecutarse en la misma transacción EF. Si la transición de la Vacante falla, la `Ocupacion` NO DEBE persistirse. (Cobertura detallada de escenarios en `web-ocupaciones-crear-editar`, REQ-OCC-FORM-010.)

(Nota: este requisito se declara aquí por su dominio de datos, pero los escenarios operativos se especifican en `web-ocupaciones-crear-editar` para evitar duplicación.)