# Especificación de alta y edición web de usuarios

## Purpose

Definir el slice de `Usuarios` en `SGV.Web` para que un `Administrador` cree nuevas cuentas asociadas a `Persona` activa, edite `UserName`/`Email` y roles de cuentas existentes, y reciba feedback claro ante conflictos — sin expandirse a cambio de contraseña, lockout/unlock, baja, reactivación (cubierto por `usuario-web-listado-detalle-baja`) ni CRUD de roles.

## Requirements

### Requirement: REQ-UCE-01 Acceso restringido a Administrador

`Crear` y `Editar` MUST exigir rol `Administrador`. GET MUST redirigir a `/error/403` cuando el usuario autenticado no tiene el rol; POST MUST responder `Forbid()` sin invocar la API.

#### Scenario: GET sin rol admin redirige a 403

- **DADO** un autenticado sin rol `Administrador`
- **CUANDO** solicita `/seguridad/usuarios/crear` o `/seguridad/usuarios/editar/{id}`
- **ENTONCES** MUST redirigir a `/error/403`.

#### Scenario: POST sin rol admin responde Forbid

- **DADO** un autenticado sin rol `Administrador`
- **CUANDO** envía un POST de alta o edición
- **ENTONCES** MUST responder `Forbid()`
- **Y** MUST NOT invocar la mutación contra la API.

### Requirement: REQ-UCE-02 Selector de Persona con buscador modal en Crear Usuario

`OnGetAsync` de crear MUST NO cargar el catálogo completo de personas activas como insumo del campo (deja de invocar `IPersonaOptionsProvider.GetActivasAsync()` como render del campo). El campo MUST exponer el selector modal definido en `usuario-web-selector-persona-buscador`, manteniendo `Input.PersonaId` como hidden input para preservar el binding. El comportamiento de catálogo vacío se delega a REQ-UCE-09.

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

### Requirement: REQ-UCE-03 Validación del formulario Crear

El formulario MUST exigir `UserName` único no vacío, `Email` con formato válido, `Password` que cumpla la política de Identity, al menos un rol del catálogo fijo y un `PersonaId` válido existente. Errores MUST mapearse a campos visibles (`Input.*`) preservando el resto del formulario.

#### Scenario: Validación de unicidad y formato

- **DADO** un `Administrador` completando el alta
- **CUANDO** envía datos con `UserName` duplicado o `Email` inválido
- **ENTONCES** MUST mostrar el error en el campo correspondiente sin perder `PersonaId`, `Password` ni selección de roles.

#### Scenario: Rechazo por Persona inexistente

- **DADO** un `PersonaId` que no existe o ya está inactivo
- **CUANDO** un `Administrador` envía el alta
- **ENTONCES** MUST rechazar la operación con feedback claro
- **Y** NO MUST crear el usuario.

### Requirement: REQ-UCE-04 PRG al detalle tras creación exitosa

Tras `201` en `POST /api/v1/usuarios`, MUST redirigir al detalle del nuevo usuario con feedback success, preservando los filtros del listado.

#### Scenario: Alta exitosa con PRG

- **DADO** datos válidos
- **CUANDO** un `Administrador` envía el alta y el backend responde `201`
- **ENTONCES** MUST redirigir al detalle del nuevo usuario con mensaje visible de éxito.

### Requirement: REQ-UCE-05 Formulario Edit prellenado con datos existentes

`OnGetAsync` de editar MUST recuperar `UsuarioDto` por `id` y prellenar `UserName`, `Email`, `Persona` (read-only) y roles. Persona no consultable MUST mostrar estado recuperable con retorno claro al listado.

#### Scenario: Edit prellena datos

- **DADO** un usuario activo existente
- **CUANDO** un `Administrador` abre `/seguridad/usuarios/editar/{id}`
- **ENTONCES** MUST mostrar los valores actuales y permitir modificar `UserName`, `Email` y roles.

#### Scenario: Edit para usuario no consultable

- **DADO** un identificador de usuario no consultable
- **CUANDO** un `Administrador` abre el editor
- **ENTONCES** MUST mostrar estado recuperable y MUST ofrecer retorno al listado.

### Requirement: REQ-UCE-06 Edición de UserName/Email/roles con PRG

`PUT /api/v1/usuarios/{id}` MUST aplicar cambios de `UserName`, `Email` y roles en una sola operación. Tras `200`, MUST re-redirigir al propio editor con feedback success preservando filtros. `400`/`409`/`403` MUST mapearse a feedback de campo sin perder el resto del formulario.

#### Scenario: Edit exitoso con PRG

- **DADO** un usuario activo existente
- **CUANDO** un `Administrador` guarda cambios y la API responde `200`
- **ENTONCES** MUST re-redirigir al propio editor con feedback success y filtros preservados.

#### Scenario: Conflicto por UserName duplicado

