# Especificación: persona-card-partial

## Propósito

Definir el comportamiento de la partial Razor `_PersonaCard.cshtml` que unifica en un único componente reutilizable la card de persona hoy duplicada en `Usuarios/Details`, `Usuarios/_Form`, `Ocupaciones/Details` y `Ocupaciones/_Form`. Expone dos modos (`readonly` y `editable`) vía `ViewDataDictionary`, acepta `PersonaDto?` como `@model` y preserva el contrato de `data-*` attributes que consume `usuario-persona-buscador.js`. No introduce Tag Helper, Blazor ni cambios a `Personas/Details.cshtml`.

## Requisitos

### Requirement: PER-CARD-01 — Modos `readonly` y `editable`

La partial `_PersonaCard.cshtml` MUST renderizarse en modo `readonly` cuando `ViewData["Mode"]="readonly"` y en modo `editable` cuando `ViewData["Mode"]="editable"`. En modo `readonly` MUST NOT emitir botones `Quitar`/`Cambiar` ni el contenedor `data-display` editable; en modo `editable` SHALL emitirlos. Cualquier otro valor (o ausencia) de `Mode` MUST tratarse como `readonly`.

#### Scenario: `readonly` omite acciones mutables
- GIVEN la partial invocada con `Mode="readonly"` y `PersonaDto` poblado
- WHEN renderiza
- THEN el HTML MUST contener nombre y documento formateado
- AND MUST NOT contener `data-usuario-persona-quitar` ni `data-usuario-persona-buscar` (los botones mutables son propia del modo editable).

#### Scenario: `editable` emite acciones mutables
- GIVEN la partial invocada con `Mode="editable"`
- WHEN renderiza
- THEN el HTML MUST contener el botón `Quitar` (`data-usuario-persona-quitar`) y el botón `Cambiar`/`Buscar` (`data-usuario-persona-buscar` + Bootstrap `data-bs-target="#{ModalId}"`)
- AND MUST incluir el contenedor con `data-usuario-persona-display` (su `id` es el que el modal root referencia vía `dataset.displayContainerId`).

#### Scenario: `Mode` omitido cae a `readonly`
- GIVEN la partial invocada sin `ViewData["Mode"]`
- WHEN renderiza
- THEN el resultado MUST ser idéntico al modo `readonly`.

### Requirement: PER-CARD-02 — Datos completos de persona

La partial MUST mostrar, cuando existan en `PersonaDto`, los campos Nombre completo, Documento (vía `PersonaFormatHelper.FormatDocumento`), Email, Teléfono y Estado. Ante un `PersonaDto` nulo o dado de baja, la card MUST renderizarse en estado vacío sin lanzar excepción.

#### Scenario: Datos completos en readonly
- GIVEN `Mode="readonly"` y `PersonaDto` con Email y Teléfono
- WHEN renderiza
- THEN el HTML MUST presentar Email y Teléfono junto a Nombre y Documento.

#### Scenario: `PersonaDto` nulo muestra estado vacío
- GIVEN `Model=null`
- WHEN renderiza
- THEN la card MUST mostrar un placeholder vacío
- AND MUST NOT arrojar `NullReferenceException`.

### Requirement: PER-CARD-03 — Badge de Estado de Persona controlado por parámetro

La presencia del badge de Estado de **Persona** MUST estar gobernada por `ViewData["ShowStatusBadge"]`. Si es `true` y `PersonaDto.Estado` existe, SHALL renderizar el badge de Estado de Persona; si es `false` o ausente, MUST ocultarlo sin alterar el resto de la card. **`ShowStatusBadge` refiere exclusivamente al Estado de Persona** y MUST NO interferir con el badge de Estado de Ocupación (u otra entidad anfitriona), que vive en su propio componente fuera de la partial y se rige por su propio parámetro.

#### Scenario: `ShowStatusBadge=true` muestra Estado de Persona
- GIVEN `ShowStatusBadge=true` y `PersonaDto.Estado` presente
- WHEN renderiza
- THEN el HTML de la partial MUST contener el badge de Estado de Persona.

