# Delta for `usuario-web-crear-editar`

Este delta reemplaza el dropdown plano de personas activas del formulario `Crear Usuario` por el selector modal definido en `usuario-web-selector-persona-buscador`. NO toca la capa de autorización (REQ-UCE-01), validación (REQ-UCE-03), PRG (REQ-UCE-04) ni el resto del formulario `Crear/Editar`. Las páginas siguen exigiendo rol `Administrador` y el contrato de binding `Input.PersonaId` se preserva.

## ADDED Requirements

### Requirement: REQ-UCE-08 Pre-poblado de persona en Editar Usuario

En `/seguridad/usuarios/editar/{id}`, `OnGetAsync` MUST recuperar la persona vinculada al usuario (o, si no existiera vínculo activo, quedarse sin selección) y exponerla en el estado `Seleccionada` del selector (REQ-USB-02). `Quitar` MUST volver al estado `Vacío` (REQ-USB-01) y `Cambiar` MUST abrir el popup excluyendo la persona actual de los resultados.

#### Scenario: Editar carga la persona como card preseleccionada

- **DADO** usuario activo con persona activa vinculada
- **CUANDO** un `Administrador` abre `/seguridad/usuarios/editar/{id}`
- **ENTONCES** el selector MUST renderizar la card preseleccionada
- **Y** el botón `Buscar Persona`/`Cambiar` MUST permitir abrir el popup para reemplazarla.

#### Scenario: Quitar en Editar vuelve al estado vacío

- **DADO** editar con `García, Juan` preseleccionada
- **CUANDO** el `Administrador` pulsa `Quitar`
- **ENTONCES** el selector MUST pasar al estado del REQ-USB-01
- **Y** el hidden `Input.PersonaId` MUST quedar `null` en el formulario resultante.

#### Scenario: Cambiar abre el popup sin la persona actual

- **DADO** editar con `García, Juan` preseleccionada
- **CUANDO** el `Administrador` pulsa `Cambiar` o `Buscar Persona`
- **ENTONCES** MUST abrirse el modal `#usuario-persona-buscador-modal`
- **Y** la fila de `García, Juan` MUST NOT figurar entre los resultados del primer `GET /consulta`.

### Requirement: REQ-UCE-09 Banner vacío Crear Usuario cuando no hay candidatas

En `Crear Usuario`, cuando la consulta inicial del selector (`/consulta?soloSinUsuario=true` con cualquier `pageSize` razonable) reporta cero personas activas sin usuario, el formulario MUST mostrar un banner visible con un CTA hacia `/personas/crear`, análogo al patrón actual de dropdown vacío, y el botón `Guardar` SHOULD permanecer deshabilitado hasta que se seleccione una persona.

#### Scenario: Sin personas activas candidatas muestra CTA a Crear Persona

- **DADO** cero personas activas sin usuario en `/consulta?soloSinUsuario=true`
- **CUANDO** un `Administrador` abre `/seguridad/usuarios/crear`
- **ENTONCES** MUST mostrarse un banner con un link `Crear persona` que apunte a `/personas/crear`
- **Y** el selector MUST seguir siendo operable (botón `Buscar Persona` visible)
- **Y** el `submit` SHOULD estar bloqueado mientras `Input.PersonaId` sea `null`.

### Requirement: REQ-UCE-10 Conservación del contrato API ante 409 por Persona duplicada

Al guardar, si `POST /api/v1/usuarios` responde `409` porque la persona ya tiene un usuario activo (anti-join violado por condición de carrera), el selector MUST mostrar feedback de campo equivalente al patrón `Codigo` duplicado de Cargos — error visible en `Input.PersonaId` con opción accionable — sin perder el resto del formulario (`UserName`, `Email`, `Password` y roles) ni el hidden del selector.

#### Scenario: 409 por condición de carrera preserva el formulario

- **DADO** `Crear` con `UserName`/`Email`/`Password`/`Roles` válidos y `PersonaId` que otro request acaba de ocupar
- **CUANDO** el backend responde `409`
- **ENTONCES** el formulario MUST permanecer renderizado con valores previos
- **Y** el selector MUST mostrar `Esa persona ya tiene un usuario activo.` sobre el campo
- **Y** MUST existir un control que permita `Quitar` para limpiar el `Input.PersonaId` o `Cambiar` para reabrir el modal.

