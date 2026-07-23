# Delta for Habilidad Web Crear/Editar

## MODIFIED Requirements

### Requirement: Campos visibles y Codigo editable en Crear/Editar Habilidad (MODIFIED)

El formulario de edit MUST permitir modificar `Codigo`, `Nombre`, `CategoriaId` (Guid opcional) y `Descripcion`. El campo de categoría MUST renderizarse como un `<select>` poblado desde `GET /api/v1/categorias-habilidad`, con la opción vacía ("Sin categoría") para representar `CategoriaId = NULL`. El create MUST mostrar el mismo `<select>` con la opción vacía seleccionada por defecto. MUST NOT introducir campo de nivel, dropdown de nivel ni input libre para categoría.
(Previously: el formulario usaba un input libre `Categoria` (`<input type="text" maxlength="100">`) y no consumía ningún catálogo.)

#### Scenario: Create muestra dropdown poblado desde el catálogo

- **GIVEN** un usuario autenticado en `/organizacion/habilidades/crear`
- **AND** el catálogo `CategoriasHabilidad` expone 4 categorías (`Conducción`, `Técnica`, `Dominio`, `Académica`)
- **WHEN** la pantalla termina de cargar
- **THEN** la interfaz MUST mostrar un `<select name="Input.CategoriaId">` poblado con las 4 opciones
- **AND** MUST incluir una opción vacía "Sin categoría" seleccionada por defecto
- **AND** MUST NOT mostrar un input libre de texto para categoría
- **AND** MUST permitir editar `Codigo` antes del guardado.

#### Scenario: Edit muestra dropdown pre-seleccionado con la categoría actual

- **GIVEN** una Habilidad activa existente con `CategoriaId = <guid-Conduccion>`
- **AND** un usuario autenticado abre la página de edición
- **WHEN** la pantalla termina de cargar
- **THEN** la interfaz MUST mostrar `<select name="Input.CategoriaId">` poblado desde el catálogo
- **AND** MUST tener seleccionada la opción correspondiente a `<guid-Conduccion>` con su `Nombre` visible.

#### Scenario: Edit muestra "Sin categoría" cuando la Habilidad no tiene categoría

- **GIVEN** una Habilidad activa existente con `CategoriaId = NULL`
- **WHEN** un usuario autenticado abre la página de edición
- **THEN** la interfaz MUST mostrar la opción vacía "Sin categoría" seleccionada en el `<select>`.

#### Scenario: Edit muestra `Codigo` en un input editable

- **GIVEN** una habilidad activa existente
- **WHEN** un usuario autenticado abre edit
- **THEN** la interfaz MUST mostrar `Codigo` con su valor actual en un input editable.

## ADDED Requirements

### Requirement: Poblado del dropdown desde `CategoriaHabilidadApiClient` (REQ-CAT-06)

La página MUST invocar `ICategoriaHabilidadApiClient.GetAllAsync()` antes de renderizar el `<select>` de categoría para poblarlo. Cuando la llamada falle por `ErrorCategoria.Transport` o `ErrorCategoria.Unexpected`, la página MUST mostrar un error legible y MUST NOT procesar el guardado. Cuando la llamada devuelva una colección vacía, el `<select>` MUST contener únicamente la opción "Sin categoría".

#### Scenario: Dropdown poblado en caliente al cargar la página

- **GIVEN** un usuario autenticado en `/organizacion/habilidades/crear`
- **AND** `ICategoriaHabilidadApiClient.GetAllAsync` devuelve 4 categorías
- **WHEN** se ejecuta `OnGetAsync`
- **THEN** el `PageModel` MUST poblar `Input.CategoriasDisponibles` con los 4 elementos recibidos
- **AND** la vista MUST renderizar el `<select>` con esas 4 opciones + "Sin categoría".

#### Scenario: Catálogo vacío sigue siendo renderizable

- **GIVEN** un usuario autenticado abriendo Crear o Edit
- **AND** `ICategoriaHabilidadApiClient.GetAllAsync` devuelve una colección vacía
- **WHEN** se renderiza la página
- **THEN** la vista MUST mostrar el `<select>` con únicamente la opción "Sin categoría" (sin opciones del catálogo)
- **AND** MUST permitir continuar con el guardado dejando `CategoriaId = NULL`.

#### Scenario: Fallo de carga del catálogo impide guardado

- **GIVEN** un usuario autenticado abriendo Edit
- **AND** `ICategoriaHabilidadApiClient.GetAllAsync` falla con `ErrorCategoria.Transport`
- **WHEN** se ejecuta `OnGetAsync`
- **THEN** el `PageModel` MUST mostrar un error legible "No se pudo cargar el catálogo de categorías"
- **AND** la vista MUST NO exponer el formulario de guardado activo.

### Requirement: Guardado envía `CategoriaId` (Guid?) al backend (REQ-CAT-03)

El formulario de create/edit MUST enviar `CategoriaId` (`Guid?`) al backend. Si el `<select>` está en "Sin categoría", el valor enviado MUST ser `null`. El backend MUST persistir el valor recibido tal cual (sin coerción ni default a una categoría por omisión).

#### Scenario: Submit con categoría seleccionada envía su `Id`

- **GIVEN** un usuario autenticado en Edit
- **AND** el `<select>` tiene seleccionada la opción `<guid-Conduccion>`
- **WHEN** confirma el guardado
- **THEN** el request POST MUST contener `CategoriaId = <guid-Conduccion>` en el payload
- **AND** el backend MUST persistirlo y devolver `CategoriaId = <guid-Conduccion>` + `CategoriaNombre`.

#### Scenario: Submit con "Sin categoría" envía `CategoriaId = null`

- **GIVEN** un usuario autenticado en Edit
- **AND** el `<select>` muestra "Sin categoría" seleccionada
- **WHEN** confirma el guardado
- **THEN** el request MUST contener `CategoriaId = null`
- **AND** el backend MUST persistirlo y responder con `CategoriaId = null` + `CategoriaNombre = null`.

#### Scenario: `CategoriaId` inexistente muestra error de validación

- **GIVEN** un usuario autenticado en Create o Edit
- **WHEN** confirma el guardado con un `CategoriaId` que el backend rechaza como inexistente
- **THEN** la página MUST mostrar el error de validación devuelto por el backend
- **AND** MUST conservar el resto del formulario para corrección.