#### Scenario: `ShowStatusBadge=false` omite Estado de Persona
- GIVEN `ShowStatusBadge=false`
- WHEN renderiza
- THEN el HTML de la partial MUST NOT contener el badge de Estado de Persona
- AND el resto de la card MUST permanecer inalterado.

#### Scenario: Badge de Ocupación independiente del badge de Persona
- GIVEN `Ocupaciones/Details` renderiza `OcupacionDetailsViewModel` que ya muestra su propio badge de Estado de Ocupación fuera de `_PersonaCard`
- WHEN la partial se invoca con `ShowStatusBadge=false` (u omitido) para suprimir el badge de Persona
- THEN el badge de Estado de Ocupación MUST seguir renderizándose inalterado por su vista anfitriona
- AND `_PersonaCard` MUST NO emitir ni suprimir el badge de Ocupación.

### Requirement: PER-CARD-04 — Acciones `Quitar`/`Cambiar` solo en editable

Las acciones `Quitar` y `Cambiar` MUST emitirse únicamente en modo `editable` y cuando `ShowQuitarCambiar` no sea `false`. `Quitar` MUST limpiar el hidden `PersonaIdInputName` vía JS sin invocar la API; `Cambiar` MUST abrir el modal `ModalId` declarado por el consumer.

#### Scenario: `Quitar` no invoca la API
- GIVEN `Mode="editable"`, persona preseleccionada y JS cargado
- WHEN el Administrador pulsa `Quitar`
- THEN el hidden `PersonaIdInputName` MUST quedar vacío
- AND MUST NOT producirse ninguna llamada HTTP a `IPersonaApiClient`.

#### Scenario: `Cambiar` abre el modal configurado
- GIVEN `Mode="editable"` y `ModalId="usuario-persona-buscador-modal"`
- WHEN el Administrador pulsa el botón `data-usuario-persona-buscar`
- THEN Bootstrap MUST abrir `#usuario-persona-buscador-modal`
- AND el JS MUST excluir la persona actual (`modal.dataset.currentPersonaId`) de los resultados.

### Requirement: PER-CARD-05 — Contrato `data-*` idéntico al JS vigente

La partial MUST emitir exactamente los `data-*` attributes que `usuario-persona-buscador.js` selecciona hoy (verificados en `wwwroot/js/pages/usuario-persona-buscador.js`): en el contenedor página `data-usuario-persona-display` (con `id`referenciado por `modal.dataset.displayContainerId`), dentro suyo `data-usuario-persona-card`, `data-usuario-persona-display-text` y `data-usuario-persona-empty`, y como sibling del `parentElement` del display el hidden `data-usuario-persona-display-input`. En modo `editable` además el botón `Quitar` lleva `data-usuario-persona-quitar`, el botón `Buscar`/`Cambiar` lleva `data-usuario-persona-buscar` junto a Bootstrap `data-bs-toggle="modal"` y `data-bs-target="#{ModalId}"`. El modal `_PersonaBuscadorModal.cshtml` root aporta `data-usuario-persona-modal`, `data-solo-sin-usuario` y los dataset `hidden-input-id`, `display-container-id`, `api-url`, `current-persona-id`. La partial **MUST NOT inventar** attributes inexistentes (`data-usuario-persona-cambiar`, `-persona-id`, `-modal-id`, `data-display-container-id` como atributo de página) — ya se confirmó que el JS no los lee.

#### Scenario: Botón `Buscar`/`Cambiar` usa `data-usuario-persona-buscar` + Bootstrap
- GIVEN la partial en `Mode="editable"` y `ModalId="usuario-persona-buscador-modal"`
- WHEN el JS inspecciona el botón
- THEN MUST hallar `data-usuario-persona-buscar`, `data-bs-toggle="modal"` y `data-bs-target="#usuario-persona-buscador-modal"`
- AND MUST NOT hallar `data-usuario-persona-cambiar` ni `data-usuario-persona-modal-id`.

