# SGV Web Shell Specification

## Purpose

Define the first functional Razor Pages frontend shell for `SGV.Web`: a neutral Inspinia-based layout that is ready for incremental SGV modules without exposing demo content, authentication flows, or placeholder business screens.

## Requirements

### Requirement: Functional base shell

The system MUST provide a functional SGV web shell using the Inspinia Starterkit baseline, including a shared layout, static assets, page chrome, and a reachable default page.

#### Scenario: Shell loads successfully

- GIVEN the SGV web application is running
- WHEN a user opens the default web entry point
- THEN the response MUST render an SGV shell page successfully
- AND the page MUST load the required shared styling and scripts

#### Scenario: Missing optional module content

- GIVEN no SGV business modules have been implemented in this shell
- WHEN the default page is rendered
- THEN the page MUST remain usable without requiring module-specific data

### Requirement: Demo content removal

The system MUST NOT expose Inspinia demo pages, sample dashboards, fake data screens, or demo navigation as product-facing content.

#### Scenario: Demo navigation is absent

- GIVEN the shell navigation is rendered
- WHEN a user reviews the available navigation entries
- THEN no entry MUST link to Inspinia demo pages, sample dashboards, or fake module screens

#### Scenario: Demo pages are not reachable from shell chrome

- GIVEN the shell page is rendered
- WHEN a user follows visible shell links
- THEN the user MUST NOT be taken to template demo content

### Requirement: Minimal technical navigation

El sistema MUST incluir la navegación mínima del shell y, a partir de este cambio, MUST exponer `Unidades Organizativas`, `Cargos`, `Habilidades` y `Puestos` como módulos funcionales de negocio habilitados. La navegación autenticada MUST mantener `Home`, `Unidades Organizativas`, `Cargos`, `Habilidades` y `Puestos`; `Puestos` MUST renderizarse dentro del grupo `Organización` como entry colapsable con icono `ti ti-hierarchy` y submenú `Listado` + `Nuevo`; y el shell MUST NOT mostrar placeholders de otros módulos no especificados.

(Previously: la navegación autenticada exponía `Home`, `Unidades Organizativas`, `Cargos` y `Habilidades` como módulos funcionales habilitados, sin entry colapsable para `Puestos`.)

#### Scenario: Navegación mínima con Puestos habilitado

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario inspecciona las entradas disponibles
- THEN las entradas MUST incluir `Home`, `Unidades Organizativas`, `Cargos`, `Habilidades` y `Puestos`
- AND `Puestos` MUST ser alcanzable como destino del shell dentro del grupo `Organización`.

#### Scenario: Submenú de Puestos visible y activo

- GIVEN un usuario autenticado ubicado en `/organizacion/puestos` o cualquier subruta (`crear`, `editar/{id}`, `detalle/{id}`)
- WHEN se renderiza el grupo `Organización` del sidenav
- THEN la navegación MUST mostrar `Puestos` expandido
- AND MUST incluir los sub-items `Listado` y `Nuevo`
- AND MUST reflejar el estado `active` para el sub-item correspondiente.

#### Scenario: Otros módulos siguen fuera de alcance

- GIVEN el menú de navegación autenticado renderizado
- WHEN un usuario revisa las opciones visibles
- THEN la navegación MUST NOT mostrar placeholders de reclutamiento, vacantes, catálogos u otros módulos no especificados.

### Requirement: Neutral branding and Inspinia visual system

The system MUST use neutral `SGV` branding, SHOULD preserve Inspinia template colors, and MUST preserve Inspinia layout controls/customizer for this first delivery.

#### Scenario: Neutral SGV brand is visible

- GIVEN the shell is rendered
- WHEN a user views the header, sidebar, or browser title
- THEN the visible product identity MUST be neutral `SGV`

#### Scenario: Layout controls remain available

- GIVEN the shell layout is rendered
- WHEN a user accesses available layout controls or customizer affordances
- THEN the Inspinia layout controls MUST remain present and functional

### Requirement: No authentication dependency

El sistema MUST mantener `/auth/sign-in` accesible para usuarios no autenticados, MUST proteger el dashboard inicial y el shell autenticado detrás de sesión web, y MUST separar el layout de autenticación del layout principal. El sistema MUST NOT incorporar UI de registro, recuperación de contraseña ni navegación de account-management en esta entrega.

(Previously: el shell era completamente público y no mostraba login, logout ni navegación de cuenta.)

#### Scenario: Acceso anónimo al shell protegido

- GIVEN un usuario no autenticado
- WHEN abre el punto de entrada protegido del shell SGV
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`

#### Scenario: Acceso público a login

- GIVEN un usuario no autenticado
- WHEN abre `/auth/sign-in`
- THEN la pantalla MUST renderizarse sin redirigir al shell autenticado
- AND la vista MUST usar un layout distinto de `_VerticalLayout`

#### Scenario: UI de cuenta acotada

- GIVEN el shell autenticado renderizado
- WHEN el usuario revisa las acciones visibles de cuenta
- THEN la interfaz MUST ofrecer logout
- AND la interfaz MUST NO mostrar registro, forgot password ni account-management

### Requirement: Frontend validation expectations

The implementation MUST keep the Razor Pages shell buildable and MUST validate the frontend asset workflow when shell assets or asset pipeline configuration are changed.

#### Scenario: .NET solution remains buildable

- GIVEN the shell implementation is complete
- WHEN the solution build is executed
- THEN the build MUST succeed without backend, database, or API changes required by this shell

#### Scenario: Asset pipeline changes are validated

- GIVEN frontend assets or asset pipeline configuration are modified
- WHEN the frontend asset build command is executed
- THEN generated shell assets MUST compile successfully
