# Especificación de Persona Management

## Propósito

El sistema DEBE administrar Personas como entidades independientes con datos básicos, identificación, contacto y estado activo/inactivo. Este corte NO DEBE incluir Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`.

## Requisitos

### Requisito: Datos Administrables de Persona

El sistema MUST administrar Personas con datos básicos, identificación, contacto y estado activo/inactivo. Este corte MUST NOT incluir Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`. Las respuestas MUST ser modelos seguros para consumidores y MUST NOT exponer entidades de dominio, entidades de persistencia, auditoría interna ni navegaciones excluidas.

#### Escenario: Consultar detalle de persona

- **DADO** que existe una Persona persistida
- **CUANDO** se consulta su detalle administrativo
- **ENTONCES** el sistema DEBE devolver sus datos básicos, identificación, contacto y estado
- **Y** NO DEBE incluir Postulantes, Ocupaciones, Habilidades ni `PersonaHabilidad`.

#### Escenario: Listar personas

- **DADO** que existen Personas persistidas
- **CUANDO** se solicita el listado administrativo
- **ENTONCES** el sistema DEBE devolver una colección de Personas con contrato consumer-safe.

### Requisito: Alta de Persona

El sistema MUST permitir crear Personas con datos válidos. `Legajo` MAY omitirse; cuando se informa, MUST respetar `Longitud ≤ 50` y, si es no-nulo/no-vacío, MUST ser único entre Personas activas. El formulario web MUST tratar `Legajo` whitespace-only como `null` antes de invocar la API, de modo que el backend persista `Legajo = NULL`. `Email` y documento MAY omitirse, pero si se informan MUST respetar formato/longitud y unicidad activa. Cuando se informa documento, `TipoDocumentoId` MUST referenciar un `TipoDocumento` seedeado, `NumeroDocumento` MUST satisfacer el `PatronValidacion` y rango del tipo referenciado, y el cambio queda registrado en `Auditorias`.
(Previously: `Legajo` era MUST ser requerido y único entre Personas activas; ahora puede omitirse manteniendo la unicidad cuando tiene valor, y la UI MUST normalizar whitespace antes de invocar la API.)

#### Escenario: Crear persona válida con Legajo y tipo de documento

- **DADO** que no existe una Persona activa con el mismo `Legajo`, `Email` ni documento informado
- **Y** existe `TipoDocumento` con `Codigo="DNI"`
- **CUANDO** se crea una Persona con `Legajo="L-001"`, `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12345678"`
- **ENTONCES** el sistema DEBE persistirla como activa
- **Y** DEBE devolver su identificador y datos administrables.

#### Escenario: Crear persona omitiendo Legajo

- **DADO** un `CrearPersonaRequest` válido con `legajo` ausente o `legajo: null`
- **Y** que no existe otra Persona activa con el mismo `Email` o documento informado
- **CUANDO** se crea la Persona
- **ENTONCES** el sistema DEBE persistirla como activa con `Legajo = NULL`
- **Y** la API DEBE responder `201 Created`.

#### Escenario: Crear persona con Legajo whitespace-only

- **DADO** un POST a `/personas/crear` donde el operador envía `Legajo = "   "`
- **CUANDO** el PageModel normaliza a `null` antes de invocar la API
- **ENTONCES** el request serializado MUST contener `legajo: null` o la clave ausente
- **Y** el backend MUST persistir `Legajo = NULL` y responder `201 Created`.

#### Escenario: Rechazar Legajo que excede 50 caracteres

- **DADO** un `CrearPersonaRequest` con `Legajo` de longitud > 50
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `Legajo`
- **Y** la API DEBE responder `400 Bad Request` con `FieldErrors["Legajo"]`.

#### Escenario: Rechazar Legajo duplicado entre Personas activas

- **DADO** que existe una Persona activa con `Legajo="L-001"`
- **CUANDO** se crea o reactiva otra Persona con ese mismo `Legajo` no-nulo/no-vacío
- **ENTONCES** el sistema DEBE rechazar con `409 Conflict`
- **Y** MUST NOT romper el invariante de unicidad activa.

#### Escenario: Rechazar documento que no satisface patrón del tipo

- **DADO** un `CrearPersonaRequest` con `TipoDocumentoId=<Id de Pasaporte>` y `NumeroDocumento="12345"`
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `NumeroDocumento`
- **Y** la API DEBE responder `400 Bad Request` con `FieldErrors`.

