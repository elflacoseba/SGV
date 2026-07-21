# Delta para Gestión de Personas

> **Status:** MODIFIED — capability exists at `openspec/specs/persona-management/spec.md`. This delta modifies the Alta/Actualización requisitos to introduce catalog-driven validation against `TipoDocumentoId`, adds audit/coverage escenarios for `TipoDocumentoId` change, and adds Web shell requirements for the `TipoDocumento` `<select>` and API client.
> **Change:** `2026-07-20-147-tipos-documento-catalogo` (issue #147)

## ADDED Requirements

### Requirement: Validación de `NumeroDocumento` por `TipoDocumentoId` en creación

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

### Requirement: Validación equivalente en actualización

`ActualizarPersonaRequestValidator` DEBE aplicar las mismas reglas de patrón, rango y FK existente que `CrearPersonaRequestValidator` para `TipoDocumentoId` y `NumeroDocumento`.

#### Escenario: Rechazar `NumeroDocumento` inválido en update

- **DADO** un `ActualizarPersonaRequest` con `TipoDocumentoId=<Id de Pasaporte>` y `NumeroDocumento="12345"` (no cumple `^[A-Za-z]{3}\d{6}$`)
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `NumeroDocumento` con código `PATRON_NO_CUMPLIDO`
- **Y** el handler NO DEBE invocar la capa de persistencia.

### Requirement: Auditoría del cambio de `TipoDocumentoId` en Persona

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

### Requirement: Cliente tipado de Web expone `GetTiposDocumentoAsync`

`IPersonaApiClient` DEBE exponer `Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken)`. La implementación fake (`FakePersonaApiClient`) DEBE registrar las invocaciones sin emitir HTTP real, para que los tests del shell Web no requieran `WebApplicationFactory` ni red.

#### Escenario: Fake registra la invocación de `GetTiposDocumentoAsync`

- **DADO** un `FakePersonaApiClient` configurado con una lista seed de `TipoDocumentoDto`
- **CUANDO** el PageModel invoca `GetTiposDocumentoAsync()`
- **ENTONCES** `GetTiposDocumentoCalls.Count == 1`
- **Y** el valor retornado es la lista seed
- **Y** el test NO emite ninguna request HTTP.

### Requirement: Formulario Create carga `TiposDocumento` para el `<select>`

`/personas/crear` (GET) DEBE poblar `PersonaInputModel.TiposDocumento` invocando `IPersonaApiClient.GetTiposDocumentoAsync()` una sola vez por request. La vista DEBE renderizar un `<select name="TipoDocumentoId">` con 4 opciones (`DNI`, `LE`, `LC`, `Pasaporte`) y un placeholder inicial sin selección.

#### Escenario: GET a Create carga el catálogo y renderiza 4 opciones

- **DADO** un usuario autenticado con rol `Administrador` que abre `/personas/crear`
- **CUANDO** el handler GET ejecuta
- **ENTONCES** `GetTiposDocumentoCalls.Count == 1` en `FakePersonaApiClient`
- **Y** el HTML resultante contiene un `<select name="TipoDocumentoId">` con 4 `<option>`
- **Y** las etiquetas visibles son `DNI`, `LE`, `LC` y `Pasaporte`.

### Requirement: Formulario Edit pre-selecciona el `TipoDocumento` actual

`/personas/editar/{id}` (GET) DEBE poblar `PersonaInputModel.TiposDocumento` invocando `IPersonaApiClient.GetTiposDocumentoAsync()` y DEBE pre-seleccionar en el `<select>` el `TipoDocumentoId` correspondiente a la persona persistida.

#### Escenario: Edit pre-selecciona el tipo actual de la persona

- **DADO** una Persona activa con `TipoDocumentoId=<Id de Pasaporte>`
- **CUANDO** un `Administrador` abre `/personas/editar/{id}`
- **ENTONCES** `GetTiposDocumentoCalls.Count == 1` en `FakePersonaApiClient`
- **Y** el HTML del `<select name="TipoDocumentoId">` contiene la opción de Pasaporte con el atributo `selected`.

### Requirement: Feedback de validación server-side en Create/Edit

Al submitir Create o Edit con un `NumeroDocumento` que no matchea el patrón del `TipoDocumentoId` seleccionado, el handler DEBE responder `400 Bad Request` con `FieldErrors` para `NumeroDocumento`. La página DEBE re-renderizar preservando los datos del formulario y mostrando un mensaje de error en español para `Input.NumeroDocumento`.

#### Escenario: Error de patrón visible y formulario preservado

- **DADO** un POST a `/personas/crear` con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12A45678"`
- **CUANDO** el handler responde `400` con `FieldErrors["NumeroDocumento"]`
- **ENTONCES** el HTML resultante preserva los valores de `Nombres|Apellidos|Email|Telefono|Legajo`
- **Y** muestra un mensaje en español asociado a `Input.NumeroDocumento`
- **Y** la opción `TipoDocumentoId` permanece seleccionada con `DNI`.

## MODIFIED Requirements

### Requirement: Alta de Persona

El sistema MUST permitir crear Personas con datos válidos. `Legajo` MUST ser requerido y único entre Personas activas. `Email` y documento MAY omitirse, pero si se informan MUST respetar formato/longitud y unicidad activa. Cuando se informa documento, `TipoDocumentoId` MUST referenciar un `TipoDocumento` seedeado, `NumeroDocumento` MUST satisfacer el `PatronValidacion` y rango del tipo referenciado, y el cambio queda registrado en `Auditorias`.
(Previously: el requisito trataba `TipoDocumento` como texto libre sin catálogo ni validación por patrón; el cambio introduce FK opcional hacia `TiposDocumento` con validación server-side.)

#### Escenario: Crear persona válida con tipo de documento

- **DADO** que no existe una Persona activa con el mismo `Legajo`, `Email` ni documento informado
- **Y** existe `TipoDocumento` con `Codigo="DNI"`
- **CUANDO** se crea una Persona con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12345678"`
- **ENTONCES** el sistema DEBE persistirla como activa
- **Y** DEBE devolver su identificador y datos administrables.

#### Escenario: Rechazar datos obligatorios faltantes

- **DADO** una solicitud de creación de Persona
- **CUANDO** falta un dato obligatorio como `Legajo` o nombre requerido por el contrato
- **ENTONCES** el sistema DEBE rechazar la solicitud sin persistir cambios.

#### Escenario: Rechazar documento que no satisface patrón del tipo

- **DADO** un `CrearPersonaRequest` con `TipoDocumentoId=<Id de Pasaporte>` y `NumeroDocumento="12345"`
- **CUANDO** el validator corre
- **ENTONCES** DEBE emitir un error de validación en `NumeroDocumento`
- **Y** la API DEBE responder `400 Bad Request` con `FieldErrors`.

### Requirement: Actualización de Persona

El sistema MUST permitir actualizar datos propios de Persona sin modificar relaciones fuera de alcance. La actualización MUST preservar la unicidad activa de `Legajo`, `Email` y documento. Cuando se modifica `TipoDocumentoId` o `NumeroDocumento`, la nueva combinación MUST satisfacer el patrón y rango del tipo seleccionado y el cambio MUST quedar registrado en `Auditorias`.
(Previously: el requisito trataba `TipoDocumento` como texto libre; el cambio incorpora FK opcional y validación por patrón.)

#### Escenario: Actualizar contacto preservando documento válido

- **DADO** que existe una Persona activa
- **CUANDO** se actualizan datos de contacto válidos y el documento cumple el patrón del tipo seleccionado
- **ENTONCES** el sistema DEBE persistir los cambios
- **Y** NO DEBE alterar relaciones excluidas.

#### Escenario: Rechazar cambio de documento que rompe patrón

- **DADO** una Persona activa con `TipoDocumentoId=<Id de DNI>` y `NumeroDocumento="12345678"`
- **CUANDO** se envía un update con `NumeroDocumento="12A45678"`
- **ENTONCES** el sistema DEBE responder `400 Bad Request` con `FieldErrors["NumeroDocumento"]`.

#### Escenario: Rechazar duplicados activos

- **DADO** que existe otra Persona activa con el mismo `Legajo`, `Email` o documento
- **CUANDO** se intenta crear, actualizar o reactivar una Persona con esos valores
- **ENTONCES** el sistema DEBE rechazar la operación con un conflicto claro.