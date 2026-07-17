# Tasks: Buscador modal reutilizable de Personas en Crear/Editar Usuario

Referencia: REQ-USB-01..11, REQ-PM-01, REQ-UCE-02/08/09/10 (`specs/`), D-01..D-10 (`design.md`).

## Resumen

Se implementa un selector modal Bootstrap 5 paginado server-side que reemplaza el combo plano de `_Form.cshtml` y su backend `IPersonaOptionsProvider`. Se extiende `GET /api/v1/personas/consulta` con `soloSinUsuario=true` (anti-join contra `AspNetUsers`), se crea el partial `_PersonaBuscadorModal.cshtml` + JS, y se limpia `IPersonaOptionsProvider`/`HttpPersonaOptionsProvider`/`FakePersonaOptionsProvider`. 8 work units, ~600 líneas estimadas. Strict TDD: RED → GREEN por WU.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~600 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | WU-1..3 → WU-4 → WU-5..8 |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

---

## Work Units

### WU-1 — Filtro `soloSinUsuario` en repositorio (LEFT JOIN)

**Objetivo**: Extender `PersonaRepository.QueryAsync` con parámetro `bool? soloSinUsuario`. Activas + `true` → LEFT JOIN `AspNetUsers` exigiendo `PersonaId IS NULL`. Eliminadas + `true` → cortocircuito `items=[]`, `totalCount=0`. `null`/`false` → bit-identical.

**Archivos a tocar**:
- `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaListQuery.cs` (+ `bool? SoloSinUsuario`)
- `src/SGV.Aplicacion/Personas/Consultas/IPersonaRepository.cs` (+ param `QueryAsync`)
- `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaRepository.cs` (+ LEFT JOIN + cortocircuito)

**Tests (RED)** (4 `[MySqlFact]`):
- `QueryAsync_SoloSinUsuarioTrue_ExcluyePersonasConUsuario` — persona con usuario activo queda excluida
- `QueryAsync_SoloSinUsuarioTrueConEliminadas_RetornaVacio` — cortocircuito con `Segmento=Eliminadas`
- `QueryAsync_SoloSinUsuarioFalseONull_PreservaBackCompat` — mismo resultado que hoy
- `QueryAsync_SoloSinUsuarioCombinaConSearchSortPaginacion` — ortogonal con search/sort/page

**Implementación (GREEN)**: Agregar `bool? soloSinUsuario = null` al método. Si `true && Activas`, aplicar `query = query.Where(p => !Context.Set<IdentityUser>().Any(u => u.PersonaId == p.Id))`. Si `true && Eliminadas`, retornar `([], 0)`. Refactorizar `PersonaListQuery` a `SoloSinUsuario` opcional.

**Refactor**: Verificar que ningún consumidor existente (Index Personas, typeahead) pase `soloSinUsuario=true` — deben seguir funcionando bit-identical.

**Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaRepositoryTests"`

**Tamaño estimado**: ~80 líneas (30 producción + 50 tests)

---

### WU-2 — Propagación del flag en servicio

**Objetivo**: `PersonaServicioConsulta.ListarAsync` lee `SoloSinUsuario` del `PersonaListQuery` y lo pasa al repositorio. Sin lógica nueva — sólo enlace.

**Archivos a tocar**:
- `src/SGV.Aplicacion/Personas/Consultas/PersonaServicioConsulta.cs` (+ propagación)

**Tests (RED)** (4 `[Fact]` con `FakePersonaRepository`):
- `ListarAsync_SoloSinUsuarioTrue_PropagaARepositorio` — verifica que el repo recibe `true`
- `ListarAsync_SoloSinUsuarioNullONoSet_PropagaNull` — back-compat
- `ListarAsync_SoloSinUsuarioTrueConEliminadas_PropagaTrue` — ortogonal
- `ListarAsync_SoloSinUsuarioCombinaConSearchSort` — todo el query pasa íntegro

**Implementación (GREEN)**: Una línea: pasar `query.SoloSinUsuario` al `repository.QueryAsync`.

