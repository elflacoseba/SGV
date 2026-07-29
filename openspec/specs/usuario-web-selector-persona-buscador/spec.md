# Especificación: Selector modal de Persona con buscador — Crear/Editar Usuario

## Propósito

Definir el selector reutilizable basado en modal Bootstrap 5 que reemplaza el combo plano de personas activas en los formularios `Crear Usuario` y `Editar Usuario` de `SGV.Web`. El selector pagina server-side vía `GET /api/v1/personas/consulta`, filtra a personas activas sin usuario asociado y conserva el contrato de binding existente (`Input.PersonaId`). Adicionalmente, el handler BFF same-origin de la consulta endurece sus entradas: limita `?search` a 200 caracteres, valida `?sort=` y `?segmento=` contra whitelists cerradas antes de invocar `IPersonaApiClient`, y preserva defaults back-compat. La spec no impone restricciones al dominio, no requiere migración, no toca el typeahead de `Pages/Personas/Shared/`, no cambia el constraint `IX_AspNetUsers_PersonaId` ni la FK `Restrict`, y no agrega dependencias.

## Requisitos

### Requirement: REQ-USB-01 Estado vacío inicial del selector

Cuando `Input.PersonaId` es `null`, el campo MUST renderizar el botón disparador `Buscar Persona` (label español vigente) y MUST NOT renderizar un `<select>` que cargue el catálogo completo de personas activas.

#### Scenario: Crear sin persona seleccionada expone el buscador

- **DADO** un `Administrador` abriendo `/seguridad/usuarios/crear`
- **CUANDO** renderiza el formulario con `Input.PersonaId = null`
- **ENTONCES** MUST existir el botón `Buscar Persona` (`data-usuario-persona-buscar` o equivalente funcional)
- **Y** el HTML MUST NOT contener un `<select name="Input.PersonaId">` poblado con `<option>` por persona.

### Requirement: REQ-USB-02 Card de persona seleccionada

Cuando hay persona seleccionada, el campo MUST mostrar una card con `Apellido, Nombre (TipoDocumento: NumeroDocumento)` — cayendo a `Legajo` cuando no hay documento — más botones `Quitar` y `Cambiar`. El id MUST vivir en un `<input type="hidden" asp-for="Input.PersonaId">` para preservar el contrato del modelo.

#### Scenario: Persona seleccionada se renderiza como card y oculta como hidden

- **DADO** `Input.PersonaId` apuntando a persona activa con documento
- **CUANDO** el formulario se renderiza
- **ENTONCES** MUST aparecer la card con el formato `Apellido, Nombre (TipoDoc: NroDoc)`
- **Y** MUST existir el hidden input con el id en `Input.PersonaId`.

#### Scenario: Persona sin documento muestra Legajo

- **DADO** `Input.PersonaId` apuntando a persona activa sin `TipoDocumento`/`NumeroDocumento`
- **CUANDO** el formulario se renderiza
- **ENTONCES** la card MUST mostrar el `Legajo` en lugar del bloque de documento.

### Requirement: REQ-USB-03 Modal Bootstrap 5 con búsqueda lazy

Al pulsar el disparador, el sistema MUST abrir el modal `#usuario-persona-buscador-modal` con foco en el input de búsqueda, placeholder `Buscar por legajo, nombre, apellido, email o documento`, y la búsqueda se dispara al pulsar `Enter` o el botón `Buscar` (sin recarga) sobre los campos `Legajo|Apellidos|Nombres|Email|NumeroDocumento` en forma case-insensitive por subcadena. El JS MUST conditionalizar el parámetro `soloSinUsuario` del `GET /api/v1/personas/consulta` según el atributo `data-solo-sin-usuario` del modal raíz (REQ-USB-12); cuando el atributo está ausente o inválido, defaultea a `true`.

#### Scenario: Apertura enfoca el input y renderiza placeholder

- **DADO** el selector en estado vacío y foco en el disparador
- **CUANDO** se hace click en `Buscar Persona`
- **ENTONCES** MUST abrirse el modal con `aria-hidden="false"`
- **Y** el foco inicial MUST estar en el input de búsqueda
- **Y** el placeholder visible MUST ser exactamente `Buscar por legajo, nombre, apellido, email o documento`.

