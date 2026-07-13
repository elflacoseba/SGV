# Especificación de create y edit web de puestos

## Purpose

Flujos autenticados de alta y edición de `Puestos` en `SGV.Web` alineados con `Cargos`. Create: catálogo completo; Edit: **estrictamente** `Nombre`, `Descripcion?`, `PuestoSuperiorId?`. El HTML de `Edit` MUST NOT contener `name="codigo"`, `name="unidadOrganizativaId"` ni `name="cargoId"`.

## Requirements

### Requirement: Acceso administrador a create y edit

Páginas Razor protegidas para `crear` y `editar/{id}` dentro del shell autenticado. Un usuario autenticado sin rol `Administrador` MUST ser redirigido a `/error/403` en GET y MUST recibir `Forbid()` en POST.

#### Scenario: Acceso administrador vs no-admin vs anónimo
- GIVEN un usuario en `SGV.Web`
- WHEN navega a `crear` o `editar/{id}`
- IF autenticado con rol `Administrador` MUST responder con el form.
- IF autenticado sin rol `Administrador` MUST redirigirlo a `/error/403`.
- IF anónimo MUST redirigirlo a `sign-in`.

#### Scenario: Puesto inexistente en edit
- GIVEN un id no consultable como puesto activo
- WHEN un usuario abre `editar/{id}`
- THEN MUST mostrar estado recuperable e impedir guardado.

### Requirement: Create con los seis campos editables

`Create` MUST mostrar form con `Codigo`, `Nombre`, `Descripcion?`, `UnidadOrganizativaId`, `CargoId` y `PuestoSuperiorId?`, todos editables.

#### Scenario: Create muestra los seis campos
- GIVEN un usuario autenticado abre `crear`
- WHEN la pantalla termina de cargar
- THEN MUST mostrar los seis campos editables antes del envío.

### Requirement: PuestoSuperiorId con select poblado

El PageModel MUST invocar `IPuestosApiClient.GetAllAsync()` para armar el `SelectList` con `Codigo + Nombre` por opción y una vacía para "sin superior".

#### Scenario: Select poblado por la API
- GIVEN `GetAllAsync()` responde con N puestos activos
- WHEN un usuario abre Create
- THEN el dropdown MUST contener N+1 opciones y cada opción MUST mostrar `Codigo + Nombre`.

#### Scenario: Falla del catálogo
- GIVEN `GetAllAsync()` falla o no responde
- WHEN un usuario abre Create
- THEN MUST mostrar estado recuperable con reintento o retorno al listado.


### Requirement: Edit con tres campos

`Edit` MUST mostrar form con `Nombre`, `Descripcion?`, `PuestoSuperiorId?`. El HTML MUST NOT incluir `name="codigo"`, `name="unidadOrganizativaId"`, `name="cargoId"`.

#### Scenario: Edit muestra los tres campos
- GIVEN un puesto activo
- WHEN un usuario abre `editar/{id}`
- THEN MUST mostrar `Nombre`, `Descripcion?` y `PuestoSuperiorId?` prellenados y editables.

#### Scenario: Ausencia de Codigo/UO/Cargo en Edit
- GIVEN el HTML de `Edit.cshtml`
- WHEN se inspecciona el form
- THEN MUST NOT contener `name="codigo"` ni `name="unidadOrganizativaId"` ni `name="cargoId"`
- AND un test RED obligatorio MUST afirmar esa ausencia.

### Requirement: _Form.cshtml compartido

`_Form.cshtml` MUST ser el partial compartido por `Create` y `Edit`. `Codigo` MUST renderizarse solo si el PageModel lo expone; `UnidadOrganizativaId`/`CargoId` MUST solo en Create.

#### Scenario: Codigo solo en Create
- GIVEN una página renderizando `_Form.cshtml`
- WHEN PageModel es Create THEN HTML MUST incluir `name="codigo"`
- AND WHEN PageModel es Edit THEN HTML MUST NOT incluir `name="codigo"`.

### Requirement: Guardado con PRG y feedback

Create y Edit MUST aplicar PRG tras éxito y MUST traducir `ValidationProblemDetails` a `FieldErrors`, 409 a mensaje visible y fallos de transporte a reintento.

#### Scenario: POST no-admin rechazado
- GIVEN un usuario autenticado sin rol `Administrador`
- WHEN envía el formulario de `crear` o `editar/{id}`
- THEN el handler MUST responder mediante `Forbid()`
- AND MUST NOT invocar la mutación contra la API.

#### Scenario: Create o Edit exitoso
- GIVEN datos válidos
- WHEN el usuario confirma Create o Edit y el backend persiste
- IF Create MUST redirigir al listado con éxito.
- IF Edit MUST redirigir al detalle (o listado) con éxito.

#### Scenario: Validación por campo
- GIVEN backend responde 400 con `ValidationProblemDetails`
- WHEN se re-renderiza el form
- THEN los errores MUST asociarse al input y los datos MUST conservarse.

#### Scenario: Conflicto por Codigo duplicado
- GIVEN un POST con `Codigo` ya usado
- WHEN backend responde 409
- THEN MUST mostrar mensaje claro sobre `Codigo` y conservar el resto.

#### Scenario: Backend no disponible durante guardado
- GIVEN un form válido
- WHEN el POST falla por timeout o error de transporte
- THEN MUST mostrar error visible con reintento concreto.

### Requirement: Submenú de Puestos

`_Sidenav` MUST exponer `Nuevo` en `Puestos` apuntando a `crear`; el estado `active` MUST aplicarse en `crear` y `editar/{id}`.

#### Scenario: Estado active y retorno al Listado
- GIVEN un usuario en `crear` o `editar/{id}`
- WHEN se renderiza el submenú
- THEN MUST reflejar `active` y mantener retorno al Listado.

## Requisitos Añadidos (#120)

Bloque anexo al spec canónico tras el change `2026-07-13-fix-120-uo-catalog-no-truncation`. Conserva el estilo de redacción del delta original (español + modal `MUST` + `GIVEN/WHEN/THEN/AND`) para preservar la trazabilidad de la decisión locked #3 del change de puestos.

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
