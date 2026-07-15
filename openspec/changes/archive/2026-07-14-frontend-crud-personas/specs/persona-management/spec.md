# Delta para persona-management

## ADDED Requirements

### Requirement: Listado segmentado y paginado de Personas (endpoint `/consulta`)

`GET /api/v1/personas/consulta?p=&pageSize=&search=&sort=&status=activas|eliminadas` MUST estar disponible para cualquier autenticado. Búsqueda aplica a `Legajo|Nombres|Apellidos|Email|NumeroDocumento`. `status` omitido o desconocido MUST caer a `activas`. Respuesta MUST ser `PagedResult<PersonaDto>`.

#### Scenario: Listar personas con paginación, búsqueda y orden server-side

- **DADO** personas activas persistidas
- **CUANDO** se solicita `/consulta?search=garcia&sort=apellidos_asc&p=1`
- **ENTONCES** responde `200` con `PagedResult<PersonaDto>` paginado, excluye inactivas y aplica filtro + orden antes de `Skip/Take`.

### Requirement: Wire-types de Personas en SGV.Contracts

`SGV.Contracts.Personas` MUST exponer `PersonaDto`, `PersonaListQuery`, `PersonaSegmentoListado`, `CrearPersonaRequest`, `ActualizarPersonaRequest`, `PersonaDeleteResult` con `ErrorCategoria` y `PersonaCommandResult` con `FieldErrors`. `SGV.Web` MUST consumir estos tipos sin depender de `SGV.Aplicacion.Personas`.

#### Scenario: SGV.Web enlaza solo contra Contracts

- **DADO** un build de `SGV.Web`
- **CUANDO** el proyecto compila
- **ENTONCES** enlaza solo `SGV.Contracts.Personas` y no importa `SGV.Aplicacion.Personas.*`.

### Requirement: Listado web segmentado de Personas

`/personas` MUST alternar `activas|eliminadas` preservando `search`/`sort` y reseteando `p=1`. Grilla paginada server-side vía `/consulta`. `Detalle` visible a cualquier autenticado; resto de acciones reservadas a `Administrador`. Vista `eliminadas` oculta todo salvo `Reactivar`. Escrituras aplican PRG con feedback.

#### Scenario: Toggle de segmento y gating de acciones

- **DADO** usuario en Activas con `search` y `sort` aplicados
- **CUANDO** usa toggle a Eliminadas
- **ENTONCES** envía `status=eliminadas`, preserva `search`/`sort`, resetea `p` a `1` y oculta Detalle/Editar/Crear/Eliminar por fila, mostrando solo `Reactivar`.

### Requirement: Creación de Persona desde frontend web

`/personas/crear` MUST exigir `Administrador` (redirect `/error/403` en GET; `Forbid()` en POST). Formulario MUST incluir `Legajo|Nombres|Apellidos|Email|TipoDocumento|NumeroDocumento|Telefono`. PRG al `Details` con feedback success tras `201`; `400` se asocia a `Input.*` y `409` a feedback claro preservando datos.

#### Scenario: Create y feedback de unicidad

- **DADO** datos válidos
- **CUANDO** el backend responde `201`
- **ENTONCES** redirige al detail del nuevo Persona con mensaje visible de éxito.
- **Y** si responde `409` por unicidad de `Legajo`/`Email`/`NumeroDocumento`, MUST mostrar mensaje del campo afectado sin perder el resto del formulario.

### Requirement: Edición de Persona desde frontend web

`/personas/editar/{id}` MUST exigir `Administrador`, prellenar vía `GET /api/v1/personas/{id}` y aplicar PRG re-redirigiendo al propio edit tras `200`. `400`/`409` siguen el contrato de create; persona inexistente MUST mostrar estado recuperable.

#### Scenario: Edit prellena y persiste

- **DADO** Persona activa existente
- **CUANDO** un Administrador abre edit y guarda
- **ENTONCES** muestra todos los campos con sus valores actuales y, tras `200`, re-redirige al propio edit con feedback success.
- **Y** si el id no es consultable como activo, MUST mostrar estado recuperable con retorno al listado.

### Requirement: Detalle de Persona en frontend web

`/personas/detalle/{id}` MUST ser accesible para cualquier autenticado, mostrar `PersonaDto` readonly y ofrecer retorno al listado preservando `p`/`search`/`sort`/`status`. Persona no consultable MUST mostrar estado recuperable.

#### Scenario: Detalle existente muestra datos readonly

- **DADO** Persona activa existente
- **CUANDO** el usuario abre su detalle
- **ENTONCES** muestra datos en modo solo lectura con retorno al listado preservando filtros.

### Requirement: Desactivación y reactivación desde frontend web

`Index?handler=Delete` MUST ejecutar `DELETE /api/v1/personas/{id}` con PRG; si provenía de Activas, persiste el id en `TempData` para CTA rápido (oculto en Eliminadas). `Index?handler=Reactivate` MUST invocar `PATCH /api/v1/personas/{id}/reactivar`; éxito redirige a Activas; fallo permanece en Eliminadas con feedback accionable.

#### Scenario: Reactivación exitosa y fallida

- **DADO** persona eliminada visible en Eliminadas
- **CUANDO** usuario confirma `?handler=Reactivate`
- **ENTONCES** con éxito MUST redirigir a Activas, mostrar confirmación y limpiar el CTA rápido.
- **Y** con conflicto de unicidad MUST permanecer en Eliminadas con banner claro y accionable.

### Requirement: Typeahead reutilizable de Personas

`Pages/Personas/Shared/_PersonaTypeahead.cshtml` MUST consumir `GET /api/v1/personas` filtrando client-side por término, exponer hook de selección y NOT imponer dependencias privativas del módulo Usuarios.

#### Scenario: Typeahead muestra coincidencias al tipear

- **DADO** partial embebido en página autenticada
- **CUANDO** el usuario tipea ≥2 caracteres
- **ENTONCES** muestra personas activas cuyo `Legajo|Apellidos|Nombres` contenga el término y permite seleccionar una fila.

## MODIFIED Requirements

### Requirement: Autorización de endpoints de personas

`PersonasController` MUST requerir autenticación. `GET /api/v1/personas`, `GET /api/v1/personas/{id}` y el nuevo `GET /api/v1/personas/consulta` MUST permitir acceso a cualquier usuario autenticado con respuesta `2xx`. `POST`, `PUT`, `PATCH` y `DELETE` (incluyendo `Reactivar`, `AsignarSkill` y `QuitarSkill`) MUST requerir rol `Administrador`.
(Previously: la lista de GET cubiertos no incluía `/consulta`.)

#### Scenario: Mutaciones requieren Administrador

- **DADO** usuario sin rol `Administrador`
- **CUANDO** solicita POST/PUT/PATCH/DELETE (incluyendo `Reactivar`, `AsignarSkill`, `QuitarSkill`)
- **ENTONCES** la API responde `403 Forbidden`.

#### Scenario: Acceso anónimo rechazado

- **DADO** cliente sin credenciales
- **CUANDO** solicita cualquier endpoint de PersonasController
- **ENTONCES** la API responde `401 Unauthorized`.