#### Scenario: Búsqueda al pulsar Enter desde Usuarios envía `soloSinUsuario=true`

- **DADO** el modal de Usuarios abierto con texto `garcia`
- **CUANDO** el `Administrador` pulsa `Enter`
- **ENTONCES** MUST dispararse un único `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=true&p=1&pageSize=25`
- **Y** el input MUST mantener el texto `garcia` durante el request.

#### Scenario: Búsqueda desde modal con `data-solo-sin-usuario="false"` omite el parámetro

- **DADO** un modal reutilizado con `data-solo-sin-usuario="false"` (e.g., Ocupaciones) abierto con texto `garcia`
- **CUANDO** el Administrador pulsa `Enter`
- **ENTONCES** MUST dispararse `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=false&p=1&pageSize=25`
- **Y** el input MUST mantener el texto `garcia` durante el request.

### Requirement: REQ-USB-04 Tabla paginada server-side

El modal MUST renderizar una tabla paginada con `pageSize=25`, columnas `Apellido y Nombre | Documento | Legajo | Email | Acción`, cada celda de cabecera MUST ser `<th scope="col">`, y la paginación MUST ofrecer navegación `Anterior`/`Siguiente` (y/o numérica) que preserve el texto de búsqueda actual.

#### Scenario: Render de tabla con paginación 25

- **DADO** el modal abierto y el `Administrador` pulsó `Buscar`
- **CUANDO** `/consulta` responde con un `PagedResult<PersonaDto>` con `pageSize=25`
- **ENTONCES** el modal MUST renderizar hasta 25 filas con las columnas `Apellido y Nombre | Documento | Legajo | Email | Acción`
- **Y** MUST existir controles `Anterior`/`Siguiente` deshabilitados según haya más páginas.

### Requirement: REQ-USB-05 Estados visuales del modal

El modal MUST distinguir cuatro estados visibles para el usuario, sin mezclarlos: **Inicial** (sin texto en input y sin request previo → mensaje `Ingresá un texto para buscar personas.`), **Empty** (0 resultados → `No se encontraron personas con ese criterio.`), **Loading** (request en vuelo → spinner y deshabilitar el botón `Buscar` y los controles de paginación), y **Error de transporte** (fallo recuperable → `No se pudo conectar con el servidor. Reintentá.` preservando el texto y sin cerrar el modal).

#### Scenario: Estado Inicial antes de la primera búsqueda

- **DADO** el modal recién abierto sin texto tipeado
- **CUANDO** se renderiza por primera vez
- **ENTONCES** MUST mostrarse el mensaje `Ingresá un texto para buscar personas.`
- **Y** MUST NOT haber consulta a la API disparada.

#### Scenario: Estado Empty con 0 resultados

- **DADO** búsqueda `zzzzz` sin coincidencias
- **CUANDO** `/consulta` responde `200` con `items=[]` y `totalCount=0`
- **ENTONCES** MUST mostrarse `No se encontraron personas con ese criterio.`
- **Y** MUST existir el botón `Buscar` operativo para reintentar.

#### Scenario: Estado Loading deshabilita controles

- **DADO** un request en curso al modal
- **CUANDO** el `Administrador` observa la UI
- **ENTONCES** MUST existir un spinner visible y los controles `Buscar`, `Anterior`, `Siguiente` MUST estar deshabilitados.

#### Scenario: Error de transporte preserva texto y abre reintento

- **DADO** el request falla por error de transporte (red, timeout, 5xx)
- **CUANDO** el handler clasifica el fallo
- **ENTONCES** MUST mostrarse `No se pudo conectar con el servidor. Reintentá.`
- **Y** el input MUST preservar el texto tipeado
- **Y** el modal MUST permanecer abierto para reintento manual.

### Requirement: REQ-USB-06 Selección aplica y persiste el id

Al pulsar el botón `Seleccionar` de una fila, el modal MUST cerrarse, MUST setear `<input type="hidden" name="Input.PersonaId">` con el `Id` de la persona elegida, MUST actualizar la card visible con el formato de REQ-USB-02, y MUST disparar un `change` event sobre el hidden para que el binding de `UsuarioInputModel` se mantenga sincronizado. Si ya había persona, el reemplazo MUST ocurrir sin confirmación.

