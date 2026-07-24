# Spec: habilidad-web-crear-editar

## Purpose

Definir los flujos autenticados de alta y edición de `Habilidades` en `SGV.Web`, reutilizando el patrón de `Cargos` y permitiendo editar `Codigo` dentro del alcance real del catálogo maestro.

## Requirements

### Requirement: Acceso autenticado a create y edit de habilidades

El sistema MUST exponer páginas Razor protegidas para create y edit de `Habilidades` dentro del shell autenticado y MUST ofrecer una acción visible para volver al listado.

#### Scenario: Usuario autenticado abre create

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega a `/organizacion/habilidades/crear`
- THEN la aplicación MUST responder con un formulario vacío dentro del shell autenticado.

#### Scenario: Habilidad activa existente en edit

- GIVEN una Habilidad activa existente y un usuario autenticado
- WHEN navega a su URL de edición
- THEN la aplicación MUST mostrar el formulario prellenado con los datos actuales.

#### Scenario: Habilidad inexistente o eliminada en edit

- GIVEN un identificador que no puede consultarse como habilidad activa
- WHEN un usuario autenticado abre la pantalla de edit
- THEN la interfaz MUST mostrar un estado recuperable de no disponible
- AND MUST impedir el guardado desde esa vista.

### Requirement: Edición web de Codigo de una Habilidad

La página de edición MUST permitir cambiar `Codigo` de una Habilidad activa existente y MUST enviar el valor actualizado al backend junto con el resto del formulario.

#### Scenario: Editar Codigo de una Habilidad existente

- GIVEN una Habilidad activa existente con `Codigo` vigente
- WHEN un usuario autenticado edita `Codigo` por otro valor válido y guarda el formulario
- THEN la página MUST confirmar el guardado exitoso
- AND MUST volver a mostrar la Habilidad con el nuevo `Codigo` persistido.

#### Scenario: Editar otros campos sin cambiar Codigo

- GIVEN una Habilidad activa existente con datos válidos
- WHEN un usuario autenticado guarda cambios en `Nombre`, `Categoria` o `Descripcion` sin modificar `Codigo`
- THEN la página MUST confirmar el guardado exitoso
- AND MUST volver a mostrar el mismo `Codigo` previo junto con los demás campos actualizados.

#### Scenario: Codigo inválido en edición

- GIVEN una Habilidad activa existente
- WHEN un usuario autenticado intenta guardar un `Codigo` vacío, demasiado largo o fuera del formato admitido por el formulario
- THEN la página MUST mostrar el error de validación asociado a `Codigo`
- AND MUST conservar el resto de los datos ingresados para su corrección.

#### Scenario: Codigo duplicado de otra Habilidad activa

- GIVEN una Habilidad activa existente
- AND existe otra Habilidad activa con el `Codigo` ingresado
- WHEN un usuario autenticado intenta guardar ese `Codigo` duplicado
- THEN la página MUST mostrar un error de conflicto coherente sobre `Codigo`
- AND MUST conservar el resto del formulario para permitir corregirlo.

#### Scenario: Reutilizar un Codigo liberado por baja lógica

- GIVEN una Habilidad activa en edición
- AND existe una Habilidad eliminada cuyo `Codigo` coincide con el nuevo valor ingresado
- WHEN un usuario autenticado guarda el formulario con ese `Codigo`
- THEN la página MUST aceptar el guardado
- AND MUST mostrar la Habilidad actualizada con el `Codigo` reutilizado.

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

### Requirement: Guardado con PRG y feedback accionable

El sistema MUST aplicar PRG tras create o edit exitosos y MUST traducir validaciones, conflictos de `Codigo` activo y fallos de disponibilidad del backend a feedback claro y accionable.

#### Scenario: Create exitoso

- GIVEN datos válidos para una nueva Habilidad
- WHEN el usuario confirma create y el backend persiste la operación
- THEN la shell MUST redirigir al detail de la Habilidad creada
- AND MUST mostrar un mensaje visible de éxito.

#### Scenario: Edit exitoso

- GIVEN una Habilidad activa existente y datos válidos
- WHEN el usuario confirma edit y el backend persiste la operación
- THEN la shell MUST redirigir nuevamente a edit o detalle con confirmación visible.

#### Scenario: Edit exitoso con cambio de Codigo mantiene PRG

- GIVEN una Habilidad activa existente y datos válidos
- WHEN el usuario confirma edit con un nuevo `Codigo` válido y el backend persiste la operación
- THEN la shell MUST redirigir nuevamente a edit o detalle con confirmación visible
- AND MUST reflejar el `Codigo` actualizado tras la redirección.

#### Scenario: Conflicto por Codigo activo duplicado

- GIVEN un intento de create con `Codigo` ya usado por otra Habilidad activa
- WHEN el backend responde conflicto
- THEN la interfaz MUST mostrar un mensaje claro indicando el campo afectado
- AND MUST permitir corregir el formulario sin perder el resto de los datos.

#### Scenario: Backend no disponible durante el guardado

- GIVEN un formulario válido listo para enviarse
- WHEN create o edit falla porque el backend no está disponible
- THEN la interfaz MUST mostrar un error visible de disponibilidad
- AND MUST ofrecer una acción concreta de reintento.

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

## Out of scope

- No extiende el contrato de persistencia de `skills` con datos de nivel en este cambio.
- No muestra ni persiste un nivel de habilidad en el catálogo maestro porque el modelo de dominio no tiene `NivelId` propio en la entidad `Habilidad`; el nivel vive en la asociación con un cargo o persona.
- No incluye asignaciones `habilidad↔cargo` ni `habilidad↔persona`.