### Requisito: Actualización de Persona

El sistema MUST permitir actualizar datos propios de Persona sin modificar relaciones fuera de alcance. La transición `Legajo` no-nulo → `null` MAY dispararse desde el formulario web y, cuando ocurre, el sistema MUST emitir una fila en `Auditorias` con `Entidad="Persona"`, `Accion="UpdateLegajo"`, `IdEntidad=<Persona.Id>`, `Usuario`, `FechaHora`, `ValoresAnteriores={"LegajoAnterior":<valor previo>}` y `ValoresNuevos={"LegajoNuevo":null}`. La unicidad activa de `Legajo` no-nulo/no-vacío, `Email` y documento MUST preservarse. Cuando se modifica `TipoDocumentoId` o `NumeroDocumento`, la nueva combinación MUST satisfacer el patrón y rango del tipo seleccionado y el cambio MUST quedar registrado en `Auditorias`.
(Previously: el requisito no describía la limpieza de `Legajo` ni el evento de auditoría explícito asociado; ahora se permiten ambas con los nombres canónicos `UpdateLegajo`, `LegajoAnterior`, `LegajoNuevo`.)

#### Escenario: Actualizar contacto preservando documento válido

- **DADO** que existe una Persona activa
- **CUANDO** se actualizan datos de contacto válidos y el documento cumple el patrón del tipo seleccionado
- **ENTONCES** el sistema DEBE persistir los cambios
- **Y** NO DEBE alterar relaciones excluidas.

#### Escenario: Editar limpiando Legajo persiste null y registra auditoría UpdateLegajo

- **DADO** una Persona activa con `Legajo="L-001"`
- **Y** un POST a `/personas/editar/{id}` con `Legajo` ausente o `null`
- **CUANDO** el servicio de comandos aplica el cambio
- **ENTONCES** la Persona DEBE persistirse con `Legajo = NULL`
- **Y** la tabla `Auditorias` MUST contener una fila con `Accion="UpdateLegajo"`, `LegajoAnterior="L-001"`, `LegajoNuevo=null`, `PersonaId=<Id>`, `Usuario=<sub del JWT>`.

#### Escenario: Editar con Legajo whitespace-only se normaliza a null antes de la API

- **DADO** una Persona activa con `Legajo="L-001"`
- **Y** el operador envía en el formulario `Legajo = "   "`
- **CUANDO** el PageModel normaliza a `null` antes de invocar la API
- **ENTONCES** el request serializado MUST contener `legajo: null` o la clave ausente
- **Y** el backend MUST persistir `Legajo = NULL` y registrar la auditoría `UpdateLegajo`.

#### Escenario: Editar sin transición de Legajo no genera fila UpdateLegajo

- **DADO** una Persona activa con `Legajo="L-001"`
- **CUANDO** se aplica un update que mantiene `Legajo="L-001"` y modifica otros campos
- **ENTONCES** MUST NOT existir una fila con `Accion="UpdateLegajo"` para ese `PersonaId` y `FechaHora`
- **Y** los demás eventos del interceptor centralizado MUST seguir emitiéndose normalmente.

#### Escenario: Rechazar cambio de documento que rompe patrón

- **DADO** una Persona activa con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12345678"`
- **CUANDO** se envía un update con `NumeroDocumento="12A45678"`
- **ENTONCES** el sistema DEBE responder `400 Bad Request` con `FieldErrors["NumeroDocumento"]`.

#### Escenario: Rechazar duplicados activos

- **DADO** que existe otra Persona activa con el mismo `Legajo` no-nulo/no-vacío, `Email` o documento
- **CUANDO** se intenta crear, actualizar o reactivar una Persona con esos valores
- **ENTONCES** el sistema DEBE rechazar la operación con un conflicto claro.

### Requisito: Auditoría explícita al limpiar Legajo de Persona

