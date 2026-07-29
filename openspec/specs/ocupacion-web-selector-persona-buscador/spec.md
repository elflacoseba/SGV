# Especificación: Selector modal de Persona con buscador — Crear/Editar Ocupación

## Propósito

Definir el comportamiento del campo `PersonaId` en los formularios `Crear/Editar Ocupacion` de `SGV.Web` reemplazando el `<select>` plano por la card + modal reutilizable, con la diferencia crítica de NO aplicar el filtro `soloSinUsuario=true` (una persona puede tener múltiples ocupaciones). No modifica backend, persistencia, migraciones ni el modal `_PersonaBuscadorModal.cshtml`.

## Requisitos

### Requirement: OCC-PER-BUSC-01 Reemplazo del `<select>` por card + modal

`Create.cshtml` y `Edit.cshtml` de Ocupaciones MUST renderizar el campo `PersonaId` como card de persona + botón `Buscar` + modal `_PersonaBuscadorModal.cshtml`, y MUST NOT renderizar un `<select name="Input.PersonaId">` poblado con el catálogo completo vía `IPersonaApiClient.GetAllAsync()`. El id MUST persistirse en un `<input type="hidden" asp-for="Input.PersonaId">` para preservar el contrato del modelo.

#### Scenario: Cargar Create renderiza card + botón Buscar

- **GIVEN** un Administrador autenticado abre `/organizacion/ocupaciones/crear`
- **WHEN** la página renderiza
- **THEN** el HTML MUST contener la card de Persona con botón `Buscar`
- **AND** MUST NOT contener `<select name="Input.PersonaId">`
- **AND** MUST contener `<input type="hidden" name="Input.PersonaId">`
- **AND** MUST incluir `usuario-persona-buscador.js` en la sección `Scripts`.

#### Scenario: Cargar Edit renderiza card + botón Buscar

- **GIVEN** un Administrador autenticado abre `/organizacion/ocupaciones/editar/{id}` de una Ocupación vigente
- **WHEN** la página renderiza
- **THEN** el HTML MUST contener la card con la persona vinculada
- **AND** MUST NOT contener `<select name="Input.PersonaId">`
- **AND** MUST incluir `usuario-persona-buscador.js`.

### Requirement: OCC-PER-BUSC-02 `IOcupacionForm` expone estado enriquecido de la persona

`IOcupacionForm` MUST exponer `PersonaDisplay` (string formateado `Apellido, Nombre (TipoDoc: NroDoc)`, cayendo a `Legajo` si no hay documento) y `PersonaVinculada` (`PersonaDto?`). `OcupacionFormPageModel` MUST declarar ambas propiedades y `LoadCatalogsAsync` MUST NO usar `GetAllAsync` para personas. En Edit, el PageModel MUST cargar la persona vía `IPersonaApiClient.GetByIdAsync(Input.PersonaId)` tras resolver `Input.PersonaId` desde la `OcupacionDto`.

#### Scenario: `IOcupacionForm` expone `PersonaDisplay` y `PersonaVinculada`

- **GIVEN** el tipo `IOcupacionForm`
- **WHEN** un test inspecciona sus miembros
- **THEN** MUST contener `PersonaDisplay` (string) y `PersonaVinculada` (PersonaDto?)
- **AND** MUST NO exponer `PersonaOptions` poblado desde `GetAllAsync`.

#### Scenario: Edit enriquece la card desde `GetByIdAsync`

- **GIVEN** una Ocupación vigente con `PersonaId` resuelto
- **WHEN** `OcupacionFormPageModel.LoadCatalogsAsync` ejecuta en Edit
- **THEN** MUST invocar `IPersonaApiClient.GetByIdAsync(Input.PersonaId)`
- **AND** `PersonaVinculada` MUST quedar poblada con el DTO
- **AND** `PersonaDisplay` MUST formatearse como `Apellido, Nombre (TipoDoc: NroDoc)` o `Legajo` si no hay documento.

#### Scenario: Create no invoca `GetByIdAsync` para personas

- **GIVEN** `Create` con `Input.PersonaId = null`
- **WHEN** `LoadCatalogsAsync` ejecuta
- **THEN** MUST NO invocar `IPersonaApiClient.GetByIdAsync`
- **AND** `PersonaDisplay` MUST ser `null`/vacía (card en estado vacío).

### Requirement: OCC-PER-BUSC-03 Búsqueda sin filtro `soloSinUsuario`

El modal invocado desde Ocupaciones MUST construir `GET /api/v1/personas/consulta` SIN el parámetro `soloSinUsuario=true` (o con `soloSinUsuario=false`), listando personas activas estén o no vinculadas a un usuario. El modal root MUST declarar `data-solo-sin-usuario="false"`.

#### Scenario: Búsqueda desde Ocupaciones omite `soloSinUsuario`

- **GIVEN** el modal de Ocupaciones abierto con texto `garcia`
- **WHEN** se pulsa `Enter` o el botón `Buscar`
- **THEN** MUST dispararse `GET /api/v1/personas/consulta?search=garcia&p=1&pageSize=25` (sin `soloSinUsuario=true`)
- **AND** el resultado MAY contener personas con usuario activo asociado.

#### Scenario: Modal root declara `data-solo-sin-usuario="false"`

- **GIVEN** el HTML de `_Form.cshtml` de Ocupaciones
- **WHEN** un test inspecciona el contenedor del modal
- **THEN** MUST existir `data-solo-sin-usuario="false"` en el modal root.

