# Especificación de create y edit web de puestos

## Purpose

Flujos autenticados de alta y edición de `Puestos` en `SGV.Web` alineados con `Cargos`. Create: catálogo completo; Edit: **estrictamente** `Nombre`, `Descripcion?`, `PuestoSuperiorId?`. El HTML de `Edit` MUST NOT contener `name="codigo"`, `name="unidadOrganizativaId"` ni `name="cargoId"`.

## Requirements

### Requirement: Acceso autenticado a create y edit

Páginas Razor protegidas para `crear` y `editar/{id}` dentro del shell autenticado.

#### Scenario: Acceso autenticado vs anónimo
- GIVEN un usuario en `SGV.Web`
- WHEN navega a `crear` o `editar/{id}`
- IF autenticado MUST responder con el form.
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