**Refactor**: Ninguno.

**Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaServicioConsultaTests"`

**Tamaño estimado**: ~55 líneas (15 producción + 40 tests)

---

### WU-3 — Parámetro en controller API

**Objetivo**: `PersonasController.GetConsulta` acepta `[FromQuery] bool? soloSinUsuario` y lo pasa al construir `PersonaListQuery`.

**Archivos a tocar**:
- `src/SGV.Api/Controllers/PersonasController.cs` (+ param + pase a `PersonaListQuery`)

**Tests (RED)** (4 `[ApiIntegration]`):
- `GetConsulta_ConSoloSinUsuarioTrue_FiltraPersonasSinUsuario` — endpoint + anti-join
- `GetConsulta_ConSoloSinUsuarioTrueYEliminadas_Retorna200ConItemsVacio` — cortocircuito
- `GetConsulta_SoloSinUsuarioAusenteOMalformed_PreservaBackCompat` — null/false/string no bool
- `GetConsulta_SoloSinUsuarioCombinaConSearchSort` — composición completa

**Implementación (GREEN)**: Agregar `[FromQuery] bool? soloSinUsuario = null` y pasarlo al `new PersonaListQuery(...)`.

**Refactor**: Ninguno.

**Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonasControllerTests"`

**Tamaño estimado**: ~55 líneas (10 producción + 45 tests)

---

### WU-4 — Cliente HTTP + Fake

**Objetivo**: `PersonaApiClient.BuildQueryUri` serializa `soloSinUsuario=true` sólo cuando aplica. `FakePersonaApiClient` extiende su `QueryAsync` para filtrar por `SoloSinUsuario`. `WithSoloSinUsuarioSet(IEnumerable<Guid>)` en el fake.

**Archivos a tocar**:
- `src/SGV.Web/Integration/Personas/PersonaApiClient.cs` (+ param en `BuildQueryUri` y `QueryAsync`)
- `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` (+ filtro `SoloSinUsuario` + helper `WithSoloSinUsuarioSet`)

**Tests (RED)** (3 `[Fact]` + 1 `[Fact]`):
- `BuildQueryUri_ConSoloSinUsuarioTrue_SerializaParam` — URI contiene `&soloSinUsuario=true`
- `BuildQueryUri_ConSoloSinUsuarioNullOFalse_OmiteParam` — back-compat URI
- `BuildQueryUri_ConTransportFailure_PropagaExcepcionNativa` — sin try-catch falso
- `FakePersonaApiClient_QueryAsync_ConSoloSinUsuarioTrue_Filtra` — `WithSoloSinUsuarioSet` funciona

**Implementación (GREEN)**: Agregar parámetro a `BuildQueryUri`. En `FakePersonaApiClient`, si `query.SoloSinUsuario == true`, excluir ids del set `_soloSinUsuarioSet` del resultado. El helper `WithSoloSinUsuarioSet` crea el set.

**Refactor**: Extraer la lógica de filtro `SoloSinUsuario` a método privado.

**Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~FakePersonaApiClient|PersonaApiClient"`

**Tamaño estimado**: ~70 líneas (20 producción + 50 tests)

---

### WU-5 — Página Create sin dropdown

**Objetivo**: `Create.cshtml(.cs)` deja de depender de `IPersonaOptionsProvider`. En GET, invoca `IPersonaApiClient.QueryAsync(page=1, pageSize=1, soloSinUsuario=true)` para REQ-UCE-09 (banner si `TotalCount==0`). El campo Persona expone el botón `Buscar Persona` (REQ-USB-01). `409` por carrera muestra feedback en campo (D-10).

**Archivos a tocar**:
- `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` (− `IPersonaOptionsProvider`, + `IPersonaApiClient`; reemplazar `LoadPersonasAsync` por `QueryAsync` para banner)
- `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml` (− alert de `PersonaOptions.Count==0`, + banner condicional por `TotalCount` de la query)

