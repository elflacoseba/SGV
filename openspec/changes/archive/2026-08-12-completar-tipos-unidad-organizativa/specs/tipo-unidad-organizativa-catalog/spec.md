# Delta para tipo-unidad-organizativa-catalog

**Change:** `completar-tipos-unidad-organizativa`
**Modo:** hybrid (OpenSpec + Engram) | `strict_tdd: true`

## ADDED Requirements

### REQ-TUO-007 — Migraciones de seed forward-only y append-only

Las migraciones que materializan filas en `TiposUnidadOrganizativa` MUST ser forward-only y append-only. El método `Down()` de toda migración de seed del catálogo MUST lanzar `NotSupportedException` y MUST NOT emitir `DELETE` ni `UPDATE` sobre filas seed existentes. Una migración de seed MUST NOT alterar las filas insertadas por migraciones anteriores del mismo catálogo. El catálogo solo evoluciona mediante nuevas migraciones que insertan filas adicionales.

#### Scenario: Down no soportado

- **GIVEN** la migración `CompletarTiposUnidadOrganizativaSeed` fue aplicada
- **WHEN** se invoca su método `Down()`
- **THEN** MUST lanzar `NotSupportedException`
- **AND** ninguna fila de `TiposUnidadOrganizativa` se elimina ni modifica.

#### Scenario: No altera filas sembradas previamente

- **GIVEN** la base ya contiene los 7 tipos sembrados por la migración anterior (`SemillaTipoUnidadOrganizativa`)
- **WHEN** se aplica `CompletarTiposUnidadOrganizativaSeed`
- **THEN** los 7 tipos previos conservan `Id`, `Codigo` y `Nombre` intactos
- **AND** únicamente se insertan los 13 tipos faltantes.

## MODIFIED Requirements

### REQ-TUO-001 — Inmutabilidad del catálogo y seed completo vía migración

El catálogo `TipoUnidadOrganizativa` MUST ser inmutable en runtime. El sistema MUST NOT exponer endpoints de escritura (`POST`, `PUT`, `PATCH`, `DELETE`) sobre `/api/v1/tipos-unidad-organizativa` ni sobre recursos individuales. El catálogo se siembra exclusivamente por migraciones EF Core con constantes `Guid` estáticas; cualquier modificación requiere una nueva migración. Tras aplicar todas las migraciones pendientes vía `Database.Migrate()`, la tabla `TiposUnidadOrganizativa` MUST contener exactamente 20 filas, tanto en bases de datos nuevas como en existentes, y las migraciones de seed MUST ser idempotentes respecto a `Migrate()`.
(Previously: el requisito afirmaba 20 filas vía seed runtime/DatosSemilla sin distinguir el camino `EnsureCreated()` del camino de producción `Database.Migrate()`; la delta extiende la aserción a `Migrate()` y agrega garantías idempotencia/forward-only.)

#### Scenario: Seed crea 20 tipos estáticos

- **GIVEN** la tabla `TiposUnidadOrganizativa` está vacía
- **WHEN** se ejecuta la migración contra una base nueva
- **THEN** existen exactamente 20 filas
- **AND** cada fila tiene `Id`, `Codigo` y `Nombre` tomados de las constantes estáticas declaradas en la migración y en `DatosSemilla.cs`
- **AND** los 20 códigos son `Area`, `Celula`, `Coordinacion`, `Departamento`, `Direccion`, `Division`, `Equipo`, `Escuela`, `Facultad`, `Gerencia`, `Institucion`, `Oficina`, `Planta`, `Region`, `Secretaria`, `Seccion`, `Sede`, `Subgerencia`, `Sucursal`, `Vicepresidencia`.

#### Scenario: Migrate produce 20 filas en base existente con 7 tipos

- **GIVEN** una base de datos existente ya sembró 7 tipos y aún no aplicó `CompletarTiposUnidadOrganizativaSeed`
- **WHEN** se ejecuta `Database.Migrate()` aplicando las migraciones pendientes
- **THEN** la tabla `TiposUnidadOrganizativa` MUST contener exactamente 20 filas
- **AND** los 13 tipos nuevos tienen los `Id` estáticos declarados en `TipoUnidadOrganizativaConstantes`
- **AND** ningún `Id` se duplica entre la migración previa y la nueva.

#### Scenario: Migrate es idempotente en reaplicación

- **GIVEN** `CompletarTiposUnidadOrganizativaSeed` ya está registrada en `__EFMigrationsHistory`
- **WHEN** se invoca `Database.Migrate()` nuevamente
- **THEN** la migración no se reejecuta
- **AND** la tabla conserva exactamente 20 filas sin duplicados ni errores.

#### Scenario: No se exponen endpoints de escritura

- **GIVEN** la API está corriendo
- **WHEN** cualquier cliente intenta `POST`, `PUT`, `PATCH` o `DELETE` sobre `/api/v1/tipos-unidad-organizativa` o `/api/v1/tipos-unidad-organizativa/{id:guid}`
- **THEN** la respuesta es `405 Method Not Allowed` o `404 Not Found`
- **AND** ninguna fila de `TiposUnidadOrganizativa` se inserta, modifica o elimina.

### REQ-TUO-002 — Listado completo anclado a estado post-migración

El sistema MUST exponer `GET /api/v1/tipos-unidad-organizativa` que retorna todos los tipos del catálogo. El endpoint MUST requerir autenticación mediante `[Authorize]` a nivel clase (con `FallbackPolicy.RequireAuthenticatedUser` como red de seguridad); el cliente sin credenciales MUST recibir `401 Unauthorized`; el cliente autenticado MUST recibir `200 OK` con el contrato vigente. La aserción de 20 elementos está anclada al estado post-aplicación de las migraciones de seed del catálogo, no al estado real de la tabla en un momento arbitrario.
(Previously: el escenario "Retorna lista completa" asumía 20 filas sembradas sin anclar el origen a `Database.Migrate()`; la delta ata explícitamente la aserción al estado post-migración.)

#### Scenario: Retorna lista completa

- **GIVEN** todas las migraciones pendientes fueron aplicadas y existen 20 tipos sembrados en `TiposUnidadOrganizativa`
- **WHEN** un cliente autenticado llama `GET /api/v1/tipos-unidad-organizativa`
- **THEN** la respuesta es `200 OK`
- **AND** el body es un array JSON de 20 elementos
- **Y** cada elemento tiene solo los campos `id`, `codigo`, `nombre`.

#### Scenario: Base de datos vacía (pre-migración)

- **GIVEN** la tabla `TiposUnidadOrganizativa` no tiene filas y ninguna migración de seed ha aplicado
- **WHEN** un cliente autenticado llama `GET /api/v1/tipos-unidad-organizativa`
- **THEN** la respuesta es `200 OK`
- **AND** el body es un array JSON vacío `[]` (no `404 Not Found`).