## MODIFIED Requirements

### Requirement: REQ-UCE-02 Selector de Persona con buscador modal en Crear Usuario

`OnGetAsync` de crear MUST NO cargar el catálogo completo de personas activas como insumo del campo (deja de invocar `IPersonaOptionsProvider.GetActivasAsync()` como render del campo). El campo MUST exponer el selector modal definido en `usuario-web-selector-persona-buscador`, manteniendo `Input.PersonaId` como hidden input para preservar el binding. El comportamiento de catálogo vacío se delega a REQ-UCE-09.
(Previously: dropdown poblado por `IPersonaOptionsProvider.GetActivasAsync()` con bloqueo o banner según presencia del catálogo.)

#### Scenario: GET Crear expone el buscador sin `<select>` poblado

- **DADO** personas activas disponibles y un `Administrador`
- **CUANDO** solicita `GET /seguridad/usuarios/crear`
- **ENTONCES** MUST existir el botón `Buscar Persona`
- **Y** MUST NOT existir un `<select name="Input.PersonaId">` poblado con `<option>` por persona
- **Y** el campo MUST estar en estado `Vacío` (`Input.PersonaId = null`).

#### Scenario: Persona seleccionada persiste en el hidden

- **DADO** `Crear` con persona elegida en el modal
- **CUANDO** el `Administrador` observa el formulario
- **ENTONCES** MUST existir la card con el formato `Apellido, Nombre (TipoDoc: NroDoc)` o `Legajo`
- **Y** MUST existir el `<input type="hidden" name="Input.PersonaId">` con el id elegido.

#### Scenario: Submit sin persona seleccionada es rechazado

- **DADO** `Crear` con `Input.PersonaId = null`
- **CUANDO** el `Administrador` pulsa `Guardar`
- **ENTONCES** MUST mostrarse el error `Debe seleccionar una persona activa.` en el campo
- **Y** MUST NOT invocarse `POST /api/v1/usuarios`.

#### Scenario: Banner vacío en Crear delega a REQ-UCE-09

- **DADO** cero personas activas sin usuario
- **CUANDO** se renderiza `Crear`
- **ENTONCES** aplica REQ-UCE-09 (banner + CTA), independientemente del nuevo selector.

## Decisiones de especificación

- **Q1 — Catálogo de reemplazo.** `IPersonaOptionsProvider.GetActivasAsync()` deja de alimentar el render del campo. La política de retención (dejarlo en DI, retirarlo, archivarlo) se difiere al `design` — esta spec sólo exige que el campo del formulario no dependa de él.
- **Q2 — Cohabitación con Edit.** La rama `else` actual de `_Form.cshtml` (Persona read-only en Edit) se transforma en la rama `Seleccionada` del selector. La rama `if (!Model.IsEdit)` desaparece como dropdown y se reemplaza por la misma estructura selector en ambos modos, compartiendo el contrato `Input.PersonaId`.
- **Q3 — Cancelación de Edit.** `Quitar` en Edit limpia `Input.PersonaId` pero el handler backend (`PUT /api/v1/usuarios/{id}`) sigue aceptando la edición con el resto del formulario intacto; el guardado sin persona se considera válido para esa rama (verificar invariantes fuera de scope aquí).

## Consideraciones fuera de alcance

- Migraciones, dependencias nuevas, cambios a `IX_AspNetUsers_PersonaId` o a la FK `Restrict`.
- Edición de Persona desde el modal.
- Reorden del Index de Personas.
- Política de retención de `IPersonaOptionsProvider.GetActivasAsync()`.
- Cambios al typeahead `Pages/Personas/Shared/_PersonaTypeahead.cshtml`.
- Switches exhaustivos con `default:` — sigue prohibido en este repo.

## Pruebas de aceptación (strict_tdd)

Las pruebas se redactan ANTES del código y cubren al menos: render sin `<select>` poblado, preselección en Edit, `Quitar` deja `Input.PersonaId` null, `Cambiar` excluye la persona actual, banner vacío con CTA, y feedback de `409` sin perder el resto del formulario. Las páginas siguen exigiendo `Administrador` y los handlers existentes siguen devolviendo `Forbid()` para roles no admin.