**Tests (RED)** (3 `[WebIntegration]`):
- `Get_Create_NoRenderizaSelectPoblado_RenderizaBotonBuscar` — sin `<select name="Input.PersonaId">` (REQ-USB-01)
- `Get_Create_ConTotalCountCero_MuestraBannerConCtaAPersonasCrear` — REQ-UCE-09
- `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` — D-10, REQ-UCE-10

**Implementación (GREEN)**: Inyectar `IPersonaApiClient`. En `OnGetAsync`, llamar `QueryAsync(page: 1, pageSize: 1, soloSinUsuario: true)` y setear `TotalCountSugerido` para la view. Reemplazar el `<div class="alert alert-warning">` por uno condicional en `TotalCount==0`. En POST, `409` → `ModelState.AddModelError("Input.PersonaId", "Esa persona ya tiene un usuario activo.")`.

**Refactor**: Remover `LoadPersonasAsync`, `PersonaOptions` y `IPersonaOptionsProvider` del constructor.

**Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~CreatePageTests"`

**Tamaño estimado**: ~80 líneas (40 producción + 40 tests)

---

### WU-6 — Página Edit con card preseleccionada

**Objetivo**: `Edit.cshtml(.cs)` reemplaza `IPersonaOptionsProvider` por la persona ya cargada desde `usuarioApiClient.GetByIdAsync`. Persona actual se muestra como card (REQ-USB-02). `Quitar` → estado vacío (REQ-UCE-08). `Cambiar` → modal excluye persona actual.

**Archivos a tocar**:
- `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` (− `IPersonaOptionsProvider`, −`LoadPersonasAsync`, −`PersonaOptions`; `PersonaId` desde `GetByIdAsync` ya está disponible)
- `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml` (sin cambios — el partial maneja el estado)

**Tests (RED)** (2 `[WebIntegration]`):
- `Get_Edit_ConPersonaVinculada_RenderizaCardPreseleccionada` — persona actual como card, hidden con id (REQ-USB-02)
- `Get_Edit_BotonQuitar_LimpiaSelector_VuelveAEstadoVacio` — sin invocar API (REQ-UCE-08, REQ-USB-08)

**Implementación (GREEN)**: En `EditModel.OnGetAsync`, los datos de la persona vinculada ya vienen en `usuario.PersonaId` y `usuario.PersonaDisplay`. Remover `personaOptionsProvider` del constructor, remover `PersonaOptions` y `LoadPersonasAsync`. La card se renderiza desde los datos del DTO.

**Refactor**: Simplificar `EditModel` constructor: queda `(IUsuarioApiClient, IAuthSessionRedirector, ILogger)`.

**Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~EditPageTests"`

**Tamaño estimado**: ~40 líneas (15 producción + 25 tests)

---

### WU-7 — Partial `_PersonaBuscadorModal.cshtml`

**Objetivo**: Crear el partial Bootstrap 5 con markup accesible (role="dialog", aria-modal, aria-labelledby). Cuatro estados: Inicial, Empty, Loading, Error. Tabla paginada 25 filas, columnas `Apellido y Nombre | Documento | Legajo | Email | Acción`. Paginación `Anterior` + numérica (1..N con elipsis si >7) + `Siguiente`. Contrato `ViewData`: `ModalId`, `HiddenInputName`, `HiddenInputId`, `DisplayContainerId`, `CurrentPersonaId` (Guid?), `CurrentPersonaDisplay` (string?). Integrado en `_Form.cshtml`.

**Archivos a tocar**:
- **Nuevo**: `src/SGV.Web/Pages/Seguridad/Usuarios/_PersonaBuscadorModal.cshtml`
- `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` (reemplazar bloque `@if (!Model.IsEdit)`/`else` por llamada al partial + card contenedor)

**Tests (RED)** (3 `[WebIntegration]`):
- `_PersonaBuscadorModal_TieneRoleDialogYAriaModal` — REQ-USB-09 (accesibilidad mínima)
- `_PersonaBuscadorModal_EstadoInicial_MuestraMensajeGuia` — "Ingresá un texto para buscar personas." (REQ-USB-05)
- `_PersonaBuscadorModal_EstadoEmpty_MuestraMensajeSinResultados` — "No se encontraron personas con ese criterio." (REQ-USB-05)