#### Scenario: Seleccionar desde la tabla setea el id y cierra el modal

- **DADO** el modal abierto mostrando la fila de `García, Juan (DNI: 12345678)`
- **CUANDO** el `Administrador` pulsa `Seleccionar` de esa fila
- **ENTONCES** MUST cerrarse el modal
- **Y** la card visible MUST mostrar `García, Juan (DNI: 12345678)`
- **Y** el hidden `Input.PersonaId` MUST tener el valor del `Id` correspondiente.

#### Scenario: Reemplazo de persona anterior sin confirmación

- **DADO** persona `Pérez, Ana` ya seleccionada y modal abierto
- **CUANDO** el `Administrador` pulsa `Seleccionar` sobre `García, Juan`
- **ENTONCES** la card MUST pasar de `Pérez, Ana` a `García, Juan` sin mostrar diálogo de confirmación.

### Requirement: REQ-USB-07 Cierre del modal sin elegir

Pulsar `Cerrar`, `Esc`, hacer click sobre el backdrop o sobre el botón `X` MUST cerrar el modal SIN modificar `Input.PersonaId`, y al cerrar MUST devolverse el foco al botón disparador original.

#### Scenario: Cerrar con Esc no cambia la selección

- **DADO** el modal abierto con una búsqueda en curso o ya cargada
- **CUANDO** se pulsa `Esc`
- **ENTONCES** el modal MUST cerrarse
- **Y** `Input.PersonaId` MUST conservar el valor previo (sea `null` o el id anterior)
- **Y** el foco MUST volver al elemento que disparó el modal.

#### Scenario: Cerrar por backdrop o X

- **DADO** el modal abierto
- **CUANDO** se hace click en backdrop o en el botón `X`
- **ENTONCES** MUST cerrarse sin elegir
- **Y** el foco MUST volver al disparador.

### Requirement: REQ-USB-08 Preselección en Edición

En `/seguridad/usuarios/editar/{id}`, cuando la persona vinculada existe, el selector MUST renderizarse en estado `Seleccionada` con la card visible, y al abrir el popup la persona actual MUST estar excluida de los resultados hasta que se pulse `Quitar` o `Cambiar`. `Quitar` MUST volver el selector al estado `Vacío` (REQ-USB-01) sin invocar la API.

#### Scenario: Editar carga la persona actual como card

- **DADO** usuario activo con persona vinculada `García, Juan`
- **CUANDO** se renderiza `/seguridad/usuarios/editar/{id}`
- **ENTONCES** MUST existir la card con `García, Juan (DNI: 12345678)`
- **Y** MUST existir el hidden `Input.PersonaId` con ese id.

#### Scenario: Quitar limpia el selector

- **DADO** `Editar` con `García, Juan` preseleccionada
- **CUANDO** el `Administrador` pulsa `Quitar`
- **ENTONCES** el campo MUST pasar al estado del REQ-USB-01 (botón `Buscar Persona`, sin card, sin `<select>` poblado)
- **Y** `Input.PersonaId` MUST quedar `null`.

#### Scenario: Cambiar abre el popup ocultando la persona actual

- **DADO** `Editar` con `García, Juan` preseleccionada
- **CUANDO** el `Administrador` pulsa `Cambiar`
- **ENTONCES** MUST abrirse el modal
- **Y** la fila de `García, Juan` MUST NOT estar en los resultados.

### Requirement: REQ-USB-09 Accesibilidad AA del modal

El modal MUST tener `role="dialog"`, `aria-modal="true"`, `aria-labelledby` apuntando al título, el botón `Seleccionar` de cada fila MUST llevar `aria-label="Seleccionar a {Apellido}, {Nombre}"`, y regiones con contenido dinámico (mensajes de estado, resultados) SHOULD usar `aria-live="polite"`.

#### Scenario: Atributos de accesibilidad presentes en el HTML

- **DADO** el modal renderizado
- **CUANDO** un test inspecciona el HTML
- **ENTONCES** el contenedor MUST tener `role="dialog"` y `aria-modal="true"`
- **Y** el `<h5>` del título MUST estar referenciado por `aria-labelledby`
- **Y** los botones `Seleccionar` MUST incluir `aria-label` con `Apellido` y `Nombre` de la fila.