Cuando `PersonaServicioComandos.ActualizarAsync` aplica una transición `Legajo` no-nulo → `null`, el sistema MUST invocar `IAuditoriaServicio.RegistrarAsync(...)` con `Entidad="Persona"`, `IdEntidad=<Persona.Id>`, `Accion="UpdateLegajo"`, `Usuario=<usuario autenticado>`, `FechaHora=UTC now`, `ValoresAnteriores={"LegajoAnterior":<valor previo>}` y `ValoresNuevos={"LegajoNuevo":null}`. La auditoría MUST emitirse dentro de la misma unidad lógica que el `SaveChanges` de la actualización y es independiente del origen del cambio (formulario web, consumidor HTTP autenticado o `ReactivarAsync` que modifique `Legajo`). El interceptor centralizado de auditoría continúa aplicando los demás eventos `Update`/`Delete` sin verse afectado por esta regla explícita.

#### Escenario: Limpieza de Legajo vía formulario web registrada con UpdateLegajo

- **DADO** una Persona activa con `Legajo="L-001"` accedida desde `/personas/editar/{id}` por un `Administrador`
- **CUANDO** el operador limpia el campo y confirma el POST
- **ENTONCES** la fila de `Auditorias` MUST tener `Accion="UpdateLegajo"`, `LegajoAnterior="L-001"`, `LegajoNuevo=null`, `PersonaId=<Id>`, `Usuario=<sub del JWT>`.

#### Escenario: Limpieza de Legajo vía consumidor autenticado no-web registrada

- **DADO** un cliente HTTP autenticado que envía `ActualizarPersonaRequest` con `legajo` ausente o `null` contra una Persona con `Legajo="L-099"`
- **CUANDO** `PersonaServicioComandos.ActualizarAsync` aplica el cambio
- **ENTONCES** la fila de auditoría MUST emitirse igualmente con `Accion="UpdateLegajo"`, `LegajoAnterior="L-099"`, `LegajoNuevo=null`.

#### Escenario: Persona con Legajo previamente null no genera UpdateLegajo en update sin transición

- **DADO** una Persona activa con `Legajo=NULL`
- **CUANDO** se aplica un update con `legajo` ausente o `legajo: null`
- **ENTONCES** MUST NOT existir una fila con `Accion="UpdateLegajo"` porque no hay transición no-nulo → null.

### Requisito: Ciclo de Vida de Persona

El sistema MUST permitir desactivar y reactivar Personas mediante baja lógica. Las consultas activas MUST excluir Personas inactivas por defecto. Una Persona con usuario autenticable asociado MUST conservar el vínculo histórico; cualquier restricción operativa sobre su desactivación MUST ser explícita y MUST NOT crear usuarios sin Persona.
(Previously: el requisito cubría baja/reactivación lógica sin describir el impacto de usuarios autenticables vinculados.)

#### Escenario: Desactivar persona

- **DADO** que existe una Persona activa
- **CUANDO** se solicita su desactivación
- **ENTONCES** el sistema DEBE marcarla como inactiva sin eliminación física.

#### Escenario: Reactivar persona sin conflicto

- **DADO** que existe una Persona inactiva sin conflictos activos de `Legajo`, `Email` ni documento
- **CUANDO** se solicita su reactivación
- **ENTONCES** el sistema DEBE restaurar su estado activo.

#### Escenario: Preservar vínculo de usuario al desactivar Persona

- **DADO** que una Persona tiene un usuario autenticable asociado
- **CUANDO** la Persona se desactiva o reactiva
- **ENTONCES** el sistema MUST preservar la asociación Persona-usuario
- **Y** MUST NOT convertir el usuario en una cuenta standalone.

### Requisito: Listado segmentado y paginado de Personas (endpoint `/consulta`)

`GET /api/v1/personas/consulta?p=&pageSize=&search=&sort=&status=activas|eliminadas&soloSinUsuario=true|false` MUST estar disponible para cualquier autenticado. Búsqueda aplica a `Legajo|Nombres|Apellidos|Email|NumeroDocumento`. `status` omitido o desconocido MUST caer a `activas`. `soloSinUsuario` ausente, `false` o `null` MUST preservar el comportamiento vigente (sin filtro adicional). Cuando `soloSinUsuario=true`, el endpoint MUST retornar sólo personas activas sin usuario activo asociado (anti-join sobre `AspNetUsers.PersonaId`); la combinación con `Segmento=Eliminadas` MUST devolver `items=[]` y `totalCount=0`. Respuesta MUST ser `PagedResult<PersonaDto>`.

#### Escenario: Listar personas con paginación, búsqueda y orden server-side

