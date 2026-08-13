# Especificación de Gestión de Puestos

## Propósito

Contrato durable de la API HTTP de `Puestos`: lectura autenticada del catálogo maestro, mutaciones restringidas al rol `Administrador`, y diferenciación 401/403 entre acceso anónimo y autenticado sin permisos. Esta capacidad se mantiene estable frente a cambios futuros de UI, persistencia o integración; los detalles de UI, validaciones de payload y reglas de unicidad viven en capacidades específicas (`puesto-web-listado-detalle-baja`, `puesto-web-crear-editar` y los delta specs que correspondan).

## Requisitos

### Requisito: Autorización de endpoints de puestos

`PuestosController` DEBE requerir autenticación. `GET /api/v1/puestos` y `GET /api/v1/puestos/{id}` DEBEN permitir el acceso a cualquier usuario autenticado y DEBEN responder `2xx` con el contrato de lectura vigente. `POST /api/v1/puestos`, `PUT /api/v1/puestos/{id}`, `DELETE /api/v1/puestos/{id}` y `PATCH /api/v1/puestos/{id}/reactivar` DEBEN requerir el rol `Administrador`; con payload válido y rol correcto, DEBEN conservar sus contratos `2xx` vigentes.

#### Escenario: Lectura autenticada exitosa

- **DADO** un usuario autenticado
- **CUANDO** solicita `GET /api/v1/puestos` o `GET /api/v1/puestos/{id}`
- **ENTONCES** la API DEBE responder `2xx` con el contrato de lectura vigente.

#### Escenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita un `GET` o una mutación de `PuestosController`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

#### Escenario: Mutación protegida por rol administrador

- **DADO** una solicitud válida de mutación sobre puestos
- **CUANDO** la ejecuta un usuario autenticado sin rol `Administrador`
- **ENTONCES** la API DEBE responder `403 Forbidden`
- **Y**, si la ejecuta un `Administrador`, DEBE responder `2xx` con el contrato vigente.

#### Source

- `openspec/changes/archive/2026-07-08-implementa-edicion-puesto-frontend/specs/puesto-management/spec.md:5-26`
- `openspec/changes/archive/2026-07-08-implementa-edicion-puesto-frontend/proposal.md:8,30-34,42`
- `openspec/changes/archive/2026-07-08-implementa-edicion-puesto-frontend/design.md:5,11-12,75-78`

#### Verification

- API (anónimo): `GetAll_WithoutCredentials_ReturnsUnauthorized`, `GetById_WithoutCredentials_ReturnsUnauthorized`, `[Theory] Mutation_WithoutCredentials_ReturnsUnauthorized` (POST/PUT/DELETE/PATCH).
- API (no-admin): `Create/Update/Delete/Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden`.
- API (atributo presente): `Controller_HasAuthorizeAttribute`.
- API (admin sigue verde): los tests `2xx` existentes en `PuestosControllerTests` corren con `factory.CreateAdminClient()` y permanecen `PASS`.

### Requisito: Consulta segmentada paginada de puestos (REQ-PTO-001)

`IPuestoServicioConsulta` e `IPuestoRepository` DEBEN soportar `QueryAsync(PuestoListQuery)` con filtros de segmento (activas/eliminadas), búsqueda LIKE sobre `Codigo`, `Nombre` y `Descripcion`, orden server-side (`codigo_asc`, `codigo_desc`, `nombre_asc`, `nombre_desc`) y paginación con `Skip`/`Take`. El segmento por defecto DEBE ser `Activas`.

#### Escenario: Paginación por defecto Activas

- **DADO** que existen puestos activos y eliminados
- **CUANDO** se consulta sin especificar segmento
- **ENTONCES** el sistema DEBE devolver solo los puestos activos con `TotalCount` correcto.

#### Escenario: Búsqueda con filtros

- **DADO** que existen puestos eliminados cuyo código, nombre o descripción coinciden con un término de búsqueda
- **CUANDO** se consulta con `status=eliminadas`, término de búsqueda, orden explícito y página específica
- **ENTONCES** el sistema DEBE devolver solo coincidencias del segmento Eliminadas, DEBE aplicar orden antes de `Skip`/`Take`, y DEBE retornar `TotalCount` del segmento filtrado.

#### Escenario: Orden explícito

- **DADO** que existen puestos en un segmento
- **CUANDO** se especifica `sort=codigo_desc` o `sort=nombre_asc`
- **ENTONCES** el sistema DEBE aplicar el orden solicitado antes de paginar, y `codigo_asc` DEBE ser el orden por defecto.

#### Source

- `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/specs/puestos-consulta-segmentada/spec.md:9-28`

#### Verification

- Repository: `PuestoRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoActivas_NoIncluyeEliminadas`, `QueryAsync_MySql_SearchFiltraPorCodigo_Nombre_Descripcion`, `QueryAsync_MySql_SortCodigoAsc_AplicaOrdenAntesDePaginar` (`tests/SGV.Tests/Persistencia/PuestoRepositoryQueryAsyncTests.cs`).
- Service: `PuestoServicioConsultaTests.QueryAsync_ConSegmentoActivas_RetornaSoloActivos`, `QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar` (`tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs`).