### Requirement: REQ-USB-10 Listado exclusivo de activas sin usuario

El modal, cuando se invoca desde el formulario Crear/Editar Usuario (default `soloSinUsuario=true` o atributo ausente), MUST listar exclusivamente personas activas (`IsActive=true` y `IsDeleted=false`) que NO tengan un usuario activo asociado (`AspNetUsers.PersonaId IS NULL`), independientemente de la versión client-side del catálogo `IPersonaOptionsProvider.GetActivasAsync()`. Cuando el modal se reutiliza desde otros contextos con `data-solo-sin-usuario="false"` (e.g., Ocupaciones), el filtro `soloSinUsuario` MUST NO aplicarse y el listado MAY incluir personas con usuario activo asociado, quedando la decisión de exclusión fuera del scope de este requisito (ver `ocupacion-web-selector-persona-buscador`).

#### Scenario: Solo activas sin usuario en `/consulta` desde Usuarios

- **DADO** una persona activa sin usuario
- **Y** una persona activa con usuario activo
- **Y** una persona eliminada sin usuario
- **CUANDO** el modal de Usuarios invoca `/consulta?soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener solo la persona activa sin usuario
- **Y** MUST NOT contener personas con usuario ni personas eliminadas.

#### Scenario: Modal reutilizado con `soloSinUsuario=false` no filtra por usuario

- **DADO** una persona activa con usuario activo asociado
- **Y** una persona activa sin usuario
- **CUANDO** un modal reutilizado con `data-solo-sin-usuario="false"` invoca `/consulta?soloSinUsuario=false&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener AMBAS personas activas
- **AND** MUST seguir excluyendo personas eliminadas (filtro `Segmento=Activas` aplica ortogonalmente).

### Requirement: REQ-USB-11 Error de carrera 409 al guardar

Si `POST /api/v1/usuarios` (Crear) responde `409` por conflicto de unicidad de `PersonaId`, el selector MUST mostrar feedback de campo equivalente al patrón `Codigo` duplicado de Cargos — error visible en `Input.PersonaId` con opción accionable — sin perder el resto del formulario (UserName, Email, Password, roles) ni el hidden del selector.

#### Scenario: 409 en Crear muestra error en el selector sin perder input

- **DADO** `Crear` con `UserName`/`Email`/`Password`/`Roles` válidos y `PersonaId` ya ocupado por otro usuario activo
- **CUANDO** el backend responde `409`
- **ENTONCES** el campo MUST mostrar `Esa persona ya tiene un usuario activo.`
- **Y** MUST preservarse `UserName`, `Email`, `Password` y los roles en el formulario
- **Y** el `Input.PersonaId` hidden MUST seguir seteado al id que motivó el 409.

### Requirement: REQ-USB-12 Configuración del modal via `data-solo-sin-usuario`

El modal reutilizable `_PersonaBuscadorModal.cshtml` y el script `usuario-persona-buscador.js` MUST soportar el atributo `data-solo-sin-usuario` en el contenedor raíz del modal. Cuando el atributo está presente, el JS MUST leerlo (parseo case-insensitive: `"true"|"false"` → booleano) y conditionalizar `url.searchParams.set("soloSinUsuario", valor)` en cada invocación a `GET /api/v1/personas/consulta`. Cuando el atributo está AUSENTE o no es parseable, el JS MUST defaultear a `true` (preserva el comportamiento vigente de Usuarios). El script MUST NO hardcodear `soloSinUsuario=true` sin lectura del atributo.

#### Scenario: Modal Usuarios sin atributo mantiene `soloSinUsuario=true`

- **DADO** el modal de `/seguridad/usuarios/crear` sin `data-solo-sin-usuario` (o valor inválido)
- **CUANDO** el Administrador dispara una búsqueda con texto `garcia`
- **ENTONCES** MUST construirse `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=true&p=1&pageSize=25`
- **Y** el listado MUST excluir personas con usuario activo asociado.

#### Scenario: Modal Ocupaciones con `data-solo-sin-usuario="false"` omite el filtro