- **DADO** personas activas persistidas
- **CUANDO** se solicita `/consulta?search=garcia&sort=apellidos_asc&p=1`
- **ENTONCES** responde `200` con `PagedResult<PersonaDto>` paginado, excluye inactivas y aplica filtro + orden antes de `Skip/Take`.

#### Escenario: Filtrar soloSinUsuario=true devuelve solo activas sin usuario

- **DADO** personas activas con y sin usuario activo asociado
- **CUANDO** se solicita `/consulta?soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener únicamente las personas activas sin usuario
- **Y** `totalCount` MUST coincidir con el total filtrado.

#### Escenario: soloSinUsuario es ortogonal al search y paginación vigentes

- **DADO** 30 personas activas con y sin usuario, conteniendo `garcía` en algunos nombres
- **CUANDO** se solicita `/consulta?search=garcia&soloSinUsuario=true&p=1&pageSize=5`
- **ENTONCES** MUST responder con `PagedResult<PersonaDto>` cuya `page=1`, `pageSize=5`, sin paginar personas con usuario
- **Y** el orden server-side por defecto MUST mantenerse intacto.

### Requirement: REQ-PM-01 Ortogonalidad entre `soloSinUsuario` y `Segmento`

El parámetro `soloSinUsuario=true` MUST ser ortogonal al `Segmento` sólo en el sentido de que filtra personas activas sin usuario. Combinado con `Segmento=Eliminadas`, el endpoint MUST responder `200` con `items=[]` y `totalCount=0` sin invocar joins. Combinado con `Segmento=Activas` (default), el endpoint MUST aplicar el filtro anti-join. El parámetro ausente, `false` o `null` MUST restaurar el comportamiento previo (sin filtro adicional).

#### Scenario: soloSinUsuario=true con Activas aplica filtro anti-join

- **DADO** personas activas con y sin usuario activo asociado
- **CUANDO** se solicita `/consulta?status=activas&soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** el `items` MUST contener únicamente las personas activas cuyo `AspNetUsers.PersonaId IS NULL`
- **Y** `totalCount` MUST reflejar el conteo post-filtro.

#### Scenario: soloSinUsuario=true con Eliminadas responde vacío

- **DADO** personas eliminadas en el segmento `Eliminadas`
- **CUANDO** se solicita `/consulta?status=eliminadas&soloSinUsuario=true&p=1&pageSize=25`
- **ENTONCES** MUST responder `200` con `items=[]` y `totalCount=0`
- **Y** MUST NOT aplicar el anti-join (cortocircuito por segmento).

#### Scenario: soloSinUsuario ausente preserva back-compat

- **DADO** personas activas y con usuario activo
- **CUANDO** se solicita `/consulta` sin el parámetro `soloSinUsuario`
- **ENTONCES** MUST devolver todas las activas sin aplicar el filtro anti-join
- **Y** MUST preservar el contrato previo de `PagedResult<PersonaDto>`.

#### Scenario: soloSinUsuario combinado con search sort y paginación

- **DADO** personas activas con y sin usuario
- **CUANDO** se solicita `/consulta?search=garcia&sort=apellidos_asc&p=2&pageSize=25&soloSinUsuario=true`
- **ENTONCES** el filtro `soloSinUsuario`, el `search`, el `sort` y la paginación 1-based MUST componerse en ese orden antes del `Skip/Take`
- **Y** `totalCount` MUST ser el conteo post-filtro `search` + `soloSinUsuario`.

### Requisito: Wire-types de Personas en SGV.Contracts

`SGV.Contracts.Personas` MUST exponer `PersonaDto`, `PersonaListQuery`, `PersonaSegmentoListado`, `CrearPersonaRequest`, `ActualizarPersonaRequest`, `PersonaDeleteResult` con `ErrorCategoria` y `PersonaCommandResult` con `FieldErrors`. `SGV.Web` MUST consumir estos tipos sin depender de `SGV.Aplicacion.Personas`.

#### Escenario: SGV.Web enlaza solo contra Contracts

- **DADO** un build de `SGV.Web`
- **CUANDO** el proyecto compila
- **ENTONCES** enlaza solo `SGV.Contracts.Personas` y no importa `SGV.Aplicacion.Personas.*`.

### Requisito: Listado web segmentado de Personas

