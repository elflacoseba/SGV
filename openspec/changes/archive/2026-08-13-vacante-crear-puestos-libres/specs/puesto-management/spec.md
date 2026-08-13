# Delta para `puesto-management`

## ADDED Requirements

### Requisito: Listado de puestos disponibles (REQ-PTO-DISP-001)

`PuestosController` DEBE exponer `GET /api/v1/puestos/disponibles` con `[Authorize]`. La consulta DEBE devolver únicamente Puestos activos (`IsActive = 1`, `IsDeleted = 0`) que NO tengan `Ocupacion` vigente (`IsDeleted = 0` AND `FechaFin IS NULL`) NI `Vacante` abierta (`IsDeleted = 0` AND `FechaCierre IS NULL`). El endpoint DEBE coexistir con `GET /api/v1/puestos`, que conserva su forma y semántica vigente (todos los activos). La definición de "disponible" es **defense-in-depth**: la validación backend N1 (`PuestoOcupado`) y el constraint `ActivePuestoIdUnique` permanecen como fuente de verdad y NO se modifican.

#### Escenario: Endpoint autenticado accesible

- **DADO** un usuario autenticado
- **CUANDO** solicita `GET /api/v1/puestos/disponibles`
- **ENTONCES** la API DEBE responder `2xx` con `IReadOnlyList<PuestoDto>` y shape idéntico a `GET /api/v1/puestos`.

#### Escenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita `GET /api/v1/puestos/disponibles`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

#### Escenario: Excluye puestos soft-deleted o inactivos

- **DADO** puestos con `IsDeleted=1` y/o `IsActive=0` que no tienen Ocupación vigente ni Vacante abierta
- **CUANDO** se consulta `GET /api/v1/puestos/disponibles`
- **ENTONCES** la respuesta NO DEBE incluir ninguno de esos puestos.

#### Escenario: Excluye puestos con Ocupación vigente

- **DADO** un Puesto activo con una `Ocupacion` donde `IsDeleted=0` AND `FechaFin IS NULL`
- **CUANDO** se consulta `GET /api/v1/puestos/disponibles`
- **ENTONCES** la respuesta NO DEBE incluir ese Puesto.

#### Escenario: Excluye puestos con Vacante Abierta

- **DADO** un Puesto activo sin Ocupación vigente pero con una `Vacante` donde `IsDeleted=0` AND `FechaCierre IS NULL`
- **CUANDO** se consulta `GET /api/v1/puestos/disponibles`
- **ENTONCES** la respuesta NO DEBE incluir ese Puesto.

#### Escenario: Caso combinado — Ocupación vigente + Vacante Cubierta queda excluido

- **DADO** un Puesto activo que tiene simultáneamente una `Ocupacion` vigente y una `Vacante` en estado `Cubierta`
- **CUANDO** se consulta `GET /api/v1/puestos/disponibles`
- **ENTONCES** la respuesta NO DEBE incluir ese Puesto
- **Y** el motivo de exclusión es la Ocupación vigente (ambas condiciones se evalúan, basta una para excluir).

#### Escenario: Puesto con Vacante Cubierta y Ocupación derivada finalizada queda INCLUIDO

- **DADO** un Puesto activo cuya `Vacante` Cubierta derivó en una `Ocupacion` con `FechaFin` no nula (finalizada, `IsDeleted=0`)
- **Y** no existe otra Ocupación vigente ni otra Vacante abierta para ese Puesto
- **CUANDO** se consulta `GET /api/v1/puestos/disponibles`
- **ENTONCES** la respuesta DEBE incluir ese Puesto (la posición se libera al finalizar la Ocupación, consistente con N4).

#### Escenario: `GET /api/v1/puestos` sin cambios

- **DADO** un consumidor existente de `GET /api/v1/puestos`
- **CUANDO** solicita el listado vigente
- **ENTONCES** la API DEBE responder `IReadOnlyList<PuestoDto>` con todos los Puestos activos, sin aplicar el filtro de disponibilidad
- **Y** ni la forma ni la semántica del endpoint existente cambian.

#### Source

- `openspec/changes/vacante-crear-puestos-libres/proposal.md:5,28-35,44,88-95`
- `openspec/changes/vacante-crear-puestos-libres/exploration.md:84-95,100`

#### Verification

- Repository (MySqlFact): los 4 escenarios (con/sin Ocupación vigente) × (con/sin Vacante Abierta) cubren la query con los dos `NOT EXISTS`.
- Service: `PuestoServicioConsultaTests.ListarDisponiblesAsync_*` cubren el mapeo a `PuestoDto`.
- API: `PuestosControllerTests.GetDisponibles_*` cubren 200/401 y shape del contrato.
- Backward compat: `GetAll_NoModificaShape` persiste verde.