- **DADO** el modal de Ocupaciones con `data-solo-sin-usuario="false"` en el raíz
- **CUANDO** se dispara una búsqueda con texto `garcia`
- **ENTONCES** MUST construirse `GET /api/v1/personas/consulta?search=garcia&soloSinUsuario=false&p=1&pageSize=25`
- **Y** el listado MAY incluir personas con usuario activo asociado.

#### Scenario: Atributo `data-solo-sin-usuario` con casing/value variants normaliza

- **DADO** un modal con `data-solo-sin-usuario="False"` (mayúsculas)
- **CUANDO** el JS parsea el atributo vía comparación case-insensitive contra `"true"`
- **ENTONCES** MUST interpretarse como `false`
- **Y** MUST construirse la URL con `soloSinUsuario=false`.

#### Scenario: Script backwards-compatible sin cambios de comportamiento en Usuarios

- **DADO** la suite de tests existente de `usuario-web-selector-persona-buscador` (REQ-USB-03, REQ-USB-10)
- **CUANDO** se ejecutan sin modificar markup de Usuarios
- **ENTONCES** los resultados observados MUST ser idénticos a los previos al change
- **Y** MUST NO requierirse cambios al markup de `_PersonaBuscadorModal.cshtml` para preservar el comportamiento de Usuarios.

### Requirement: BFF acota `?search` a 200 caracteres

El handler BFF MUST aceptar `?search` con hasta `200` caracteres y reenviarlo al `PersonaListQuery`. Si supera `200`, MUST responder `400` con `ProblemDetails` describiendo el límite y MUST NOT invocar `IPersonaApiClient`.

#### Scenario: BFF reenvía `?search` de exactamente 200 caracteres

- **DADO** un request autenticado a `GET /api/v1/personas/consulta` con `?search=` de exactamente `200` caracteres válidos
- **CUANDO** el handler valida y construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Search` igual al valor recibido y responder `200 OK`.

#### Scenario: BFF rechaza `?search` de 201 caracteres

- **DADO** un request autenticado a `GET /api/v1/personas/consulta` con `?search=` de `201` caracteres
- **CUANDO** el handler detecta el exceso
- **ENTONCES** MUST responder `400` con `ProblemDetails` cuyo `detail` mencione el límite de `200` caracteres para `search`
- **Y** MUST NOT invocar `IPersonaApiClient.QueryAsync`.

### Requirement: BFF acepta `?sort=` con whitelist cerrada

El handler BFF MUST aceptar `?sort=` únicamente cuando su valor (case-insensitive, vía `ToLowerInvariant()`) coincide con uno de los ocho tokens `apellidos_asc`, `apellidos_desc`, `nombres_asc`, `nombres_desc`, `legajo_asc`, `legajo_desc`, `email_asc` o `email_desc`. Cualquier otro valor (incluido `documento_asc`/`documento_desc`) MUST responder `400` con `ProblemDetails` enumerando los tokens válidos y MUST NOT invocar `IPersonaApiClient`.

#### Scenario: BFF acepta un token válido de la whitelist

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?sort=email_desc`
- **CUANDO** el handler valida
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Sort="email_desc"` y responder `200 OK`.

#### Scenario: BFF rechaza token fuera de la whitelist

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?sort=documento_asc`
- **CUANDO** el handler valida
- **ENTONCES** MUST responder `400` con `ProblemDetails` cuyo `detail` liste los ocho tokens válidos para `sort`
- **Y** MUST NOT invocar `IPersonaApiClient.QueryAsync`.

### Requirement: BFF acepta `?segmento=` con whitelist cerrada

El handler BFF MUST aceptar `?segmento=` únicamente cuando su valor (case-insensitive) es `activas` o `eliminadas`, mapeando a `PersonaSegmentoListado.Activas` o `PersonaSegmentoListado.Eliminadas` respectivamente. Cualquier otro valor MUST responder `400` con `ProblemDetails` enumerando los valores válidos y MUST NOT invocar `IPersonaApiClient`.

#### Scenario: BFF acepta `?segmento=eliminadas`

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?segmento=eliminadas`
- **CUANDO** el handler construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Segmento=PersonaSegmentoListado.Eliminadas` y responder `200 OK`.