#### Scenario: Contenedor display + card dentro + display-text dentro + empty dentro
- GIVEN la partial editable o readonly con `PersonaDto` poblado
- WHEN el JS resuelve `display = root.getElementById(modal.dataset.displayContainerId)`
- THEN `display` MUST contener `data-usuario-persona-card`, `data-usuario-persona-display-text` y `data-usuario-persona-empty`
- AND su `parentElement` MUST contener `data-usuario-persona-display-input` (sibling hidden).

#### Scenario: `Quitar` presente solo en editable
- GIVEN la partial en `Mode="editable"`
- WHEN el JS ejecuta `root.querySelectorAll('[data-usuario-persona-quitar]')`
- THEN MUST hallar el botón `Quitar`
- AND en `Mode="readonly"` MUST hallar cero nodos.

#### Scenario: Atributos inexistentes no emitidos
- GIVEN el HTML emitido por la partial
- WHEN se inspecciona
- THEN MUST NOT existir `data-usuario-persona-cambiar`, `data-usuario-persona-persona-id`, `data-usuario-persona-modal-id`, ni `data-display-container-id` como atributo de página (sólo existe `data-display-container-id` en el modal root referenciado por `dataset`).

### Requirement: PER-CARD-06 — Fallback de detalle en Ocupaciones

En `Ocupaciones/Details.cshtml.cs`, el PageModel MUST intentar cargar `PersonaDto` vía `IPersonaApiClient.GetByIdAsync`. Si la llamada falla, el PageModel MUST degradar silenciosamente y exponer únicamente `PersonaNombre`, sin error visible al usuario y sin propagar excepción al request.

#### Scenario: Carga exitosa enriquece la card
- GIVEN una Ocupación con `PersonaId` válido y API disponible
- WHEN ejecuta `OnGetAsync`
- THEN `OcupacionDetailsViewModel.Persona` MUST quedar poblado
- AND la card MUST renderizar Email, Teléfono y Estado.

#### Scenario: Falla HTTP cae a `PersonaNombre`
- GIVEN `IPersonaApiClient.GetByIdAsync` lanza o responde error
- WHEN ejecuta `OnGetAsync`
- THEN el PageModel MUST loguear y degradar a `PersonaNombre`
- AND la card MUST mostrar Nombre sin Email/Teléfono/Estado
- AND MUST NOT mostrarse error al usuario ni propagarse excepción.

### Requirement: PER-CARD-07 — Exclusión de `Personas/Details.cshtml`

Este cambio MUST NOT modificar `Pages/Personas/Details.cshtml`. La partial `_PersonaCard` MAY usarse en el futuro, pero este cambio no la integra en esa vista.

#### Scenario: `Personas/Details` sin cambios
- GIVEN el árbol de archivos de este cambio
- WHEN se inspecciona `git diff`
- THEN `Pages/Personas/Details.cshtml` MUST NOT aparecer en el diff.

### Requirement: PER-CARD-08 — Errores de carga del cliente tipado

Si la card recibe un `PersonaDto` parcial (solo Nombre, sin documento/contacto) por degradación del cliente, la partial MUST renderizar sólo los campos disponibles sin mostrar inputs rotos ni placeholders de `null`. La degradación MUST ser silenciosa y visualmente coherente.

#### Scenario: `PersonaDto` parcial sin Email/Teléfono
- GIVEN `PersonaDto` con Nombre pero sin Email ni Teléfono (fallback de carga)
- WHEN la partial renderiza
- THEN MUST om esas filas sin texto `null` ni `undefined`
- AND MUST mostrar Nombre y Documento (si existen).

### Requirement: PER-CARD-09 — Regresiones visuales prohibidas

Las cuatro vistas migradas (`Usuarios/Details`, `Usuarios/_Form`, `Ocupaciones/Details`, `Ocupaciones/_Form`) MUST producir markup visualmente equivalente al pre-cambio. La introducción de la partial MUST NOT alterar clases CSS de Inspinia, estructura de filas ni `data-*` contracts consumidos por el JS.