`/personas` MUST alternar `activas|eliminadas` preservando `search`/`sort` y reseteando `p=1`. Grilla paginada server-side vía `/consulta`. `Detalle` visible a cualquier autenticado; resto de acciones reservadas a `Administrador`. Vista `eliminadas` oculta todo salvo `Reactivar`. Escrituras aplican PRG con feedback.

#### Escenario: Toggle de segmento y gating de acciones

- **DADO** usuario en Activas con `search` y `sort` aplicados
- **CUANDO** usa toggle a Eliminadas
- **ENTONCES** envía `status=eliminadas`, preserva `search`/`sort`, resetea `p` a `1` y oculta Detalle/Editar/Crear/Eliminar por fila, mostrando solo `Reactivar`.

### Requisito: Creación de Persona desde frontend web

`/personas/crear` MUST exigir `Administrador` (redirect `/error/403` en GET; `Forbid()` en POST). Formulario MUST incluir `Legajo|Nombres|Apellidos|Email|TipoDocumento|NumeroDocumento|Telefono`. PRG al `Details` con feedback success tras `201`; `400` se asocia a `Input.*` y `409` a feedback claro preservando datos.

#### Escenario: Create y feedback de unicidad

- **DADO** datos válidos
- **CUANDO** el backend responde `201`
- **ENTONCES** redirige al detail del nuevo Persona con mensaje visible de éxito.
- **Y** si responde `409` por unicidad de `Legajo`/`Email`/`NumeroDocumento`, MUST mostrar mensaje del campo afectado sin perder el resto del formulario.

### Requisito: Edición de Persona desde frontend web

`/personas/editar/{id}` MUST exigir `Administrador`, prellenar vía `GET /api/v1/personas/{id}` y aplicar PRG re-redirigiendo al propio edit tras `200`. `400`/`409` siguen el contrato de create; persona inexistente MUST mostrar estado recuperable.

#### Escenario: Edit prellena y persiste

- **DADO** Persona activa existente
- **CUANDO** un Administrador abre edit y guarda
- **ENTONCES** muestra todos los campos con sus valores actuales y, tras `200`, re-redirige al propio edit con feedback success.
- **Y** si el id no es consultable como activo, MUST mostrar estado recuperable con retorno al listado.

### Requisito: Detalle de Persona en frontend web

`/personas/detalle/{id}` MUST ser accesible para cualquier autenticado, mostrar `PersonaDto` readonly y ofrecer retorno al listado preservando `p`/`search`/`sort`/`status`. Persona no consultable MUST mostrar estado recuperable.

#### Escenario: Detalle existente muestra datos readonly

- **DADO** Persona activa existente
- **CUANDO** el usuario abre su detalle
- **ENTONCES** muestra datos en modo solo lectura con retorno al listado preservando filtros.

### Requisito: Desactivación y reactivación desde frontend web

`Index?handler=Delete` MUST ejecutar `DELETE /api/v1/personas/{id}` con PRG; si provenía de Activas, persiste el id en `TempData` para CTA rápido (oculto en Eliminadas). `Index?handler=Reactivate` MUST invocar `PATCH /api/v1/personas/{id}/reactivar`; éxito redirige a Activas; fallo permanece en Eliminadas con feedback accionable.

#### Escenario: Reactivación exitosa y fallida

- **DADO** persona eliminada visible en Eliminadas
- **CUANDO** usuario confirma `?handler=Reactivate`
- **ENTONCES** con éxito MUST redirigir a Activas, mostrar confirmación y limpiar el CTA rápido.
- **Y** con conflicto de unicidad MUST permanecer en Eliminadas con banner claro y accionable.

### Requisito: Typeahead reutilizable de Personas

`Pages/Personas/Shared/_PersonaTypeahead.cshtml` MUST consumir `GET /api/v1/personas` filtrando client-side por término, exponer hook de selección y NOT imponer dependencias privativas del módulo Usuarios.

#### Escenario: Typeahead muestra coincidencias al tipear

- **DADO** partial embebido en página autenticada
- **CUANDO** el usuario tipea ≥2 caracteres
- **ENTONCES** muestra personas activas cuyo `Legajo|Apellidos|Nombres` contenga el término y permite seleccionar una fila.

### Requisito: Exclusiones del Primer Corte

El sistema MUST NOT crear, modificar, consultar ni exponer comportamiento de Postulantes, Ocupaciones, Habilidades o `PersonaHabilidad` desde el módulo administrativo de Personas.