#### Scenario: BFF rechaza `?segmento=` fuera de la whitelist

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?segmento=todas`
- **CUANDO** el handler valida
- **ENTONCES** MUST responder `400` con `ProblemDetails` cuyo `detail` indique que `segmento` debe ser `activas` o `eliminadas`
- **Y** MUST NOT invocar `IPersonaApiClient.QueryAsync`.

### Requirement: BFF preserva defaults back-compat

Cuando el request no incluye `?sort=` o `?segmento=`, el handler BFF MUST aplicar los defaults `Sort="apellidos_asc"` y `Segmento=PersonaSegmentoListado.Activas`. Si sólo uno de los dos está presente y es válido, MUST respetarlo y mantener el default del otro.

#### Scenario: BFF aplica defaults cuando faltan ambos parámetros

- **DADO** un request autenticado a `GET /api/v1/personas/consulta` sin `?sort=` ni `?segmento=`
- **CUANDO** el handler construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Sort="apellidos_asc"` y `Segmento=PersonaSegmentoListado.Activas`.

#### Scenario: BFF respeta un parámetro válido y mantiene el default del otro

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?sort=nombres_desc`
- **CUANDO** el handler construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Sort="nombres_desc"` y `Segmento=PersonaSegmentoListado.Activas` (default preservado).

## Decisiones de especificación

- **Q1 — Nombre del query param.** Se fija `soloSinUsuario=true|false` (camelCase, ortogonal al `Segmento`). Default `false`/`null` preserva el comportamiento vigente de `/consulta` y mantiene back-compat de consumidores como el listado web de Personas.
- **Q2 — Listado combinado con búsqueda.** El filtro `soloSinUsuario` se compone con `search`, `sort`, `page` y `pageSize` exactamente igual que el resto: antes de `Skip/Take`. La segmentación `Eliminadas` sigue siendo excluyente (ver requisito añadido en `persona-management`).
- **Q3 — Hook de selección.** El botón `Seleccionar` por fila es la única vía de elección; el simple click sobre la fila NO selecciona. Decidido para evitar errores accidentales por teclado mientras se navega con flechas en el futuro.
- **Q4 — Whitelist sort alineada con `PersonaRepository.ApplySort`.** Los 8 tokens replican exactamente los soportados por el repositorio. `documento_asc/desc` queda explícitamente excluido. Si el negocio lo requiere, debe sumarse primero a `ApplySort` como change separado.
- **Q5 — Validación en BFF (no backend).** Cortar antes de invocar `IPersonaApiClient` reduce latencia, logs y superficie de ataque contra `SGV.Api`. El backend no rechaza valores inválidos — cae silenciosamente al default.
- **Q6 — NO se introduce `PersonaSort` enum.** El campo `Sort` de `PersonaListQuery` sigue siendo `string?`. Introducir el enum duplicaría la fuente de verdad con `ApplySort` y rompería el contrato del API.

## Consideraciones fuera de alcance

- Reutilización del modal desde módulos distintos a `Crear/Editar Usuario` y `Crear/Editar Ocupación` (mecanismo de configuración habilitado via `data-solo-sin-usuario`; otros módulos requieren change separado).
- Edición de `Persona` desde adentro del modal.
- Reorden o cambios al Index de `Pages/Personas/`.
- Cambios al typeahead `Pages/Personas/Shared/_PersonaTypeahead.cshtml`.
- Política de retención de `IPersonaOptionsProvider.GetActivasAsync()` — diferida al `design`.
- Dependencias, migraciones, constraints nuevos o cambios a FK.
- Extender `PersonaRepository.ApplySort` con `documento_asc`/`documento_desc` u otros tokens no implementados hoy.
- Modificar `FakePersonaApiClient`, `PersonaRepository`, `PersonaListQuery` o migraciones.
- Introducir un enum `PersonaSort`: `PersonaListQuery.Sort` permanece como `string?`.
- Mover el handler BFF fuera de `Program.cs` o resolver otros findings del issue #157.
- Cambiar el cap de `?search` a un valor distinto de `200` o sustituir `PersonaSegmentoListado`.

## Pruebas de aceptación (strict_tdd)

Las pruebas se redactan ANTES del código y cubren al menos: estados del modal (Inicial/Empty/Loading/Error), paginación 25, `soloSinUsuario` server-side, preselección en Edit, accesibilidad AA mínima, cierre sin elegir, y feedback de `409` sin perder el resto del formulario.