**Implementación (GREEN)**: Crear el partial con estructura modal Bootstrap 5. Integrar `aria-live="polite"` en región de resultados. En `_Form.cshtml`, reemplazar las ramas `if/else` por: un contenedor con botón `Buscar Persona` (estado vacío) o card con `Quitar`/`Cambiar` (estado seleccionado), más `@await Html.PartialAsync("_PersonaBuscadorModal")`. El hidden `Input.PersonaId` vive fuera del modal.

**Refactor**: Extraer el contenedor de card a un helper RenderPersonaCard para no duplicar markup.

**Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~UsuarioPageTests|CreatePageTests|EditPageTests"`

**Tamaño estimado**: ~90 líneas (50 producción + 40 tests)

---

### WU-8 — JavaScript + Cleanup (D-05)

**Objetivo**: JS modular para fetch async, debounce, estados visuales, manejo de teclado. Eliminar `IPersonaOptionsProvider`/`HttpPersonaOptionsProvider`/`FakePersonaOptionsProvider` y toda su infraestructura de DI, tests, y fixture.

**Archivos a tocar**:
- **Nuevo**: `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` (~80 líneas)
- **Eliminar**: `src/SGV.Web/Integration/Usuarios/IPersonaOptionsProvider.cs`
- **Eliminar**: `src/SGV.Web/Integration/Usuarios/HttpPersonaOptionsProvider.cs`
- **Eliminar**: `tests/SGV.Tests/Web/Usuario/FakePersonaOptionsProvider.cs`
- **Modificar**: `src/SGV.Web/Program.cs` (− registro `IPersonaOptionsProvider`)
- **Modificar**: `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs` (− overloads `CreateUsuarioLeaseAsync` con `IPersonaOptionsProvider`)
- **Modificar**: `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` − `FakePersonaOptionsProvider` → `FakePersonaApiClient` extendido en los leases
- **Modificar**: `tests/SGV.Tests/Web/Usuario/EditPageTests.cs` − ídem
- **Modificar**: `src/SGV.Web/Integration/Usuarios/IUsuarioForm.cs` (− `PersonaOptions`)

**Tests**: Ninguno nuevo (WU-8 es JS + cleanup; el JS se ejercita indirectamente por los tests de WU-5/6/7).
**Smoke manual documentado**: Verificar `Esc`/backdrop/X, foco inicial, `Seleccionar` setea hidden, `aria-label` en botones, `409` preserva form.

**Implementación (GREEN)**: Crear JS con:
- `_openModal()`: `fetch` a `/api/v1/personas/consulta` con `{search, soloSinUsuario, p, pageSize:25}`
- Debounce 300ms en input + `Enter` como trigger
- Estados: `#estado-inicial`, `#estado-empty`, `#estado-loading` (spinner + disable), `#estado-error`
- Paginación: `Anterior`/numérica/`Siguiente` — elipsis si `totalPages > 7` (`±2`)
- `Seleccionar`: cierra modal, setea hidden, actualiza card, dispara `change`
- Cierre: `Esc`/backdrop/X sin modificar hidden; foco vuelve al disparador

**Refactor del cleanup**: Tras borrar `IPersonaOptionsProvider`, asegurar que no haya referencias colgantes (grep por `IPersonaOptionsProvider` — debe ser 0 hits). Actualizar `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` y `EditPageTests.cs` para que usen `FakePersonaApiClient.QueryHandler` en lugar de `FakePersonaOptionsProvider`.

**Validación**: `dotnet build SGV.slnx` (0 errores, 0 warnings nuevos), `dotnet test SGV.slnx` (suite completa verde), `cd src/SGV.Web && bun install && bun run build` (bundle OK). Smoke: navegador con `Ctrl+Shift+R`, probar ciclo completo crear→buscar→seleccionar→guardar, editar→cambiar→guardar, 409 forzado.