#### Escenario: No exponer relaciones excluidas

- **DADO** que una Persona tiene relaciones persistidas fuera de este corte
- **CUANDO** se usa cualquier operación administrativa de Personas
- **ENTONCES** la operación NO DEBE incluir ni modificar esas relaciones.

### Requisito: Autorización de endpoints de personas

`PersonasController` MUST requerir autenticación. `GET /api/v1/personas`, `GET /api/v1/personas/{id}` y `GET /api/v1/personas/consulta` MUST permitir acceso a cualquier usuario autenticado con respuesta `2xx`. `POST`, `PUT`, `PATCH` y `DELETE` (incluyendo `Reactivar`, `AsignarSkill` y `QuitarSkill`) MUST requerir rol `Administrador`.
(Previously: la lista de GET cubiertos no incluía `/consulta`.)

#### Escenario: Mutaciones requieren Administrador

- **DADO** usuario sin rol `Administrador`
- **CUANDO** solicita POST/PUT/PATCH/DELETE (incluyendo `Reactivar`, `AsignarSkill`, `QuitarSkill`)
- **ENTONCES** la API responde `403 Forbidden`.

#### Escenario: Acceso anónimo rechazado

- **DADO** cliente sin credenciales
- **CUANDO** solicita cualquier endpoint de PersonasController
- **ENTONCES** la API responde `401 Unauthorized`.

### Requisito: Validación de `NumeroDocumento` por `TipoDocumentoId` en creación

`CrearPersonaRequestValidator` DEBE rechazar el request cuando el `NumeroDocumento` provisto no satisface el `PatronValidacion` del `TipoDocumentoId` referenciado, o cuando su longitud cae fuera del rango `[LongitudMinima, LongitudMaxima]` del mismo `TipoDocumento`. Si el `TipoDocumentoId` referenciado no existe en el catálogo `TiposDocumento`, el request DEBE rechazarse como error de validación de campo (no `500`).

#### Escenario: Rechazar `NumeroDocumento` que no matchea el patrón del tipo

- **DADO** un `CrearPersonaRequest` con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12A45678"`
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `NumeroDocumento` con código `PATRON_NO_CUMPLIDO`
- **Y** el handler NO DEBE invocar la capa de persistencia.

#### Escenario: Rechazar `NumeroDocumento` fuera del rango de longitud del tipo

- **DADO** un `CrearPersonaRequest` con `TipoDocumentoId=<Id de DNI>` (rango 7-8) y `NumeroDocumento="12345"` (5 dígitos)
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `NumeroDocumento` con código `LONGITUD_FUERA_DE_RANGO`
- **Y** el handler NO DEBE invocar la capa de persistencia.

#### Escenario: Rechazar `TipoDocumentoId` inexistente en el catálogo

- **DADO** un `CrearPersonaRequest` con `TipoDocumentoId=<Guid no presente en TiposDocumento>` y `NumeroDocumento="12345678"`
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `TipoDocumentoId` con código `FK_INEXISTENTE`
- **Y** la API DEBE responder `400 Bad Request` con `FieldErrors` (no `500`).

#### Escenario: Aceptar `NumeroDocumento` válido contra el tipo seleccionado

- **DADO** un `CrearPersonaRequest` con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12345678"`
- **CUANDO** el validator corre y el resto de campos es válido
- **ENTONCES** NO emite errores de validación sobre `NumeroDocumento` ni `TipoDocumentoId`
- **Y** el flujo continúa hacia la creación.

### Requisito: Validación equivalente en actualización

`ActualizarPersonaRequestValidator` DEBE aplicar las mismas reglas de patrón, rango y FK existente que `CrearPersonaRequestValidator` para `TipoDocumentoId` y `NumeroDocumento`.

#### Escenario: Rechazar `NumeroDocumento` inválido en update

- **DADO** un `ActualizarPersonaRequest` con `TipoDocumentoId=<Id de Pasaporte>` y `NumeroDocumento="12345"` (no cumple `^[A-Za-z]{3}\d{6}$`)
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `NumeroDocumento` con código `PATRON_NO_CUMPLIDO`
- **Y** el handler NO DEBE invocar la capa de persistencia.

### Requisito: Auditoría del cambio de `TipoDocumentoId` en Persona

