# Delta para persona-management

> Delta introducida por el change `permitir-legajo-nulo-personas-issue-202` (issue #202). Verifica `openspec/changes/permitir-legajo-nulo-personas-issue-202/proposal.md` y `exploration.md` para alcance y justificación.

## MODIFIED Requirements

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

## ADDED Requirements

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
