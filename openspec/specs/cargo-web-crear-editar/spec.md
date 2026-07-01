# Especificación de create y edit web de cargos

## Purpose

Agregar el flujo autenticado de create y edit de `Cargos` en `SGV.Web`, alineado con el shell administrativo actual y con la nueva regla que permite editar `Codigo` sin duplicados activos.

## Requirements

### Requirement: Acceso autenticado a create y edit de cargos

El sistema MUST exponer páginas Razor protegidas para create y edit de `Cargos` dentro del shell autenticado y MUST ofrecer una acción visible para volver al listado.

#### Scenario: Usuario autenticado abre create

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega a `/organizacion/cargos/crear`
- THEN la aplicación MUST responder con un formulario vacío dentro del shell autenticado
- AND MUST mostrar una acción visible para volver al listado.

#### Scenario: Usuario autenticado abre edit

- GIVEN un Cargo activo existente y un usuario autenticado
- WHEN navega a la URL de edición del Cargo
- THEN la aplicación MUST mostrar el formulario prellenado con los datos actuales
- AND MUST mostrar una acción visible para volver al listado.

#### Scenario: Usuario anónimo intenta acceder

- GIVEN un usuario no autenticado
- WHEN solicita la URL de create o edit de cargos
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`.

#### Scenario: Cargo inexistente en edit

- GIVEN un identificador de Cargo que no puede consultarse como activo
- WHEN un usuario autenticado abre la pantalla de edit
- THEN la interfaz MUST mostrar un estado recuperable de no disponible
- AND MUST ofrecer un camino claro para volver al listado.

### Requirement: Datos visibles y editables del cargo

El sistema MUST mostrar en create y edit los campos `Codigo`, `Nombre`, `Descripcion` y `Nivel`. En create todos MUST ser editables, y en edit `Codigo` MUST seguir siendo editable de forma consistente con la regla vigente del backend.

#### Scenario: Create muestra todos los campos editables

- GIVEN un usuario autenticado abre create
- WHEN la pantalla termina de cargar correctamente
- THEN la interfaz MUST mostrar `Codigo`, `Nombre`, `Descripcion` y `Nivel`
- AND MUST permitir editarlos antes del guardado.

#### Scenario: Edit permite cambiar Codigo

- GIVEN un Cargo activo existente
- WHEN un usuario autenticado abre edit
- THEN la interfaz MUST mostrar `Codigo` con su valor actual
- AND MUST permitir modificarlo junto con `Nombre`, `Descripcion` y `Nivel`.

### Requirement: Catálogo de Nivel en el formulario

El sistema MUST poblar el dropdown de `Nivel` desde `GET /api/v1/niveles-cargo` antes de mostrar create o edit, y MUST mostrar un estado recuperable cuando el catálogo no esté disponible.

#### Scenario: Catálogo cargado en create o edit

- GIVEN que `GET /api/v1/niveles-cargo` responde correctamente
- WHEN un usuario autenticado abre create o edit
- THEN la interfaz MUST mostrar opciones seleccionables de `Nivel`
- AND MUST usar ese catálogo antes de permitir el guardado.

#### Scenario: Falla la carga del catálogo

- GIVEN que `GET /api/v1/niveles-cargo` falla o no está disponible
- WHEN un usuario autenticado abre create o edit
- THEN la interfaz MUST mostrar un estado recuperable con error visible
- AND MUST ofrecer una acción clara para reintentar o volver al listado.

### Requirement: Guardado con feedback accionable

El sistema MUST aplicar PRG tras operaciones exitosas y MUST traducir validaciones, conflictos y fallos de disponibilidad del backend a feedback claro y accionable para administradores.

#### Scenario: Create exitoso

- GIVEN datos válidos para un nuevo Cargo
- WHEN el usuario confirma create y el backend persiste la operación
- THEN la shell MUST redirigir al detail del nuevo Cargo
- AND MUST mostrar un mensaje visible de éxito.

#### Scenario: Edit exitoso

- GIVEN un Cargo existente y datos editables válidos
- WHEN el usuario confirma edit y el backend persiste la operación
- THEN la shell MUST redirigir nuevamente a edit del Cargo actualizado
- AND MUST mostrar un mensaje visible de éxito.

#### Scenario: Error de validación por campo

- GIVEN un formulario con datos inválidos
- WHEN el backend responde errores de validación
- THEN la interfaz MUST asociar los errores a los campos correspondientes
- AND MUST conservar los datos ingresados para permitir su corrección.

#### Scenario: Conflicto de unicidad de Codigo

- GIVEN un intento de create o edit con `Codigo` ya usado por otro Cargo activo
- WHEN el backend responde conflicto
- THEN la interfaz MUST mostrar un mensaje claro indicando el campo afectado
- AND MUST permitir corregir el `Codigo` sin perder el resto del formulario.

#### Scenario: Backend no disponible durante el guardado

- GIVEN un formulario válido listo para enviarse
- WHEN create o edit falla porque el backend no está disponible
- THEN la interfaz MUST mostrar un error visible de disponibilidad
- AND MUST ofrecer una acción concreta de reintento.

### Requirement: Submenú de cargos con acceso a create

El sistema MUST mostrar en `_Sidenav` una entrada `Nueva` dentro del grupo `Cargos` apuntando a `/organizacion/cargos/crear`, y el estado visual `active` MUST aplicarse tanto en create como en edit.

#### Scenario: Entrada Nueva visible en el submenú

- GIVEN un usuario autenticado navega el shell en el grupo `Cargos`
- WHEN se renderiza el submenú del módulo
- THEN la navegación MUST mostrar una entrada `Nueva`
- AND MUST enlazar a `/organizacion/cargos/crear`.

#### Scenario: Estado active en create y edit

- GIVEN un usuario autenticado ubicado en create o edit de cargos
- WHEN se renderiza el submenú del módulo `Cargos`
- THEN la navegación MUST reflejar el estado `active` para ese submenú
- AND MUST mantener disponible el acceso de retorno al listado.