El interceptor centralizado de auditoría DEBE registrar en la tabla `Auditorias` un evento con `Entidad="Persona"`, `Operacion="Update"`, `IdEntidad=<Persona.Id>`, `Usuario`, `FechaHora` y los valores anterior/nuevo de `TipoDocumentoId` cada vez que `Persona.TipoDocumentoId` cambia, incluyendo la transición `NULL → valor_del_catalogo`.

#### Escenario: Cambio de un tipo a otro queda auditado

- **DADO** una Persona persistida con `TipoDocumentoId=<Id de DNI>`
- **CUANDO** el handler actualiza su `TipoDocumentoId` a `<Id de Pasaporte>` y persiste
- **ENTONCES** la tabla `Auditorias` contiene una nueva fila con `Entidad="Persona"`, `Operacion="Update"`, `IdEntidad=<Persona.Id>`
- **Y** `ValoresAnteriores` contiene el `Id` de DNI
- **Y** `ValoresNuevos` contiene el `Id` de Pasaporte.

#### Escenario: Transición NULL → valor del catálogo queda auditada

- **DADO** una Persona persistida con `TipoDocumentoId=NULL` (huérfana post-backfill)
- **CUANDO** el handler actualiza su `TipoDocumentoId` a `<Id de DNI>` y persiste
- **ENTONCES** la tabla `Auditorias` contiene una nueva fila con `Entidad="Persona"`, `Operacion="Update"`, `IdEntidad=<Persona.Id>`
- **Y** `ValoresAnteriores` registra `NULL`
- **Y** `ValoresNuevos` contiene el `Id` de DNI.

### Requisito: Cliente tipado de Web expone `GetTiposDocumentoAsync`

`IPersonaApiClient` DEBE exponer `Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken)`. La implementación fake (`FakePersonaApiClient`) DEBE registrar las invocaciones sin emitir HTTP real, para que los tests del shell Web no requieran `WebApplicationFactory` ni red.

#### Escenario: Fake registra la invocación de `GetTiposDocumentoAsync`

- **DADO** un `FakePersonaApiClient` configurado con una lista seed de `TipoDocumentoDto`
- **CUANDO** el PageModel invoca `GetTiposDocumentoAsync()`
- **ENTONCES** `GetTiposDocumentoCalls.Count == 1`
- **Y** el valor retornado es la lista seed
- **Y** el test NO emite ninguna request HTTP.

### Requisito: Formulario Create carga `TiposDocumento` para el `<select>`

`/personas/crear` (GET) DEBE poblar `PersonaInputModel.TiposDocumento` invocando `IPersonaApiClient.GetTiposDocumentoAsync()` una sola vez por request. La vista DEBE renderizar un `<select name="TipoDocumentoId">` con 4 opciones (`DNI`, `LE`, `LC`, `Pasaporte`) y un placeholder inicial sin selección.

#### Escenario: GET a Create carga el catálogo y renderiza 4 opciones

- **DADO** un usuario autenticado con rol `Administrador` que abre `/personas/crear`
- **CUANDO** el handler GET ejecuta
- **ENTONCES** `GetTiposDocumentoCalls.Count == 1` en `FakePersonaApiClient`
- **Y** el HTML resultante contiene un `<select name="TipoDocumentoId">` con 4 `<option>`
- **Y** las etiquetas visibles son `DNI`, `LE`, `LC` y `Pasaporte`.

### Requisito: Formulario Edit pre-selecciona el `TipoDocumento` actual

`/personas/editar/{id}` (GET) DEBE poblar `PersonaInputModel.TiposDocumento` invocando `IPersonaApiClient.GetTiposDocumentoAsync()` y DEBE pre-seleccionar en el `<select>` el `TipoDocumentoId` correspondiente a la persona persistida.

#### Escenario: Edit pre-selecciona el tipo actual de la persona

- **DADO** una Persona activa con `TipoDocumentoId=<Id de Pasaporte>`
- **CUANDO** un `Administrador` abre `/personas/editar/{id}`
- **ENTONCES** `GetTiposDocumentoCalls.Count == 1` en `FakePersonaApiClient`
- **Y** el HTML del `<select name="TipoDocumentoId">` contiene la opción de Pasaporte con el atributo `selected`.

### Requisito: Feedback de validación server-side en Create/Edit