**Tamaño estimado**: ~130 líneas (80 JS + 50 cleanup producción/tests)

---

## Convenciones de commits

Cada WU produce commits RED → GREEN → (REFACTOR opcional). Sin `Co-Authored-By`. Mensajes en español con conventional commits:

| WU | Commits sugeridos |
|----|-------------------|
| WU-1 | `test(repo): add [MySqlFact] for soloSinUsuario filter`, `feat(repo): add soloSinUsuario LEFT JOIN and cortocircuito` |
| WU-2 | `test(svc): add PersonaServicioConsulta soloSinUsuario tests`, `feat(svc): propagate soloSinUsuario to repository` |
| WU-3 | `test(api): add PersonasController soloSinUsuario tests`, `feat(api): accept soloSinUsuario in GetConsulta` |
| WU-4 | `test(client): add BuildQueryUri soloSinUsuario + Fake tests`, `feat(client): wire soloSinUsuario in PersonaApiClient` |
| WU-5 | `test(web): add Create page no-select tests + 409 feedback`, `feat(web): replace PersonaOptions with modal selector in Create` |
| WU-6 | `test(web): add Edit page card + Quitar tests`, `feat(web): replace PersonaOptions with card in Edit` |
| WU-7 | `test(web): add modal partial accessibility + state tests`, `feat(web): create _PersonaBuscadorModal partial` |
| WU-8 | `feat(web): add usuario-persona-buscador.js`, `refactor(web): remove IPersonaOptionsProvider and all references` |

---

## Plan de PR

~600 líneas estimadas (High budget risk). Se recomiendan **3 PRs** encadenados:

| PR | WUs | Base | Scope |
|----|-----|------|-------|
| PR-1 | WU-1..3 | `main` | Backend: repo + service + controller + tests backend |
| PR-2 | WU-4 | `main` (o PR-1 branch) | Client: persona API client + Fake extendido + tests |
| PR-3 | WU-5..8 | `main` (o PR-2 branch) | Web: Create/Edit/Modal/JS + cleanup `IPersonaOptionsProvider` |

Estrategia pendiente: `stacked-to-main` (cada PR mergea a main independiente) vs `feature-branch-chain` (PRs encadenados, sólo el último mergea). El orquestador debe preguntar al usuario y cachear la decisión antes de `sdd-apply`.

---

## Riesgos del plan

| Riesgo | Impacto | Mitigación |
|--------|---------|------------|
| WU-1 `[MySqlFact]` requiere MySQL local | Bloqueo si no hay MySQL | Los tests existentes ya usan `[MySqlFact]` con skip automático; no hay bloqueo, pero 4 tests quedarían skipeados |
| WU-7/8 JS complejo con debounce + estados | Tests `[WebIntegration]` no cubren JS puro | Los tests de WU-7 verifican HTML renderizado (atributos, estado inicial); el JS se valida en smoke manual |
| WU-8 cleanup rompe tests existentes | Falsos positivos | Los tests de `CreatePageTests`/`EditPageTests` se modifican en el mismo commit WU-8; `dotnet test` completo debe quedar verde |

---

## Validación previa a apply

- `dotnet build SGV.slnx` — 0 errores, 0 warnings nuevos (23 CS8524 preexistentes tolerados)
- `dotnet test SGV.slnx` — suite completa verde (~2412 tests + ~24 nuevos = ~2436)
- `cd src/SGV.Web && bun install && bun run build` — bundle frontend OK
- Smoke manual:
  1. Admin → Crear Usuario → botón `Buscar Persona` visible, sin `<select>`
  2. Modal: abrir → ver estado Inicial → buscar → seleccionar → card actualizada → Guardar OK
  3. Editar: card preseleccionada → Quitar → estado vacío → Buscar → seleccionar otra → Guardar
  4. Forzar 409 (misma persona en 2 pestañas) → feedback en `Input.PersonaId` sin perder el form
  5. `Esc`/backdrop cierran sin modificar selección
