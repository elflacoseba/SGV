# Delta Spec — Catálogo UO/Cargo en Edit (Puestos, #120)

## Purpose

`Edit.cshtml.cs` (Puestos) invoca `IUnidadOrganizativaApiClient.QueryAsync(... pageSize=200 ...)` y `ICargoApiClient.GetAllAsync(...)` para poblar `UnidadOrganizativaOptions` y `CargoOptions`. Estos selects **NO** se renderizan en `_Form.cshtml` cuando `IsEdit` (campos inmutables), por lo que la carga es dead code que arrastra el artefacto histórico `pageSize=200` y produce round-trips sin valor. La fix elimina la carga UO y Cargo en `Edit` y conserva únicamente `PuestoSuperiorOptions`, cuyo dropdown sí se renderiza en Create y Edit. Adicionalmente, documenta el patrón "catálogo completo" vs "listado paginado" para UO, fijando la decisión locked #3 del change de puestos.

## ADDED Requirements

### Requirement: Edit no carga catálogo de UnidadOrganizativa

El PageModel de `Puestos/Edit` MUST NOT invocar `IUnidadOrganizativaApiClient.QueryAsync(...)` ni `IUnidadOrganizativaApiClient.GetAllActivasAsync(...)` durante un GET. La propiedad `UnidadOrganizativaOptions` MUST inicializarse como lista vacía en cada GET.

#### Scenario: GET a Edit con id válido
- GIVEN un usuario autenticado con rol `Administrador` y un id de puesto existente
- WHEN navega a `editar/{id}`
- THEN `UnidadOrganizativaOptions` MUST ser `[]`
- AND `QueryCalls.Count == 0` y `GetAllActivasCalls.Count == 0` en `FakeUnidadOrganizativaApiClient`.

#### Scenario: HTML no renderiza select de UO en Edit
- GIVEN el HTML resultante de `editar/{id}`
- WHEN se inspecciona `_Form.cshtml` con `IsEdit == true`
- THEN MUST NOT existir un `<select>` enlazado a `UnidadOrganizativaId`.

### Requirement: Edit no carga catálogo de Cargo

El PageModel de `Puestos/Edit` MUST NOT invocar `ICargoApiClient.GetAllAsync(...)` durante un GET. La propiedad `CargoOptions` MUST inicializarse como lista vacía en cada GET.

#### Scenario: GET a Edit con id válido
- GIVEN un usuario autenticado con rol `Administrador` y un id de puesto existente
- WHEN navega a `editar/{id}`
- THEN `CargoOptions` MUST ser `[]`
- AND `GetAllCalls.Count == 0` en `FakeCargoApiClient`.

#### Scenario: HTML no renderiza select de Cargo en Edit
- GIVEN el HTML resultante de `editar/{id}`
- WHEN se inspecciona `_Form.cshtml` con `IsEdit == true`
- THEN MUST NOT existir un `<select>` enlazado a `CargoId`.

### Requirement: Edit sí carga catálogo de PuestoSuperior

El PageModel de `Puestos/Edit` MUST invocar `IPuestosApiClient.GetAllAsync()` para armar `PuestoSuperiorOptions` con `Codigo + Nombre` por opción y una vacía para "sin superior". El dropdown MUST renderizarse en `_Form.cshtml` independientemente de `IsEdit`.

#### Scenario: Select poblado en Edit
- GIVEN `GetAllAsync()` responde con N puestos activos
- WHEN un usuario abre `editar/{id}`
- THEN `PuestoSuperiorOptions` MUST contener N opciones
- AND el HTML MUST contener un `<select>` enlazado a `PuestoSuperiorId` con etiquetas `Codigo + Nombre`.

#### Scenario: Falla de transporte del catálogo de superiores
- GIVEN `GetAllAsync()` falla por timeout o error de transporte
- WHEN un usuario abre `editar/{id}`
- THEN MUST mostrar estado recuperable con reintento o retorno al listado
- AND MUST NOT persistir cambios parciales en `PuestoSuperiorId`.

### Requirement: Documentación del patrón catálogo vs listado

`docs/decisiones-implementacion.md` MUST contener una sección que distinga el contrato de "catálogo completo" (dropdown, sin paginación) del "listado paginado" (Index) para UO y registre explícitamente que `Puestos/Edit` no carga catálogos.

#### Scenario: Developer consulta el patrón
- GIVEN un developer que necesita decidir entre catálogo y listado de UO
- WHEN consulta `docs/decisiones-implementacion.md`
- THEN MUST encontrar la sección que especifique:
  - **Catálogo** (dropdown completo, sin paginación): `GET /api/v1/unidades-organizativas` vía `IUnidadOrganizativaApiClient.GetAllActivasAsync`. Solo en Create.
  - **Listado** (paginado, filtrable): `GET /api/v1/unidades-organizativas/consulta` vía `QueryAsync(UnidadOrganizativaListQuery)`. Usado en `Index`.
  - **Edit** no carga catálogos (decisión locked #3 del change de puestos).