### Requisito: Endpoint HTTP `GET /api/v1/puestos/consulta` (REQ-PTO-002)

`PuestosController` DEBE exponer `GET /api/v1/puestos/consulta` con `[Authorize]` y parámetros query string `page`, `pageSize`, `search`, `sort` y `status`. DEBE devolver `PagedResult<PuestoDto>`. El endpoint DEBE coexistir con `GET /api/v1/puestos` conservando su forma vigente.

#### Escenario: Sin autenticación devuelve 401

- **DADO** un cliente sin credenciales
- **CUANDO** solicita `GET /api/v1/puestos/consulta`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

#### Escenario: Status eliminadas devuelve solo eliminados

- **DADO** un cliente autenticado
- **CUANDO** solicita `GET /api/v1/puestos/consulta?status=eliminadas&page=1&pageSize=10`
- **ENTONCES** la API DEBE responder `200 OK` con `PagedResult<PuestoDto>` conteniendo solo puestos eliminados.

#### Escenario: Preserva `GET /api/v1/puestos` sin cambios

- **DADO** un consumidor que usa `GET /api/v1/puestos`
- **CUANDO** solicita el listado existente
- **ENTONCES** DEBE recibir `IReadOnlyList<PuestoDto>` sin cambios de forma ni semántica.

#### Source

- `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/specs/puestos-consulta-segmentada/spec.md:30-47`

#### Verification

- API: `GetConsulta_SinStatus_RetornaActivas`, `GetConsulta_ConSearchPageSize_DevuelvePagedResult`, `GetConsulta_WithoutCredentials_ReturnsUnauthorized`, `GetAll_NoModificaShape` (`tests/SGV.Tests/Api/PuestosControllerTests.cs`).
- Authorization: `Controller_HasAuthorizeAttribute` (`tests/SGV.Tests/Api/PuestosControllerTests.cs`).

### Requisito: Baja lógica protegida por ocupaciones vigentes (REQ-PTO-010)

`PuestoServicioComandos.DesactivarAsync` DEBE consultar `IOcupacionRepository.ExistsActiveByPuestoAsync` antes de mutar el Puesto. Si existen ocupaciones vigentes, DEBE retornar `PuestoErrorType.Conflict` con código `PuestoConOcupacionesActivas` y `Categoria=Conflict` explícito. `DELETE /api/v1/puestos/{id}` DEBE devolver `409 Conflict` con `ProblemDetails` en ese caso; preserva `204 NoContent` sin ocupaciones y `404 NotFound` para puesto inexistente.

#### Escenario: Ocupaciones bloquean baja

- **DADO** un puesto activo con al menos una ocupación vigente
- **CUANDO** un administrador solicita `DELETE /api/v1/puestos/{id}`
- **ENTONCES** la API DEBE responder `409 Conflict` con código `PuestoConOcupacionesActivas` en `ProblemDetails.Title`
- **Y** el puesto DEBE permanecer activo.

#### Escenario: Sin ocupaciones se desactiva

- **DADO** un puesto activo sin ocupaciones vigentes
- **CUANDO** un administrador solicita `DELETE /api/v1/puestos/{id}`
- **ENTONCES** la API DEBE responder `204 NoContent`
- **Y** el puesto DEBE quedar inactivo.

#### Escenario: Puesto inexistente devuelve 404

- **DADO** un identificador que no corresponde a un puesto activo
- **CUANDO** un administrador solicita `DELETE /api/v1/puestos/{id}`
- **ENTONCES** la API DEBE responder `404 NotFound`
- **Y** no DEBE modificar ningún puesto.

#### Escenario: Sin autenticación devuelve 401

- **DADO** un cliente sin credenciales
- **CUANDO** solicita `DELETE /api/v1/puestos/{id}`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

#### Source

- `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/specs/puestos-proteccion-baja/spec.md:9-34`

#### Verification

- Command: `DesactivarAsync_ConOcupacionesVigentes_RetornaConflictSinGuardar`, `DesactivarAsync_SinOcupaciones_ProcedeConLaBaja`, `DesactivarAsync_PuestoInexistente_RetornaNoEncontradoYSinGuardar` (`tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioComandosTests.cs`).
- API: `Delete_ConOcupacionesVigentes_Devuelve409ConProblemDetails`, `Delete_SinOcupaciones_Devuelve204NoContent`, `Delete_PuestoInexistente_Devuelve404ConProblemDetails`, `Delete_WithAuthenticatedNonAdmin_ReturnsForbidden` (`tests/SGV.Tests/Api/PuestosControllerTests.cs`).

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

- `openspec/changes/vacante-crear-puestos-libres/specs/puesto-management/spec.md`

#### Verification

- Repository (MySqlFact): los 4 escenarios (con/sin Ocupación vigente) × (con/sin Vacante Abierta) cubren la query con los dos `NOT EXISTS`.
- Service: `PuestoServicioConsultaTests.ListarDisponiblesAsync_*` cubren el mapeo a `PuestoDto`.
- API: `PuestosControllerTests.GetDisponibles_*` cubren 200/401 y shape del contrato.
- Backward compat: `GetAll_NoModificaShape` persiste verde.
