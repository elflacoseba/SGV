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

El sistema MUST permitir crear Personas con datos válidos. `Legajo` MUST ser requerido y único entre Personas activas. `Email` y documento MAY omitirse, pero si se informan MUST respetar formato/longitud y unicidad activa.

#### Escenario: Crear persona válida

- **DADO** que no existe una Persona activa con el mismo `Legajo`, `Email` ni documento informado
- **CUANDO** se crea una Persona con datos válidos
- **ENTONCES** el sistema DEBE persistirla como activa
- **Y** DEBE devolver su identificador y datos administrables.

#### Escenario: Rechazar datos obligatorios faltantes

- **DADO** una solicitud de creación de Persona
- **CUANDO** falta un dato obligatorio como `Legajo` o nombre requerido por el contrato
- **ENTONCES** el sistema DEBE rechazar la solicitud sin persistir cambios.

### Requisito: Actualización de Persona

El sistema MUST permitir actualizar datos propios de Persona sin modificar relaciones fuera de alcance. La actualización MUST preservar la unicidad activa de `Legajo`, `Email` y documento.

#### Escenario: Actualizar contacto

- **DADO** que existe una Persona activa
- **CUANDO** se actualizan datos de contacto válidos
- **ENTONCES** el sistema DEBE persistir los cambios
- **Y** NO DEBE alterar relaciones excluidas.

#### Escenario: Rechazar duplicados activos

- **DADO** que existe otra Persona activa con el mismo `Legajo`, `Email` o documento
- **CUANDO** se intenta crear, actualizar o reactivar una Persona con esos valores
- **ENTONCES** el sistema DEBE rechazar la operación con un conflicto claro.

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