Al submitir Create o Edit con un `NumeroDocumento` que no matchea el patrón del `TipoDocumentoId` seleccionado, el handler DEBE responder `400 Bad Request` con `FieldErrors` para `NumeroDocumento`. La página DEBE re-renderizar preservando los datos del formulario y mostrando un mensaje de error en español para `Input.NumeroDocumento`.

#### Escenario: Error de patrón visible y formulario preservado

- **DADO** un POST a `/personas/crear` con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12A45678"`
- **CUANDO** el handler responde `400` con `FieldErrors["NumeroDocumento"]`
- **ENTONCES** el HTML resultante preserva los valores de `Nombres|Apellidos|Email|Telefono|Legajo`
- **Y** muestra un mensaje en español asociado a `Input.NumeroDocumento`
- **Y** la opción `TipoDocumentoId` permanece seleccionada con `DNI`.

### Requisito: Navegación a la página de habilidades de la persona

`/personas/detalle/{id}` MUST exponer una acción visible que permita al `Administrador` acceder a `/personas/{id:guid}/habilidades` para gestionar el subrecurso `Persona↔Habilidad`. La acción MUST renderizarse solo cuando la persona sea consultable como activa, en línea con el resto de las acciones del detalle.

#### Escenario: Detalle activo expone acción hacia habilidades

- **DADO** un `Administrador` abriendo el detalle de una persona activa
- **CUANDO** la página se renderiza
- **ENTONCES** MUST existir un enlace o botón visible hacia `/personas/{id:guid}/habilidades`
- **Y** MUST estar etiquetado de forma que su propósito sea inequívoco.

#### Escenario: Detalle no consultable no expone la acción

- **DADO** que la persona no es consultable como activa (`IsNotFound == true` o estado recuperable equivalente)
- **CUANDO** la página de detalle se renderiza
- **ENTONCES** la acción hacia habilidades MUST NOT renderizarse.

#### Escenario: Persona con navegación no habilitada

- **DADO** un usuario autenticado sin rol `Administrador` en el detalle de una persona activa
- **CUANDO** la página se renderiza
- **ENTONCES** la acción hacia habilidades MUST NOT renderizarse
- **Y** el acceso al subrecurso MUST seguir bloqueado por la frontera de autorización vigente.

### Requirement: REQ-PM-NEW — Botón Habilidades por Persona activa

Cada fila activa de `Pages/Personas/Index.cshtml` MUST exponer un botón `Habilidades`, con icono `ti ti-stars` y clases `btn-primary btn-icon btn-sm rounded-circle`, que navegue a `Pages/Personas/PersonaHabilidades` con el id de la persona.

#### Scenario: Administrador navega desde una fila activa

- **DADO** un Administrador en el listado de Personas y una fila activa
- **CUANDO** selecciona `Habilidades`
- **ENTONCES** el enlace MUST incluir el id correcto y apuntar a `PersonaHabilidades`.

### Requirement: REQ-PM-NEW-ADMIN — Gating por rol y segmento

El botón MUST renderizarse solo si `Model.EsAdministrador` y la vista no es `IsDeletedView`.

#### Scenario: Gating de visibilidad

- **DADO** una fila activa o eliminada y un usuario administrador o no administrador
- **CUANDO** se renderiza el listado
- **ENTONCES** solo la combinación activa + administrador MUST mostrar el botón.

### Requirement: REQ-PM-NEW-POSITION — Orden de acciones

El botón `Habilidades` MUST ubicarse en la columna `Acciones`, entre `Detalle` y `Editar`.

#### Scenario: Orden visual del listado

- **DADO** una fila activa visible para un administrador
- **CUANDO** se renderiza la columna `Acciones`
- **ENTONCES** `Habilidades` MUST aparecer después de `Detalle` y antes de `Editar`.

### Requirement: REQ-PM-NEW-CONTEXT — Preservación del contexto de listado

`BuildHabilidadesRouteValues` MUST conservar `page`, `search`, `sort` y `status` del listado actual al construir la ruta hacia `PersonaHabilidades`.

#### Scenario: Regreso sin perder filtros

- **DADO** un listado con `page`, `search`, `sort` y `status` definidos
- **CUANDO** se construye el enlace `Habilidades`
- **ENTONCES** la ruta MUST transportar los cuatro valores para permitir volver al contexto original.