- **DADO** otro usuario activo con el mismo `UserName`
- **CUANDO** un `Administrador` guarda el cambio
- **ENTONCES** MUST mostrar el error en `Input.UserName` sin perder `Email` ni roles.

#### Scenario: Concurrencia con otro Administrador

- **DADO** dos `Administradores` editando el mismo usuario en paralelo
- **CUANDO** ambos guardan cambios casi simultáneamente
- **ENTONCES** la última escritura persistida MUST ser coherente con la API
- **Y** MUST mostrarse feedback claro cuando la edición queda invalidada por otro cambio.

### Requirement: REQ-UCE-07 Catálogo fijo de roles seleccionable

El editor MUST exponer el catálogo de roles SGV (`Administrador`, `GestorVacantes`, `Consultor`) como opciones seleccionables. Roles no presentes en el catálogo MUST NOT aparecer ni ser persistibles.

#### Scenario: Roles fijos seleccionables

- **DADO** un `Administrador` abriendo el editor
- **CUANDO** observa las opciones de roles
- **ENTONCES** MUST ver exclusivamente `Administrador`, `GestorVacantes` y `Consultor` como seleccionables.

#### Scenario: Cambio de roles preserva el resto del formulario

- **DADO** un `Administrador` editando un usuario
- **CUANDO** guarda cambios solo sobre roles
- **ENTONCES** MUST persistir los nuevos roles
- **Y** MUST preservar `UserName` y `Email` sin alterarlos.

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

### Requirement: REQ-UCE-11 Selector único de rol en alta con selección obligatoria

El formulario de alta de usuario (`GET/POST /seguridad/usuarios/crear`) MUST renderizar el campo `Roles` como un único `<select name="Input.Roles">` cuyo primer `<option>` tenga `value=""` y texto `-- Seleccione un rol --` (placeholder obligatorio que `asp-for="Input.Roles"` resuelve como `string`). El POST sin valor en `Input.Roles` MUST ser rechazado por `ModelState` antes de invocar la API, mostrando `Debe seleccionar un rol.` sobre el campo. Tras 400/409 del API, el formulario re-renderizado MUST preservar la selección vigente en el `<select>`. La edición (`/seguridad/usuarios/editar/{id}`) MUST seguir renderizando el campo como checkboxes multi-rol sin cambios.

#### Scenario: GET Crear renderiza `<select>` único con placeholder obligatorio

- **DADO** un `Administrador` autenticado y al menos un rol del catálogo fijo disponible
- **CUANDO** solicita `GET /seguridad/usuarios/crear`
- **ENTONCES** MUST existir exactamente un `<select name="Input.Roles">`
- **Y** MUST existir dentro un `<option value="">-- Seleccione un rol --</option>`
- **Y** MUST haber un `<option>` por cada rol del catálogo fijo (`Administrador`, `GestorVacantes`, `Consultor`).

#### Scenario: GET Editar conserva checkboxes multi-rol

- **DADO** un `Administrador` autenticado
- **CUANDO** solicita `GET /seguridad/usuarios/editar/{id}`
- **ENTONCES** MUST existir `<input type="checkbox" name="Input.Roles" value="...">` por cada rol del catálogo fijo
- **Y** MUST NOT existir un `<select name="Input.Roles">`.

#### Scenario: POST alta sin rol es rechazado antes de invocar la API

- **DADO** un `Administrador` enviando el alta con `Input.Roles` ausente o vacío
- **CUANDO** pulsa `Guardar`
- **ENTONCES** `ModelState` MUST ser inválido con el mensaje `Debe seleccionar un rol.` ligado al campo `Input.Roles`
- **Y** MUST NOT invocarse `POST /api/v1/usuarios`.

#### Scenario: POST alta con un rol envía un único elemento a la API

- **DADO** un `Administrador` con el resto del formulario válido y `Input.Roles` con un único rol del catálogo
- **CUANDO** pulsa `Guardar`
- **ENTONCES** la solicitud a `POST /api/v1/usuarios` MUST contener `Roles` con exactamente un elemento
- **Y** MUST NOT contener marcas de checkbox adicionales fuera del binding.

#### Scenario: Tras 400/409 el rol seleccionado se preserva en el `<select>`

- **DADO** un `POST` de alta con un rol seleccionado y datos que producen `400` o `409` del API
- **CUANDO** el formulario se re-renderiza con el error de campo
- **ENTONCES** el `<select name="Input.Roles">` MUST tener el `<option>` del rol seleccionado con atributo `selected`
- **Y** MUST preservarse el resto del formulario (`UserName`, `Email`, `PersonaId`).

## Out of scope

- No incluye baja lógica ni reactivación — cubierto por `usuario-web-listado-detalle-baja`.
- No incluye cambio de contraseña desde la UI administrativa.
- No incluye CRUD de roles ni bloqueos de cuenta.
