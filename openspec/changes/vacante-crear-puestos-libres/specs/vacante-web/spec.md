# Delta para `vacante-web`

## MODIFIED Requirements

### Requisito: Formulario de Create con catálogo de estados

El sistema DEBE mostrar en Create los campos `PuestoId`, `EstadoVacanteId`, `FechaApertura`, `Motivo`, `Observaciones`. Los dropdowns de Puesto y Estado DEBEN poblarse desde la API antes de habilitar el guardado. El dropdown de Puesto DEBE consumir `GET /api/v1/puestos/disponibles` (puestos sin Ocupación vigente ni Vacante abierta), no `GET /api/v1/puestos`, de modo que el formulario ofrezca exclusivamente Puestos efectivamente disponibles — coherente con la regla N1 y el constraint `ActivePuestoIdUnique` del backend. La creación se inicia desde el módulo de Vacantes (PB-2), NO desde el detalle de Puesto.

(Previously: el dropdown de Puesto se poblaba desde `GET /api/v1/puestos`, listado que devuelve todos los Puestos activos sin filtrar por disponibilidad, lo que derivaba en `409 Conflict` post-factum al hacer POST del formulario.)

#### Escenario: Catálogos cargados en Create

- **DADO** que `GET /api/v1/estados-vacante` y `GET /api/v1/puestos/disponibles` responden
- **CUANDO** el usuario abre Create
- **ENTONCES** la interfaz DEBE mostrar opciones seleccionables de Puesto y Estado.

#### Escenario: El dropdown de Puesto consume el endpoint de disponibles

- **DADO** el handler `OnGetAsync` de `Vacantes/Create`
- **CUANDO** un usuario autorizado abre la página
- **ENTONCES** el PageModel DEBE invocar `vacanteApiClient.ListarPuestosDisponiblesAsync` exactamente una vez
- **Y** NO DEBE invocar `vacanteApiClient.ListarPuestosAsync` para poblar el dropdown de Puesto.

#### Escenario: El dropdown no incluye puestos con Ocupación vigente

- **DADO** el backend devuelve puestos disponibles filtrados (sin Ocupación vigente)
- **CUANDO** se renderiza el `<select name="Input.PuestoId">`
- **ENTONCES** ningún `<option>` debe corresponder a un Puesto que tenga `Ocupacion` con `IsDeleted=0` AND `FechaFin IS NULL`.

#### Escenario: El dropdown no incluye puestos con Vacante Abierta

- **DADO** el backend devuelve puestos disponibles filtrados (sin Vacante abierta)
- **CUANDO** se renderiza el `<select name="Input.PuestoId">`
- **ENTONCES** ningún `<option>` debe corresponder a un Puesto que tenga `Vacante` con `IsDeleted=0` AND `FechaCierre IS NULL`.

#### Escenario: Falla la carga de catálogos

- **DADO** que el endpoint `GET /api/v1/puestos/disponibles` (u otro catálogo) falla al cargar
- **CUANDO** el usuario abre Create
- **ENTONCES** la interfaz DEBE mostrar un estado recuperable y bloquear el guardado hasta reintentar
- **Y** el comportamiento de recuperación de transporte es idéntico al vigente (compatible con `vacante-web/spec.md` §"Falla la carga de catálogos").

#### Escenario: Mutación web rechazada por rol

- **DADO** un usuario autenticado sin rol `Administrador` ni `GestorVacantes` solicita Create
- **CUANDO** se procesa el handler
- **ENTONCES** este DEBE responder `Forbid()`
- **Y** NO DEBE invocar `GET /api/v1/puestos/disponibles` ni `GET /api/v1/estados-vacante`.

#### Source

- `openspec/changes/vacante-crear-puestos-libres/proposal.md:5,49-51,89-95`
- `openspec/specs/vacante-web/spec.md:62-76` (requisito original modificado)

#### Verification

- Page smoke: `VacantesCreateEditForbidTests.Get_Create_WhenMutationRole_RendersFormWithCatalogs` se actualiza para verificar 1 invocación a `ListarPuestosDisponiblesAsync`.
- Fake sincronizado: `FakeVacanteApiClient` expone `ListarPuestosDisponiblesResult` para que el PageModel compile.
- Forbid: `Create_Forbid_*` sigue verde; el handler retorna `Forbid()` antes de tocar el ApiClient.