#### Scenario: `Usuarios/Details` sin regresión visual
- GIVEN `dotnet test` corre los smoke tests de Web
- WHEN renderiza `Usuarios/Details` como admin
- THEN los asserts existentes sobre la card MUST pasar sin modificación de las aserciones visuales.

#### Scenario: Binding JS preservado en `Usuarios/_Form`
- GIVEN `usuario-persona-buscador.js` cargado en `Usuarios/_Form`
- WHEN se ejecuta el flujo `Buscar → seleccionar`
- THEN la card MUST poblar el display y el hidden id como antes del cambio.

### Requirement: PER-CARD-10 — Enlace a detalle de Persona en modo readonly

En modo `readonly`, cuando se provee `ViewData["PersonaDetailUrl"]` (p.ej. `"/personas/detalle/{PersonaId}"`) y `PersonaDto` está poblado, el título/Nombre completo de la card MUST renderizarse como `<a href="{PersonaDetailUrl}">` para enlazar al detalle de Persona — igual que el link ya existente en `Usuarios/Details`. Si `PersonaDetailUrl` se omite o es null, MUST renderizar texto plano sin alterar el resto. Cuando `PersonaDto` es `null` pero se provee `ViewData["FallbackDisplay"]` (p.ej. `PersonaNombre` en Ocupaciones), SHALL renderizar ese texto y, si además se provee `ViewData["FallbackUrl"]`, envolverlo en el mismo `<a href>`. La rama fallback MUST NO emitir los botones `Quitar`/`Cambiar` (es readonly).

#### Scenario: readonly con `PersonaDetailUrl` envuelve Nombre en link
- GIVEN `Mode="readonly"`, `PersonaDto` poblado y `PersonaDetailUrl="/personas/detalle/abc-123"`
- WHEN renderiza
- THEN el Nombre completo MUST estar envuelto en `<a href="/personas/detalle/abc-123">`
- AND el resto de la card (Email, Teléfono, Estado, Documento) MUST permanecer sin link.

#### Scenario: readonly sin `PersonaDetailUrl` deja texto plano
- GIVEN `Mode="readonly"` y `PersonaDto` poblado pero `PersonaDetailUrl` omitido
- WHEN renderiza
- THEN el Nombre MUST mostrarse como texto plano sin `<a>` envolvente
- AND la card MUST permanecer funcional.

#### Scenario: fallback `FallbackDisplay` sin `PersonaDto` muestra Nombre plano
- GIVEN `Mode="readonly"`, `PersonaDto=null` y `FallbackDisplay="García, Juan"`
- WHEN renderiza
- THEN la card MUST mostrar "García, Juan" como texto
- AND MUST NOT emitir `Quitar`/`Cambiar` (readonly)
- AND MUST NOT arrojar.

#### Scenario: fallback `FallbackDisplay` + `FallbackUrl` envuelve en link
- GIVEN `Mode="readonly"`, `PersonaDto=null`, `FallbackDisplay="García, Juan"` y `FallbackUrl="/personas/detalle/abc-123"`
- WHEN renderiza
- THEN la card MUST mostrar `<a href="/personas/detalle/abc-123">García, Juan</a>`
- AND sin botones `Quitar`/`Cambiar`.

#### Scenario: Ocupaciones/Details enlaza a detalle de Persona
- GIVEN `Ocupaciones/Details` con `PersonaDto` cargado exitosamente
- WHEN el consumer invoca `@await Html.PartialAsync("_PersonaCard", Model.Persona, vd{Mode=readonly, PersonaDetailUrl="/personas/detalle/"+o.PersonaId, ...})`
- THEN el HTML MUST contener `<a href="/personas/detalle/{PersonaId}">` sobre el Nombre de Persona
- AND ese enlace MUST apuntar a la página de detalle de Persona vigente, NO a un endpoint API.