### Requirement: OCC-PER-BUSC-04 Preselección y exclusión en Edit

En `/organizacion/ocupaciones/editar/{id}`, cuando la Ocupación tiene `PersonaId` resuelto, la card MUST mostrarse preseleccionada. Al pulsar `Cambiar`, el modal MUST abrirse excluyendo la persona actualmente vinculada de los resultados. `Quitar` MUST limpiar `Input.PersonaId` (a `null` valores) sin invocar la API.

#### Scenario: Edit precarga la persona vinculada en la card

- **GIVEN** una Ocupación vigente con persona `García, Juan` vinculada
- **WHEN** renderiza `/organizacion/ocupaciones/editar/{id}`
- **THEN** la card MUST mostrar `García, Juan (TipoDoc: NroDoc)`
- **AND** `Input.PersonaId` (hidden) MUST tener el id correspondiente.

#### Scenario: `Cambiar` excluye la persona actual del modal

- **GIVEN** Edit con `García, Juan` preseleccionada
- **WHEN** el Administrador pulsa `Cambiar`
- **THEN** el modal MUST abrirse
- **AND** la fila de `García, Juan` MUST NOT aparecer en los resultados.

#### Scenario: `Quitar` limpia el campo sin invocar la API

- **GIVEN** Edit con `García, Juan` preseleccionada
- **WHEN** el Administrador pulsa `Quitar`
- **THEN** `Input.PersonaId` MUST quedar `null`
- **AND** MUST NO invocarse ninguna operación HTTP sobre `IPersonaApiClient`.

### Requirement: OCC-PER-BUSC-05 Pre-carga via query string en Create

Create SHOULD aceptar `?personaId={id}` y, al cargar, preseleccionar la persona en la card y poblar `Input.PersonaId`, excluyéndola del modal al abrirse. Si el id no existe o está eliminado, Create MUST caer al estado vacío sin error fatal.

#### Scenario: `?personaId` válido precarga la card

- **GIVEN** se accede a `/organizacion/ocupaciones/crear?personaId={id de persona activa}`
- **WHEN** la página carga
- **THEN** la card MUST mostrar la persona
- **AND** `Input.PersonaId` MUST estar poblado
- **AND** al abrir el modal esa persona MUST NOT aparecer en resultados.

#### Scenario: `?personaId` inexistente cae a estado vacío

- **GIVEN** se accede a `/organizacion/ocupaciones/crear?personaId={guid inexistente}`
- **WHEN** la página carga
- **THEN** la card MUST quedar vacía
- **AND** `Input.PersonaId` MUST ser `null`
- **AND** MUST NO lanzarse excepción no controlada.

### Requirement: OCC-PER-BUSC-06 Estados del modal reutilizados

El modal reutilizado MUST exhibir los estados Inicial/Empty/Loading/Error de transporte definidos en `usuario-web-selector-persona-buscador` (REQ-USB-05), con idénticos mensajes y comportamiento de preservación de texto/reintento. Los mensajes MUST NO cambiar entre contextos.

#### Scenario: Estado Empty muestra mensaje estándar

- **GIVEN** el modal de Ocupaciones abierto con término `zzzzz` sin coincidencias
- **WHEN** `/consulta` responde `200` con `items=[]`
- **THEN** MUST mostrarse `No se encontraron personas con ese criterio.`
- **AND** el botón `Buscar` MUST seguir operativo.

#### Scenario: Error de transporte preserva texto

- **GIVEN** el modal de Ocupaciones abierto y el API de personas no disponible
- **WHEN** se pulsa `Buscar`
- **THEN** MUST mostrarse `No se pudo conectar con el servidor. Reintentá.`
- **AND** el texto tipeado MUST preservarse
- **AND** el modal MUST permanecer abierto
- **AND** el resto del formulario MUST quedar intacto.

### Requirement: OCC-PER-BUSC-07 Actualización de tests xUnit

Los tests existentes que asertan el `<select>` de Persona (`OcupacionCreatePageTests.Get_Create_WhenAdmin_RendersAllFiveFieldsWithCatalogs` líneas 102-103, `Assert.Contains("García, Ana", ...)`, `Assert.Contains("Analista", ...)`) MUST actualizarse o removerse y reemplazarse por cobertura equivalente del modal (card renderizada, hidden poblado, presencia del botón `Buscar`, ausencia de `<select>`). Los tests de POST que envían `Input.PersonaId` via hidden input NO requieren cambios de contrato.

#### Scenario: Test de render validando modal en lugar de `<select>`

- **GIVEN** `OcupacionCreatePageTests` ejecutando
- **WHEN** el test inspecta el HTML de Create como admin
- **THEN** MUST asertar presencia de la card + botón `Buscar`
- **AND** MUST asertar ausencia de `<select name="Input.PersonaId">`
- **AND** MUST NOT mantener `Assert.Contains("García, Ana", ...)` ni `Assert.Contains("Analista", ...)`.

## Dependencias

- Depende de la MODIFIED delta en `usuario-web-selector-persona-buscador` (lectura de `data-solo-sin-usuario` por el JS compartido).
- Depende de `web-ocupaciones-crear-editar` (REQ-OCC-FORM-001) — este cambio sustituye el selector de `PersonaId` del REQ-OCC-FORM-001 sin alterar `PuestoId`, `FechaInicio`, `TipoAsignacion` ni `Observaciones`.
- Depende de `web-apiclient-transport-contract` sólo indirectamente vía `IPersonaApiClient.GetByIdAsync` ya vigente.
